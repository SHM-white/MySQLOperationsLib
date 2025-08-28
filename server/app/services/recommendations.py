import json
import time
from datetime import datetime
from typing import Dict, List, Any, Optional, Tuple, Union

import structlog
from prometheus_client import Counter

from app.models.event import UserProfile
from app.services.llm.base import ChatMessage
from app.services.llm.provider_router import llm_router

logger = structlog.get_logger(__name__)

# 指标
recommendation_generated = Counter(
    "hg_recommendations_generated_total", 
    "Total number of recommendations generated",
    ["type", "status"]
)


class RecommendationService:
    """
    推荐服务
    生成健康建议
    """
    
    async def generate(
        self, 
        user: UserProfile, 
        slots: List[Dict[str, Any]], 
        metrics: Dict[str, Any]
    ) -> List[Dict[str, Any]]:
        """
        生成健康建议
        
        Args:
            user: 用户信息
            slots: 槽位列表
            metrics: 用户指标数据
            
        Returns:
            List[Dict[str, Any]]: 生成的建议列表
        """
        if not slots:
            logger.info("No slots to generate recommendations for")
            return []
        
        # 获取用户偏好
        preferences = user.preferences or {}
        
        # 准备生成建议
        recommendations = []
        
        for slot in slots:
            try:
                # 构建 prompt
                messages = self._build_prompt(slot, metrics, preferences)
                
                # 调用 LLM
                response = await llm_router.chat(
                    messages=messages,
                    temperature=0.7,
                    max_tokens=512
                )
                
                # 解析响应
                recommendation = self._parse_response(response, slot)
                
                if recommendation:
                    recommendations.append(recommendation)
                    recommendation_generated.labels(
                        type=slot.get("type", "unknown"), 
                        status="success"
                    ).inc()
                else:
                    # 使用默认模板作为回退
                    fallback = self._get_fallback_recommendation(slot)
                    recommendations.append(fallback)
                    recommendation_generated.labels(
                        type=slot.get("type", "unknown"), 
                        status="fallback"
                    ).inc()
                
            except Exception as e:
                logger.exception(
                    "Error generating recommendation", 
                    slot=slot, 
                    error=str(e)
                )
                
                # 使用默认模板作为回退
                fallback = self._get_fallback_recommendation(slot)
                recommendations.append(fallback)
                recommendation_generated.labels(
                    type=slot.get("type", "unknown"), 
                    status="error"
                ).inc()
        
        return recommendations
    
    def _build_prompt(
        self, 
        slot: Dict[str, Any], 
        metrics: Dict[str, Any],
        preferences: Dict[str, Any]
    ) -> List[ChatMessage]:
        """
        构建 prompt
        
        Args:
            slot: 槽位信息
            metrics: 用户指标数据
            preferences: 用户偏好
            
        Returns:
            List[ChatMessage]: 构建的消息列表
        """
        # 系统消息
        system_message: ChatMessage = {
            "role": "system",
            "content": "你是办公健康助手，只能使用提供的事实，禁止医学诊断。"
        }
        
        # 构建事实字符串
        facts = []
        for key, value in metrics.items():
            if isinstance(value, (int, float)):
                facts.append(f"{key}={value}")
            else:
                facts.append(f"{key}=\"{value}\"")
        
        facts_str = "; ".join(facts)
        
        # 构建槽位字符串
        slot_type = slot.get("type", "unknown")
        slot_reason = slot.get("reason", "")
        slot_action = slot.get("action", "")
        
        slot_str = f"{slot_type}:{slot_reason}"
        if slot_action:
            slot_str += f"|action:{slot_action}"
        
        # 用户偏好
        style = preferences.get("message_style", "简洁中文，列点。")
        
        # 用户消息
        user_message: ChatMessage = {
            "role": "user",
            "content": (
                f"FACTS: {facts_str}\n"
                f"SLOTS: {slot_str}\n"
                f"STYLE: {style}\n"
                f"OUTPUT: JSON {{\"messages\":[\"...\"]}}"
            )
        }
        
        return [system_message, user_message]
    
    def _parse_response(
        self, 
        response: str, 
        slot: Dict[str, Any]
    ) -> Optional[Dict[str, Any]]:
        """
        解析 LLM 响应
        
        Args:
            response: LLM 响应内容
            slot: 槽位信息
            
        Returns:
            Optional[Dict[str, Any]]: 解析后的建议，如果解析失败则返回 None
        """
        try:
            # 尝试提取 JSON
            start_idx = response.find("{")
            end_idx = response.rfind("}")
            
            if start_idx != -1 and end_idx != -1:
                json_str = response[start_idx:end_idx + 1]
                data = json.loads(json_str)
                
                # 验证 JSON 结构
                if not isinstance(data, dict) or "messages" not in data:
                    logger.warning("Invalid response structure", response=response)
                    return None
                
                messages = data.get("messages", [])
                
                if not messages or not isinstance(messages, list):
                    logger.warning("Invalid or empty messages", response=response)
                    return None
                
                # 创建建议对象
                recommendation = {
                    "content": "\n".join(messages),
                    "slot": slot,
                    "context": {
                        "generated_at": datetime.utcnow().isoformat(),
                        "response": response
                    }
                }
                
                return recommendation
                
            else:
                logger.warning("No JSON found in response", response=response)
                return None
                
        except json.JSONDecodeError as e:
            logger.warning("JSON decode error", error=str(e), response=response)
            return None
            
        except Exception as e:
            logger.exception("Error parsing response", error=str(e), response=response)
            return None
    
    def _get_fallback_recommendation(self, slot: Dict[str, Any]) -> Dict[str, Any]:
        """
        获取回退建议
        当 LLM 生成失败时使用预定义模板
        
        Args:
            slot: 槽位信息
            
        Returns:
            Dict[str, Any]: 回退建议
        """
        slot_type = slot.get("type", "unknown")
        reason = slot.get("reason", "")
        action = slot.get("action", "")
        
        # 根据槽位类型选择不同的模板
        if slot_type == "break":
            content = f"提醒：{reason}。建议您{action}。"
        elif slot_type == "hydration":
            content = f"提醒：{reason}。建议您及时补充水分，保持身体水分平衡。"
        elif slot_type == "posture":
            content = f"提醒：{reason}。请注意保持正确坐姿，避免长时间保持同一姿势。"
        elif slot_type == "environment":
            content = f"提醒：{reason}。请调整环境，创造更舒适的工作条件。"
        else:
            content = f"健康提醒：{reason}。{action}"
        
        return {
            "content": content,
            "slot": slot,
            "context": {
                "generated_at": datetime.utcnow().isoformat(),
                "is_fallback": True
            }
        }
