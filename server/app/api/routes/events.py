from datetime import datetime
from typing import List, Dict, Any, Optional

from fastapi import APIRouter, Depends, Query, Path, Body
from pydantic import BaseModel, Field, validator

from app.api.deps import DB, get_event_repo
from app.api.response_models import DataResponse, PaginatedResponse, EventResponse
from app.api.errors import NotFoundError
from app.repositories.event_repo import EventRepository

router = APIRouter(prefix="/events", tags=["事件"])


# 事件创建请求模型
class EventCreate(BaseModel):
    """事件创建请求模型"""
    user_id: str = Field(..., description="用户ID")
    timestamp: Optional[datetime] = Field(None, description="事件时间")
    event_type: str = Field(..., description="事件类型")
    source: str = Field(..., description="事件来源")
    data: Dict[str, Any] = Field(..., description="事件数据")
    
    @validator("event_type")
    def validate_event_type(cls, v):
        """验证事件类型"""
        allowed_types = ["WORK", "BREAK", "WATER", "POSTURE", "ENVIRONMENT"]
        if v not in allowed_types:
            raise ValueError(f"Event type must be one of {', '.join(allowed_types)}")
        return v
    
    @validator("source")
    def validate_source(cls, v):
        """验证事件来源"""
        allowed_sources = ["desktop", "iot", "manual"]
        if v not in allowed_sources:
            raise ValueError(f"Source must be one of {', '.join(allowed_sources)}")
        return v


@router.post("/", response_model=DataResponse[EventResponse])
async def create_event(
    event: EventCreate,
    db: DB,
    event_repo: EventRepository = Depends(get_event_repo)
):
    """
    创建事件
    
    上报新的事件，如工作状态、休息、饮水、姿势、环境等
    """
    # 设置默认时间戳
    if not event.timestamp:
        event.timestamp = datetime.utcnow()
    
    # 创建事件
    created_event = await event_repo.create(event.dict())
    
    return {
        "status": "success",
        "message": "Event created successfully",
        "data": created_event
    }


@router.get("/{event_id}", response_model=DataResponse[EventResponse])
async def get_event(
    event_id: str = Path(..., description="事件ID"),
    db: DB,
    event_repo: EventRepository = Depends(get_event_repo)
):
    """
    获取事件
    
    通过ID获取事件详情
    """
    event = await event_repo.get_by_id(event_id)
    
    if not event:
        raise NotFoundError(message="Event not found", detail={"event_id": event_id})
    
    return {
        "status": "success",
        "data": event
    }


@router.get("/user/{user_id}", response_model=PaginatedResponse[EventResponse])
async def get_user_events(
    user_id: str = Path(..., description="用户ID"),
    start_time: Optional[datetime] = Query(None, description="开始时间"),
    end_time: Optional[datetime] = Query(None, description="结束时间"),
    event_type: Optional[str] = Query(None, description="事件类型"),
    page: int = Query(1, description="页码"),
    size: int = Query(20, description="每页数量"),
    db: DB,
    event_repo: EventRepository = Depends(get_event_repo)
):
    """
    获取用户事件
    
    获取用户在指定时间范围内的事件列表
    """
    # 设置默认时间范围
    if not start_time:
        start_time = datetime.utcnow() - datetime.timedelta(days=7)
    
    if not end_time:
        end_time = datetime.utcnow()
    
    # 获取事件列表
    events = await event_repo.get_by_user_and_timerange(
        user_id=user_id,
        start_time=start_time,
        end_time=end_time,
        event_type=event_type
    )
    
    # 计算分页
    total = len(events)
    pages = (total + size - 1) // size  # 向上取整
    
    start_idx = (page - 1) * size
    end_idx = min(start_idx + size, total)
    
    paginated_events = events[start_idx:end_idx]
    
    return {
        "status": "success",
        "data": paginated_events,
        "meta": {
            "page": page,
            "size": size,
            "total": total,
            "pages": pages
        }
    }
