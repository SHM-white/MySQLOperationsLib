import pytest
from datetime import datetime, timedelta
from uuid import uuid4

from app.services.rules.engine import RuleCondition


class TestRuleCondition:
    """测试规则条件解析器"""
    
    def test_equality_operator(self):
        """测试等于操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "==",
            "value": 10
        })
        
        metrics = {"test_value": 10}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is False
    
    def test_greater_than_operator(self):
        """测试大于操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": ">",
            "value": 10
        })
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 5}
        assert condition.evaluate(metrics) is False
    
    def test_greater_than_or_equal_operator(self):
        """测试大于等于操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": ">=",
            "value": 10
        })
        
        metrics = {"test_value": 10}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 5}
        assert condition.evaluate(metrics) is False
    
    def test_less_than_operator(self):
        """测试小于操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "<",
            "value": 10
        })
        
        metrics = {"test_value": 5}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is False
    
    def test_less_than_or_equal_operator(self):
        """测试小于等于操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "<=",
            "value": 10
        })
        
        metrics = {"test_value": 10}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 5}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is False
    
    def test_not_equal_operator(self):
        """测试不等于操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "!=",
            "value": 10
        })
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 10}
        assert condition.evaluate(metrics) is False
    
    def test_in_operator(self):
        """测试包含操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "in",
            "value": [10, 20, 30]
        })
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 40}
        assert condition.evaluate(metrics) is False
    
    def test_not_in_operator(self):
        """测试不包含操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "not_in",
            "value": [10, 20, 30]
        })
        
        metrics = {"test_value": 40}
        assert condition.evaluate(metrics) is True
        
        metrics = {"test_value": 20}
        assert condition.evaluate(metrics) is False
    
    def test_missing_metric(self):
        """测试缺失指标"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "==",
            "value": 10
        })
        
        metrics = {"other_value": 10}
        assert condition.evaluate(metrics) is False
    
    def test_unknown_operator(self):
        """测试未知操作符"""
        condition = RuleCondition({
            "type": "TEST",
            "metric": "test_value",
            "op": "unknown",
            "value": 10
        })
        
        metrics = {"test_value": 10}
        assert condition.evaluate(metrics) is False
