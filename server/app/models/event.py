from datetime import datetime
from typing import Dict, Any, Optional
from uuid import uuid4, UUID

from sqlalchemy import Column, String, DateTime, JSON, Text, Float, ForeignKey, Integer, Boolean
from sqlalchemy.dialects.postgresql import UUID as PgUUID
from sqlalchemy.orm import declarative_base, relationship

Base = declarative_base()


class Event(Base):
    """
    事件数据模型
    存储从桌面 Agent / IoT / 手动指令上报的各类事件
    """
    __tablename__ = "events"
    
    id = Column(PgUUID(as_uuid=True), primary_key=True, default=uuid4)
    user_id = Column(String(50), nullable=False, index=True)
    timestamp = Column(DateTime, nullable=False, default=datetime.utcnow, index=True)
    event_type = Column(String(50), nullable=False, index=True)
    source = Column(String(50), nullable=False)  # 事件来源: desktop, iot, manual
    data = Column(JSON, nullable=False)  # 事件详情 JSON
    processed = Column(Boolean, default=False)  # 是否已处理（聚合）
    created_at = Column(DateTime, default=datetime.utcnow)
    
    def __repr__(self) -> str:
        return f"<Event id={self.id} user_id={self.user_id} type={self.event_type}>"


class DailyMetrics(Base):
    """
    每日聚合指标
    存储每日聚合的健康指标数据
    """
    __tablename__ = "daily_metrics"
    
    id = Column(PgUUID(as_uuid=True), primary_key=True, default=uuid4)
    user_id = Column(String(50), nullable=False, index=True)
    date = Column(DateTime, nullable=False, index=True)
    metrics = Column(JSON, nullable=False)  # 聚合指标 JSON
    
    # 示例指标：
    # - work_duration_min: 总工作时长(分钟)
    # - continuous_max_min: 最长连续工作时长(分钟)
    # - water_intake_ml: 饮水量(毫升)
    # - water_target_ml: 饮水目标(毫升)
    # - break_count: 休息次数
    # - posture_alerts: 姿势提醒次数
    
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    def __repr__(self) -> str:
        return f"<DailyMetrics id={self.id} user_id={self.user_id} date={self.date}>"


class Rule(Base):
    """
    规则配置
    存储规则匹配条件和动作
    """
    __tablename__ = "rules"
    
    id = Column(PgUUID(as_uuid=True), primary_key=True, default=uuid4)
    name = Column(String(100), nullable=False)
    description = Column(Text, nullable=True)
    condition = Column(JSON, nullable=False)  # 规则条件 JSON
    slot_template = Column(JSON, nullable=False)  # 槽位模板 JSON
    is_active = Column(Boolean, default=True)
    priority = Column(Integer, default=0)  # 优先级，数字越大优先级越高
    cooldown_minutes = Column(Integer, default=30)  # 冷却时间(分钟)
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    def __repr__(self) -> str:
        return f"<Rule id={self.id} name={self.name}>"


class Recommendation(Base):
    """
    健康建议
    存储生成的健康建议和推送状态
    """
    __tablename__ = "recommendations"
    
    id = Column(PgUUID(as_uuid=True), primary_key=True, default=uuid4)
    user_id = Column(String(50), nullable=False, index=True)
    rule_id = Column(PgUUID(as_uuid=True), ForeignKey("rules.id"), nullable=True)
    content = Column(Text, nullable=False)  # 建议内容
    slots = Column(JSON, nullable=False)  # 触发槽位 JSON
    context = Column(JSON, nullable=True)  # 生成上下文 JSON
    status = Column(String(20), default="created")  # created, sent, failed
    sent_at = Column(DateTime, nullable=True)  # 发送时间
    created_at = Column(DateTime, default=datetime.utcnow)
    
    # 关联规则
    rule = relationship("Rule", backref="recommendations")
    
    def __repr__(self) -> str:
        return f"<Recommendation id={self.id} user_id={self.user_id}>"


class RecommendationFeedback(Base):
    """
    建议反馈
    存储用户对建议的反馈
    """
    __tablename__ = "recommendation_feedback"
    
    id = Column(PgUUID(as_uuid=True), primary_key=True, default=uuid4)
    recommendation_id = Column(PgUUID(as_uuid=True), ForeignKey("recommendations.id"), nullable=False)
    feedback_type = Column(String(20), nullable=False)  # ok, skip, nothelpful
    comment = Column(Text, nullable=True)  # 可选的反馈备注
    created_at = Column(DateTime, default=datetime.utcnow)
    
    # 关联建议
    recommendation = relationship("Recommendation", backref="feedback")
    
    def __repr__(self) -> str:
        return f"<RecommendationFeedback id={self.id} type={self.feedback_type}>"


class UserProfile(Base):
    """
    用户画像
    存储用户偏好和设置
    """
    __tablename__ = "user_profiles"
    
    user_id = Column(String(50), primary_key=True)
    display_name = Column(String(100), nullable=True)
    settings = Column(JSON, nullable=False, default=dict)  # 用户设置 JSON
    preferences = Column(JSON, nullable=False, default=dict)  # 用户偏好 JSON
    created_at = Column(DateTime, default=datetime.utcnow)
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow)
    
    def __repr__(self) -> str:
        return f"<UserProfile user_id={self.user_id}>"
