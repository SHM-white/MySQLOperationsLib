# 服务模块初始化文件
from app.services.recommendations import RecommendationService
from app.services.aggregation import AggregationService
from app.services.dingtalk_client import DingTalkClient, dingtalk_client

__all__ = [
    "RecommendationService",
    "AggregationService",
    "DingTalkClient",
    "dingtalk_client"
]
