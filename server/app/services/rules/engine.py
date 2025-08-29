from datetime import datetime, timedelta
from typing import List, Dict, Any, Optional, Union, Set

import structlog

from app.core.config import settings
from app.models.event import Rule, Recommendation
from app.repositories.rule_repo import RuleRepository
from app.repositories.recommendation_repo import RecommendationRepository

logger = structlog.get_logger(__name__)


class RuleCondition:
    """规则条件解析器"""
    
    def __init__(self, condition: Dict[str, Any]):
        self.type = condition.get("type")
        self.metric = condition.get("metric")
        self.op = condition.get("op")
        self.value = condition.get("value")
    
    def evaluate(self, metrics: Dict[str, Any]) -> bool:
        """
        评估条件是否满足
        
        Args:
            metrics: 用户指标数据
            
        Returns:
            bool: 条件是否满足
        """
        # 获取指标值
        if self.metric not in metrics:
            return False
        
        metric_value = metrics[self.metric]
        
        # 根据操作符评估
        if self.op == "==":
            return metric_value == self.value
        elif self.op == "!=":
            return metric_value != self.value
        elif self.op == ">":
            return metric_value > self.value
        elif self.op == ">=":
            return metric_value >= self.value
        elif self.op == "<":
            return metric_value < self.value
        elif self.op == "<=":
            return metric_value <= self.value
        elif self.op == "in":
            return metric_value in self.value
        elif self.op == "not_in":
            return metric_value not in self.value
        else:
            logger.warning("Unknown operator", op=self.op)
            return False


class RuleEngine:
    """
    规则引擎
    负责匹配规则和生成槽位
    """
    
    def __init__(
        self, 
        rule_repo: RuleRepository,
        recommendation_repo: RecommendationRepository
    ):
        self.rule_repo = rule_repo
        self.recommendation_repo = recommendation_repo
    
    async def match(
        self, 
        user_id: str, 
        metrics: Dict[str, Any]
    ) -> List[Dict[str, Any]]:
        """
        匹配规则并生成槽位
        
        Args:
            user_id: 用户ID
            metrics: 用户指标数据
            
        Returns:
            List[Dict[str, Any]]: 生成的槽位列表
        """
        # 获取所有激活的规则
        rules = await self.rule_repo.get_all_active()
        
        slots = []
        matched_rule_types = set()  # 已匹配的规则类型集合
        
        # 按优先级处理规则
        for rule in rules:
            rule_condition = RuleCondition(rule.condition)
            
            # 跳过已匹配同类型的规则
            rule_type = rule.condition.get("type")
            if rule_type in matched_rule_types:
                continue
            
            # 评估规则条件
            if rule_condition.evaluate(metrics):
                # 检查冷却期
                recent_recommendations = await self.recommendation_repo.get_recent_by_rule(
                    user_id=user_id,
                    rule_id=rule.id,
                    hours=rule.cooldown_minutes / 60  # 转换为小时
                )
                
                if recent_recommendations:
                    logger.info(
                        "Rule in cooldown period", 
                        rule_id=rule.id, 
                        rule_name=rule.name, 
                        user_id=user_id
                    )
                    continue
                
                # 生成槽位
                slot = self._generate_slot(rule, metrics)
                slots.append(slot)
                
                # 记录已匹配的规则类型
                matched_rule_types.add(rule_type)
        
        return slots
    
    def _generate_slot(self, rule: Rule, metrics: Dict[str, Any]) -> Dict[str, Any]:
        """
        根据规则和指标生成槽位
        
        Args:
            rule: 规则对象
            metrics: 用户指标数据
            
        Returns:
            Dict[str, Any]: 生成的槽位
        """
        # 复制槽位模板
        slot = rule.slot_template.copy()
        
        # 填充动态值
        if "reason" in slot and "{value}" in slot["reason"]:
            metric_name = rule.condition.get("metric")
            metric_value = metrics.get(metric_name, "unknown")
            slot["reason"] = slot["reason"].replace("{value}", str(metric_value))
        
        # 添加规则ID
        slot["rule_id"] = str(rule.id)
        
        return slot
