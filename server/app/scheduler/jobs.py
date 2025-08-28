from datetime import datetime, date, timedelta
from typing import Dict, Any, List, Optional

import structlog
from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.jobstores.sqlalchemy import SQLAlchemyJobStore
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.db.session import get_db, AsyncSessionLocal
from app.repositories.event_repo import EventRepository
from app.repositories.recommendation_repo import RecommendationRepository
from app.services.aggregation import AggregationService
from app.services.rules.engine import RuleEngine
from app.services.recommendations import RecommendationService
from app.services.dingtalk_client import dingtalk_client

logger = structlog.get_logger(__name__)


class SchedulerService:
    """
    调度器服务
    管理定时任务
    """
    
    def __init__(self):
        """初始化调度器"""
        jobstores = {
            'default': SQLAlchemyJobStore(url=settings.DATABASE_URL.replace('+asyncpg', ''))
        }
        
        self.scheduler = AsyncIOScheduler(jobstores=jobstores)
        
    def start(self):
        """启动调度器"""
        if not self.scheduler.running:
            self.scheduler.start()
            logger.info("Scheduler started")
            
            # 添加定时任务
            self._add_jobs()
    
    def _add_jobs(self):
        """添加定时任务"""
        # 每小时聚合一次事件
        self.scheduler.add_job(
            func=self._run_aggregation_job,
            trigger='cron',
            hour='*',  # 每小时
            minute='0',  # 整点
            id='hourly_aggregation',
            replace_existing=True,
            misfire_grace_time=600  # 10分钟宽限期
        )
        
        # 每天生成和发送每日总结
        self.scheduler.add_job(
            func=self._run_daily_summary_job,
            trigger='cron',
            hour='19',  # 每天下午7点
            minute='0',
            id='daily_summary',
            replace_existing=True,
            misfire_grace_time=1800  # 30分钟宽限期
        )
        
        # 每5分钟检查一次待发送的推荐
        self.scheduler.add_job(
            func=self._run_recommendation_push_job,
            trigger='interval',
            minutes=5,
            id='recommendation_push',
            replace_existing=True,
            misfire_grace_time=300  # 5分钟宽限期
        )
        
        logger.info("Jobs added to scheduler")
    
    async def _run_aggregation_job(self):
        """
        聚合事件任务
        聚合未处理的事件生成指标
        """
        logger.info("Running aggregation job")
        
        async with AsyncSessionLocal() as db:
            try:
                # 获取所有用户今日数据
                event_repo = EventRepository(db)
                
                # 获取唯一用户列表
                # 在实际应用中，可能需要分批处理
                # 这里简化处理，假设用户数量不会太多
                today = date.today()
                yesterday = today - timedelta(days=1)
                
                # 聚合今日和昨日的数据
                for target_date in [yesterday, today]:
                    await self._aggregate_for_date(db, target_date)
                
                await db.commit()
                logger.info("Aggregation job completed successfully")
                
            except Exception as e:
                await db.rollback()
                logger.exception("Error in aggregation job", error=str(e))
    
    async def _aggregate_for_date(self, db: AsyncSession, target_date: date):
        """为指定日期进行聚合"""
        # 获取所有用户
        # 实际应用中，应该从用户表或事件表中查询活跃用户
        # 这里简化为从当天事件中提取用户
        event_repo = EventRepository(db)
        start_time = datetime.combine(target_date, datetime.min.time())
        end_time = datetime.combine(target_date, datetime.max.time())
        
        # 获取所有事件，按用户分组
        query = f"""
        SELECT DISTINCT user_id 
        FROM events 
        WHERE timestamp >= '{start_time}' 
        AND timestamp <= '{end_time}'
        AND processed = false
        """
        
        result = await db.execute(query)
        user_ids = [row[0] for row in result]
        
        if not user_ids:
            logger.info("No users with events for aggregation", date=target_date)
            return
        
        logger.info(
            "Aggregating data for users", 
            date=target_date, 
            user_count=len(user_ids)
        )
        
        # 为每个用户聚合数据
        aggregation_service = AggregationService(db)
        
        for user_id in user_ids:
            # 聚合用户数据
            metrics = await aggregation_service.aggregate_daily_metrics(
                user_id=user_id,
                target_date=target_date
            )
            
            if metrics:
                # 将聚合结果存入数据库
                # 检查是否已存在
                query = f"""
                SELECT id FROM daily_metrics 
                WHERE user_id = '{user_id}' 
                AND date = '{target_date}'
                """
                
                result = await db.execute(query)
                existing = result.first()
                
                if existing:
                    # 更新现有记录
                    query = f"""
                    UPDATE daily_metrics 
                    SET metrics = '{json.dumps(metrics)}',
                        updated_at = NOW()
                    WHERE id = '{existing[0]}'
                    """
                else:
                    # 创建新记录
                    query = f"""
                    INSERT INTO daily_metrics (id, user_id, date, metrics, created_at, updated_at)
                    VALUES (uuid_generate_v4(), '{user_id}', '{target_date}', 
                            '{json.dumps(metrics)}', NOW(), NOW())
                    """
                
                await db.execute(query)
                logger.info(
                    "Aggregated metrics saved", 
                    user_id=user_id, 
                    date=target_date
                )
    
    async def _run_daily_summary_job(self):
        """
        每日总结任务
        生成并发送每日健康总结
        """
        logger.info("Running daily summary job")
        
        async with AsyncSessionLocal() as db:
            try:
                # 获取今日数据
                today = date.today()
                
                # 获取所有用户今日聚合数据
                query = f"""
                SELECT user_id, metrics 
                FROM daily_metrics 
                WHERE date = '{today}'
                """
                
                result = await db.execute(query)
                user_metrics = [(row[0], row[1]) for row in result]
                
                if not user_metrics:
                    logger.info("No user metrics available for daily summary")
                    return
                
                # 获取所有用户的画像
                user_ids = [um[0] for um in user_metrics]
                user_id_list = ",".join([f"'{uid}'" for uid in user_ids])
                
                query = f"""
                SELECT user_id, settings, preferences 
                FROM user_profiles 
                WHERE user_id IN ({user_id_list})
                """
                
                result = await db.execute(query)
                user_profiles = {row[0]: {"settings": row[1], "preferences": row[2]} for row in result}
                
                # 为每个用户生成每日总结
                for user_id, metrics in user_metrics:
                    if user_id in user_profiles:
                        await self._generate_and_send_summary(
                            db=db,
                            user_id=user_id,
                            metrics=metrics,
                            profile=user_profiles[user_id]
                        )
                
                await db.commit()
                logger.info("Daily summary job completed successfully")
                
            except Exception as e:
                await db.rollback()
                logger.exception("Error in daily summary job", error=str(e))
    
    async def _generate_and_send_summary(
        self, 
        db: AsyncSession,
        user_id: str,
        metrics: Dict[str, Any],
        profile: Dict[str, Any]
    ):
        """为用户生成并发送每日总结"""
        try:
            # 创建特定的总结槽位
            summary_slot = {
                "type": "daily_summary",
                "reason": "每日健康总结",
                "rule_id": None
            }
            
            # 创建用户画像对象
            user = type('UserProfile', (), {
                'user_id': user_id,
                'settings': profile.get('settings', {}),
                'preferences': profile.get('preferences', {})
            })
            
            # 生成总结
            recommendation_service = RecommendationService()
            recommendations = await recommendation_service.generate(
                user=user,
                slots=[summary_slot],
                metrics=metrics
            )
            
            if not recommendations:
                logger.warning("No summary generated", user_id=user_id)
                return
            
            summary = recommendations[0]
            
            # 保存到数据库
            repo = RecommendationRepository(db)
            recommendation = await repo.create({
                "user_id": user_id,
                "rule_id": None,
                "content": summary["content"],
                "slots": summary_slot,
                "context": summary.get("context", {})
            })
            
            # 发送到钉钉
            response = await dingtalk_client.send_markdown(
                title="每日健康总结",
                text=summary["content"]
            )
            
            # 更新发送状态
            if response.get("errcode") == 0:
                await repo.update_status(
                    recommendation_id=recommendation.id,
                    status="sent"
                )
                logger.info("Daily summary sent successfully", user_id=user_id)
            else:
                await repo.update_status(
                    recommendation_id=recommendation.id,
                    status="failed"
                )
                logger.error(
                    "Failed to send daily summary", 
                    user_id=user_id, 
                    error=response
                )
            
        except Exception as e:
            logger.exception(
                "Error generating and sending summary", 
                user_id=user_id, 
                error=str(e)
            )
    
    async def _run_recommendation_push_job(self):
        """
        推荐推送任务
        检查并发送待发送的推荐
        """
        logger.info("Running recommendation push job")
        
        async with AsyncSessionLocal() as db:
            try:
                # 获取所有待发送的推荐
                repo = RecommendationRepository(db)
                pending_recommendations = await repo.get_pending_recommendations()
                
                if not pending_recommendations:
                    logger.info("No pending recommendations to push")
                    return
                
                logger.info(
                    "Found pending recommendations", 
                    count=len(pending_recommendations)
                )
                
                # 发送推荐
                for recommendation in pending_recommendations:
                    # 发送到钉钉
                    response = await dingtalk_client.send_text(
                        content=recommendation.content
                    )
                    
                    # 更新发送状态
                    if response.get("errcode") == 0:
                        await repo.update_status(
                            recommendation_id=recommendation.id,
                            status="sent"
                        )
                        logger.info(
                            "Recommendation sent successfully", 
                            id=str(recommendation.id),
                            user_id=recommendation.user_id
                        )
                    else:
                        await repo.update_status(
                            recommendation_id=recommendation.id,
                            status="failed"
                        )
                        logger.error(
                            "Failed to send recommendation", 
                            id=str(recommendation.id),
                            user_id=recommendation.user_id,
                            error=response
                        )
                
                await db.commit()
                logger.info("Recommendation push job completed successfully")
                
            except Exception as e:
                await db.rollback()
                logger.exception("Error in recommendation push job", error=str(e))


# 创建全局实例
scheduler_service = SchedulerService()
