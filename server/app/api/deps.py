from typing import Annotated, Dict, Any, Optional

from fastapi import Depends, HTTPException, status, Query, Header
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.config import settings
from app.core.security import SecurityVerifier, get_security_verifier
from app.db.session import get_db
from app.repositories.event_repo import EventRepository
from app.repositories.rule_repo import RuleRepository
from app.repositories.recommendation_repo import RecommendationRepository
from app.services.aggregation import AggregationService
from app.services.recommendations import RecommendationService
from app.services.rules.engine import RuleEngine


# 数据库会话依赖
DB = Annotated[AsyncSession, Depends(get_db)]


# 安全验证依赖
async def verify_signature(
    timestamp: str = Header(...),
    sign: str = Header(...),
    verifier: SecurityVerifier = Depends(get_security_verifier),
    request = None
):
    """
    验证请求签名
    
    Args:
        timestamp: 请求时间戳
        sign: 签名
        verifier: 安全验证器
        request: 请求对象
    """
    if settings.is_development and not settings.DINGTALK_SIGN_SECRET:
        # 开发环境且未配置签名密钥时，跳过验证
        return True
    
    # 验证签名
    await verifier.verify_dingtalk_signature(request, timestamp, sign)
    return True


# 仓库依赖
def get_event_repo(db: DB) -> EventRepository:
    """获取事件仓库"""
    return EventRepository(db)


def get_rule_repo(db: DB) -> RuleRepository:
    """获取规则仓库"""
    return RuleRepository(db)


def get_recommendation_repo(db: DB) -> RecommendationRepository:
    """获取推荐仓库"""
    return RecommendationRepository(db)


# 服务依赖
def get_aggregation_service(db: DB) -> AggregationService:
    """获取聚合服务"""
    return AggregationService(db)


def get_rule_engine(
    rule_repo=Depends(get_rule_repo),
    recommendation_repo=Depends(get_recommendation_repo)
) -> RuleEngine:
    """获取规则引擎"""
    return RuleEngine(rule_repo, recommendation_repo)


def get_recommendation_service() -> RecommendationService:
    """获取推荐服务"""
    return RecommendationService()
