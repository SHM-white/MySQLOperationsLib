from datetime import datetime
from typing import Dict, Any, Optional, List, Union, Generic, TypeVar
from uuid import UUID

from pydantic import BaseModel, Field, validator


# 基础响应模型
class BaseResponse(BaseModel):
    """基础响应模型"""
    status: str = "success"
    message: Optional[str] = None


# 数据项类型变量
T = TypeVar("T")


# 数据响应模型
class DataResponse(BaseResponse, Generic[T]):
    """数据响应模型"""
    data: T


# 分页元数据
class PaginationMeta(BaseModel):
    """分页元数据"""
    page: int
    size: int
    total: int
    pages: int


# 分页响应模型
class PaginatedResponse(BaseResponse, Generic[T]):
    """分页响应模型"""
    data: List[T]
    meta: PaginationMeta


# 事件响应模型
class EventResponse(BaseModel):
    """事件响应模型"""
    id: UUID
    user_id: str
    timestamp: datetime
    event_type: str
    source: str
    data: Dict[str, Any]
    created_at: datetime
    
    class Config:
        orm_mode = True


# 规则响应模型
class RuleResponse(BaseModel):
    """规则响应模型"""
    id: UUID
    name: str
    description: Optional[str] = None
    condition: Dict[str, Any]
    slot_template: Dict[str, Any]
    is_active: bool
    priority: int
    cooldown_minutes: int
    created_at: datetime
    updated_at: datetime
    
    class Config:
        orm_mode = True


# 推荐响应模型
class RecommendationResponse(BaseModel):
    """推荐响应模型"""
    id: UUID
    user_id: str
    rule_id: Optional[UUID] = None
    content: str
    slots: Dict[str, Any]
    status: str
    sent_at: Optional[datetime] = None
    created_at: datetime
    
    class Config:
        orm_mode = True


# 反馈响应模型
class FeedbackResponse(BaseModel):
    """反馈响应模型"""
    id: UUID
    recommendation_id: UUID
    feedback_type: str
    comment: Optional[str] = None
    created_at: datetime
    
    class Config:
        orm_mode = True


# 用户画像响应模型
class UserProfileResponse(BaseModel):
    """用户画像响应模型"""
    user_id: str
    display_name: Optional[str] = None
    settings: Dict[str, Any]
    preferences: Dict[str, Any]
    created_at: datetime
    updated_at: datetime
    
    class Config:
        orm_mode = True


# 日指标响应模型
class DailyMetricsResponse(BaseModel):
    """日指标响应模型"""
    id: UUID
    user_id: str
    date: datetime
    metrics: Dict[str, Any]
    created_at: datetime
    updated_at: datetime
    
    class Config:
        orm_mode = True


# 错误响应模型
class ErrorResponse(BaseModel):
    """错误响应模型"""
    status: str = "error"
    message: str
    detail: Optional[Union[str, Dict[str, Any], List[Dict[str, Any]]]] = None
