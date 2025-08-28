from datetime import datetime, date, timedelta
from typing import List, Optional, Dict, Any, Union
from uuid import UUID

from sqlalchemy import select, func, and_, or_, desc
from sqlalchemy.ext.asyncio import AsyncSession

from app.models.event import Event


class EventRepository:
    """
    事件仓库
    提供事件数据的访问抽象
    """
    def __init__(self, db: AsyncSession):
        self.db = db
    
    async def create(self, event_data: Dict[str, Any]) -> Event:
        """创建新事件"""
        event = Event(**event_data)
        self.db.add(event)
        await self.db.flush()
        return event
    
    async def get_by_id(self, event_id: UUID) -> Optional[Event]:
        """根据 ID 获取事件"""
        result = await self.db.execute(select(Event).where(Event.id == event_id))
        return result.scalars().first()
    
    async def get_by_user_and_timerange(
        self, 
        user_id: str, 
        start_time: datetime, 
        end_time: datetime,
        event_type: Optional[str] = None,
        processed: Optional[bool] = None
    ) -> List[Event]:
        """获取用户在指定时间范围内的事件"""
        query = select(Event).where(
            and_(
                Event.user_id == user_id,
                Event.timestamp >= start_time,
                Event.timestamp <= end_time
            )
        )
        
        if event_type:
            query = query.where(Event.event_type == event_type)
            
        if processed is not None:
            query = query.where(Event.processed == processed)
            
        result = await self.db.execute(query.order_by(Event.timestamp))
        return result.scalars().all()
    
    async def get_events_for_daily_aggregation(
        self, 
        user_id: str, 
        target_date: date
    ) -> List[Event]:
        """获取用户某一天需要进行聚合的事件"""
        start_time = datetime.combine(target_date, datetime.min.time())
        end_time = datetime.combine(target_date, datetime.max.time())
        
        query = select(Event).where(
            and_(
                Event.user_id == user_id,
                Event.timestamp >= start_time,
                Event.timestamp <= end_time,
                Event.processed == False
            )
        )
        
        result = await self.db.execute(query.order_by(Event.timestamp))
        return result.scalars().all()
    
    async def mark_as_processed(self, event_ids: List[UUID]) -> None:
        """将事件标记为已处理"""
        if not event_ids:
            return
            
        query = select(Event).where(Event.id.in_(event_ids))
        result = await self.db.execute(query)
        events = result.scalars().all()
        
        for event in events:
            event.processed = True
            
        await self.db.flush()
    
    async def get_event_counts_by_type(
        self, 
        user_id: str, 
        start_time: datetime, 
        end_time: datetime
    ) -> Dict[str, int]:
        """获取用户在指定时间范围内各类型事件的数量"""
        query = select(
            Event.event_type,
            func.count(Event.id).label("count")
        ).where(
            and_(
                Event.user_id == user_id,
                Event.timestamp >= start_time,
                Event.timestamp <= end_time
            )
        ).group_by(Event.event_type)
        
        result = await self.db.execute(query)
        return {row[0]: row[1] for row in result}
