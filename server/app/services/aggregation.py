from datetime import datetime, date, timedelta
from typing import Dict, List, Any, Optional, Tuple, Sequence  
import structlog
from sqlalchemy.ext.asyncio import AsyncSession
from uuid import UUID

from app.repositories.event_repo import EventRepository
from app.models.event import Event, DailyMetrics

logger = structlog.get_logger(__name__)


class AggregationService:
    """
    聚合服务
    负责将原始事件聚合为每日指标
    """
    
    def __init__(self, db: AsyncSession):
        self.db = db
        self.event_repo = EventRepository(db)
    
    async def aggregate_daily_metrics(
        self, 
        user_id: str, 
        target_date: date
    ) -> Optional[Dict[str, Any]]:
        """
        聚合用户某一天的指标
        
        Args:
            user_id: 用户ID
            target_date: 目标日期
            
        Returns:
            Optional[Dict[str, Any]]: 聚合后的指标数据，如果没有事件则返回 None
        """
        # 获取用户当天的所有事件
        events = await self.event_repo.get_events_for_daily_aggregation(
            user_id=user_id,
            target_date=target_date
        )
        
        if not events:
            logger.info("No events to aggregate", user_id=user_id, date=target_date)
            return None
        
        # 按事件类型分组
        events_by_type = self._group_events_by_type(events)
        
        # 初始化指标数据
        metrics = {
            "date": target_date.isoformat(),
            "user_id": user_id,
            "work_duration_min": 0,  # 工作时长(分钟)
            "continuous_max_min": 0,  # 最长连续工作时长(分钟)
            "water_intake_ml": 0,     # 饮水量(毫升)
            "water_target_ml": 1500,  # 饮水目标(毫升)，默认值
            "water_progress": 0.0,      # 饮水进度
            "break_count": 0,         # 休息次数
            "posture_alerts": 0,      # 姿势提醒次数
            "events_count": len(events)  # 总事件数
        }
        
        # 计算工作时长
        work_metrics = self._calculate_work_metrics(events_by_type.get("WORK", []))
        metrics.update(work_metrics)
        
        # 计算饮水指标
        water_metrics = self._calculate_water_metrics(events_by_type.get("WATER", []))
        metrics.update(water_metrics)
        
        # 计算姿势指标
        posture_metrics = self._calculate_posture_metrics(events_by_type.get("POSTURE", []))
        metrics.update(posture_metrics)
        
        # 计算休息指标
        break_metrics = self._calculate_break_metrics(events_by_type.get("BREAK", []))
        metrics.update(break_metrics)
        
        # 更新环境指标
        env_metrics = self._calculate_environment_metrics(events_by_type.get("ENVIRONMENT", []))
        metrics.update(env_metrics)
        
        # 标记事件为已处理
        # 确保 event_ids 是 UUID 类型的列表
        event_ids = [event.id for event in events if isinstance(event.id, UUID)]

        # 修复排序问题，提取实际的 timestamp 值
        work_events = events_by_type.get("WORK", [])
        work_events = [event for event in work_events if isinstance(event.timestamp, datetime)]
        work_events.sort(key=lambda e: e.timestamp if isinstance(e.timestamp, datetime) else datetime.min)

        # 修复 last_timestamp 的类型问题，显式提取值
        last_timestamp: datetime | None = None
        current_continuous = 0
        for event in work_events:
            if isinstance(event.timestamp, datetime):
                event_timestamp = event.timestamp
                if last_timestamp and isinstance(last_timestamp, datetime):
                    time_diff = (event_timestamp - last_timestamp).total_seconds()
                    if time_diff < 300:  # 5分钟内
                        current_continuous += event.data.get("duration_min", 0)
                    else:
                        current_continuous = event.data.get("duration_min", 0)
                else:
                    current_continuous = event.data.get("duration_min", 0)
                last_timestamp = event_timestamp

        # 修复排序问题，提取实际的 timestamp 值
        work_events.sort(key=lambda e: e.timestamp if isinstance(e.timestamp, datetime) else datetime.min)
        env_events.sort(key=lambda e: e.timestamp if isinstance(e.timestamp, datetime) else datetime.min, reverse=True)

        # 修复 water_progress 的类型问题
        metrics["water_progress"] = round(progress * 100)

        return metrics
    
    def _group_events_by_type(self, events: Sequence[Event]) -> Dict[str, List[Event]]:
        """
        按事件类型分组
        
        Args:
            events: 事件列表
            
        Returns:
            Dict[str, List[Event]]: 按类型分组的事件
        """
        result = {}
        for event in events:
            if event.event_type not in result:
                result[event.event_type] = []
            result[event.event_type].append(event)
        return result
    
    def _calculate_work_metrics(self, work_events: List[Event]) -> Dict[str, Any]:
        """
        计算工作指标
        
        Args:
            work_events: 工作事件列表
            
        Returns:
            Dict[str, Any]: 工作指标
        """
        metrics = {
            "work_duration_min": 0,
            "continuous_max_min": 0
        }
        
        if not work_events:
            return metrics
        
        # 按时间排序
        work_events.sort(key=lambda e: e.timestamp)
        
        # 计算总工作时长
        total_minutes = 0
        continuous_max = 0
        current_continuous = 0
        last_timestamp = None
        
        for event in work_events:
            data = event.data or {}
            duration = data.get("duration_min", 0)
            
            if duration > 0:
                total_minutes += duration
                
                # 计算连续时长
                if last_timestamp and (event.timestamp - last_timestamp).total_seconds() < 300:  # 5分钟内
                    current_continuous += duration
                else:
                    current_continuous = duration
                
                continuous_max = max(continuous_max, current_continuous)
                last_timestamp = event.timestamp
        
        metrics["work_duration_min"] = total_minutes
        metrics["continuous_max_min"] = continuous_max
        
        return metrics
    
    def _calculate_water_metrics(self, water_events: List[Event]) -> Dict[str, Any]:
        """
        计算饮水指标
        
        Args:
            water_events: 饮水事件列表
            
        Returns:
            Dict[str, Any]: 饮水指标
        """
        metrics = {
            "water_intake_ml": 0
        }
        
        if not water_events:
            return metrics
        
        # 计算总饮水量
        total_ml = 0
        
        for event in water_events:
            data = event.data or {}
            amount = data.get("amount_ml", 0)
            
            if amount > 0:
                total_ml += amount
        
        metrics["water_intake_ml"] = total_ml
        
        # 计算饮水进度
        target_ml = 1500  # 默认目标
        progress = min(1.0, total_ml / target_ml) if target_ml > 0 else 0
        
        metrics["water_target_ml"] = target_ml
        metrics["water_progress"] = round(progress, 2) if isinstance(progress, float) else float(progress)
        
        return metrics
    
    def _calculate_posture_metrics(self, posture_events: List[Event]) -> Dict[str, Any]:
        """
        计算姿势指标
        
        Args:
            posture_events: 姿势事件列表
            
        Returns:
            Dict[str, Any]: 姿势指标
        """
        metrics = {
            "posture_alerts": 0,
            "posture_issues": []
        }
        
        if not posture_events:
            return metrics
        
        # 计算姿势提醒次数
        alerts_count = 0
        issues = []
        
        for event in posture_events:
            data = event.data or {}
            issue_type = data.get("issue_type")
            
            if issue_type:
                alerts_count += 1
                if issue_type not in issues:
                    issues.append(issue_type)
        
        metrics["posture_alerts"] = alerts_count
        metrics["posture_issues"] = issues
        
        return metrics
    
    def _calculate_break_metrics(self, break_events: List[Event]) -> Dict[str, Any]:
        """
        计算休息指标
        
        Args:
            break_events: 休息事件列表
            
        Returns:
            Dict[str, Any]: 休息指标
        """
        metrics = {
            "break_count": 0,
            "break_duration_min": 0
        }
        
        if not break_events:
            return metrics
        
        # 计算休息次数和总时长
        total_breaks = len(break_events)
        total_duration = 0
        
        for event in break_events:
            data = event.data or {}
            duration = data.get("duration_min", 0)
            
            if duration > 0:
                total_duration += duration
        
        metrics["break_count"] = total_breaks
        metrics["break_duration_min"] = total_duration
        
        return metrics
    
    def _calculate_environment_metrics(self, env_events: List[Event]) -> Dict[str, Any]:
        """
        计算环境指标
        
        Args:
            env_events: 环境事件列表
            
        Returns:
            Dict[str, Any]: 环境指标
        """
        metrics = {}
        
        if not env_events:
            return metrics
        
        # 获取最新的环境数据
        env_events.sort(key=lambda e: e.timestamp, reverse=True)
        latest_env = env_events[0].data or {}
        
        # 提取环境参数
        temperature = latest_env.get("temperature")
        humidity = latest_env.get("humidity")
        light = latest_env.get("light")
        noise = latest_env.get("noise")
        
        if temperature is not None:
            metrics["environment_temperature"] = temperature
        
        if humidity is not None:
            metrics["environment_humidity"] = humidity
        
        if light is not None:
            metrics["environment_light"] = light
        
        if noise is not None:
            metrics["environment_noise"] = noise
        
        return metrics
