from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional, Protocol, TypedDict

import structlog

logger = structlog.get_logger(__name__)


class ChatMessage(TypedDict):
    """聊天消息类型"""
    role: str
    content: str


class LLMClient(Protocol):
    """
    LLM 客户端接口
    定义与大语言模型交互的通用接口
    """
    
    @abstractmethod
    async def chat(
        self, 
        messages: List[ChatMessage], 
        model: str, 
        temperature: float = 0.7, 
        max_tokens: int = 512
    ) -> str:
        """
        与 LLM 进行对话
        
        Args:
            messages: 消息列表
            model: 模型名称
            temperature: 生成温度
            max_tokens: 最大 token 数
            
        Returns:
            str: LLM 响应内容
        """
        pass


class BaseLLMClient(ABC):
    """
    基础 LLM 客户端
    实现通用功能和错误处理
    """
    
    def __init__(self):
        self.provider_name = self.__class__.__name__
    
    @abstractmethod
    async def chat(
        self, 
        messages: List[ChatMessage], 
        model: str, 
        temperature: float = 0.7, 
        max_tokens: int = 512
    ) -> str:
        """
        与 LLM 进行对话
        
        Args:
            messages: 消息列表
            model: 模型名称
            temperature: 生成温度
            max_tokens: 最大 token 数
            
        Returns:
            str: LLM 响应内容
        """
        pass
    
    def format_messages(self, messages: List[ChatMessage]) -> List[ChatMessage]:
        """
        格式化消息
        确保消息格式正确
        
        Args:
            messages: 原始消息列表
            
        Returns:
            List[ChatMessage]: 格式化后的消息列表
        """
        # 确保第一条消息是系统消息
        if not messages or messages[0]["role"] != "system":
            system_message: ChatMessage = {
                "role": "system",
                "content": "你是办公健康助手，只能使用提供的事实，禁止医学诊断。"
            }
            messages = [system_message] + messages
        
        return messages
    
    def validate_response(self, response: str) -> bool:
        """
        验证 LLM 响应
        检查响应是否合法
        
        Args:
            response: LLM 响应内容
            
        Returns:
            bool: 响应是否合法
        """
        # 判断响应是否为空
        if not response or not response.strip():
            logger.warning("Empty LLM response")
            return False
        
        # 长度限制
        if len(response) > 2000:
            logger.warning("LLM response too long", length=len(response))
            return False
        
        # TODO: 可以添加更多验证，如敏感词过滤
        
        return True
