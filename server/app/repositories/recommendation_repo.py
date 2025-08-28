from datetime import datetime, timedelta
from typing import List, Optional, Dict, Any, Tuple
from uuid import UUID

from sqlalchemy import select, and_, or_, desc, func
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.event import Recommendation, RecommendationFeedback


class RecommendationRepository:
    """
    推荐仓库
    提供推荐数据的访问抽象
    """
    def __init__(self, db: AsyncSession):
        self.db = db
    
    async def create(self, recommendation_data: Dict[str, Any]) -> Recommendation:
        """创建新推荐"""
        recommendation = Recommendation(**recommendation_data)
        self.db.add(recommendation)
        await self.db.flush()
        return recommendation
    
    async def get_by_id(self, recommendation_id: UUID) -> Optional[Recommendation]:
        """根据 ID 获取推荐"""
        result = await self.db.execute(
            select(Recommendation).where(Recommendation.id == recommendation_id)
        )
        return result.scalars().first()
    
    async def get_recent_by_user(
        self, 
        user_id: str, 
        limit: int = 10
    ) -> List[Recommendation]:
        """获取用户最近的推荐"""
        query = select(Recommendation).where(
            Recommendation.user_id == user_id
        ).order_by(desc(Recommendation.created_at)).limit(limit)
        
        result = await self.db.execute(query)
        return result.scalars().all()
    
    async def get_recent_by_rule(
        self, 
        user_id: str, 
        rule_id: UUID, 
        hours: int = 1
    ) -> List[Recommendation]:
        """获取用户最近的特定规则推荐"""
        time_threshold = datetime.utcnow() - timedelta(hours=hours)
        
        query = select(Recommendation).where(
            and_(
                Recommendation.user_id == user_id,
                Recommendation.rule_id == rule_id,
                Recommendation.created_at >= time_threshold
            )
        ).order_by(desc(Recommendation.created_at))
        
        result = await self.db.execute(query)
        return result.scalars().all()
    
    async def update_status(
        self, 
        recommendation_id: UUID, 
        status: str, 
        sent_at: Optional[datetime] = None
    ) -> Optional[Recommendation]:
        """更新推荐状态"""
        recommendation = await self.get_by_id(recommendation_id)
        if not recommendation:
            return None
            
        recommendation.status = status
        if sent_at:
            recommendation.sent_at = sent_at
        else:
            recommendation.sent_at = datetime.utcnow()
            
        await self.db.flush()
        return recommendation
    
    async def add_feedback(
        self, 
        recommendation_id: UUID, 
        feedback_type: str, 
        comment: Optional[str] = None
    ) -> Tuple[Optional[Recommendation], Optional[RecommendationFeedback]]:
        """添加反馈"""
        recommendation = await self.get_by_id(recommendation_id)
        if not recommendation:
            return None, None
            
        feedback = RecommendationFeedback(
            recommendation_id=recommendation_id,
            feedback_type=feedback_type,
            comment=comment
        )
        
        self.db.add(feedback)
        await self.db.flush()
        return recommendation, feedback
    
    async def get_pending_recommendations(self) -> List[Recommendation]:
        """获取待发送的推荐"""
        query = select(Recommendation).where(
            Recommendation.status == "created"
        ).order_by(Recommendation.created_at)
        
        result = await self.db.execute(query)
        return result.scalars().all()
        
    async def get_feedback_stats(
        self, 
        user_id: Optional[str] = None, 
        days: int = 30
    ) -> Dict[str, int]:
        """获取反馈统计"""
        time_threshold = datetime.utcnow() - timedelta(days=days)
        
        query = select(
            RecommendationFeedback.feedback_type,
            func.count(RecommendationFeedback.id).label("count")
        ).join(
            Recommendation, 
            RecommendationFeedback.recommendation_id == Recommendation.id
        ).where(
            RecommendationFeedback.created_at >= time_threshold
        )
        
        if user_id:
            query = query.where(Recommendation.user_id == user_id)
            
        query = query.group_by(RecommendationFeedback.feedback_type)
        
        result = await self.db.execute(query)
        return {row[0]: row[1] for row in result}
