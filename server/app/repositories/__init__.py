# 仓库模块初始化文件
from app.repositories.event_repo import EventRepository
from app.repositories.rule_repo import RuleRepository
from app.repositories.recommendation_repo import RecommendationRepository

__all__ = [
    "EventRepository",
    "RuleRepository",
    "RecommendationRepository"
]
