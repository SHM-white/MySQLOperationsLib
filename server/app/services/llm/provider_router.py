from typing import Dict, List, Optional, Any

import structlog

from app.core.config import settings
from app.services.llm.base import LLMClient, ChatMessage
from app.services.llm.openai_client import OpenAIClient

logger = structlog.get_logger(__name__)


class LLMProviderRouter:
    """
    LLM 提供商路由
    管理不同的 LLM 提供商客户端，并处理失败回退
    """
    
    def __init__(self):
        self.providers: Dict[str, LLMClient] = {}
        self.primary_provider = settings.LLM_PROVIDER
        self.fallback_order = ["openai", "deepseek", "tongyi"]
        
        # 初始化提供商
        self._init_providers()
    
    def _init_providers(self) -> None:
        """初始化 LLM 提供商"""
        # 初始化 OpenAI 客户端
        if settings.OPENAI_API_KEY:
            self.providers["openai"] = OpenAIClient()
        
        # TODO: 初始化其他客户端，如 DeepSeek、通义等
        # 示例：
        # if settings.DEEPSEEK_API_KEY:
        #     self.providers["deepseek"] = DeepSeekClient()
        # 
        # if settings.TONGYI_API_KEY:
        #     self.providers["tongyi"] = TongyiClient()
    
    async def chat(
        self, 
        messages: List[ChatMessage], 
        model: Optional[str] = None, 
        temperature: float = 0.7, 
        max_tokens: int = 512,
        use_fallback: bool = True
    ) -> str:
        """
        使用配置的 LLM 提供商进行对话
        支持失败回退到其他提供商
        
        Args:
            messages: 消息列表
            model: 模型名称
            temperature: 生成温度
            max_tokens: 最大 token 数
            use_fallback: 是否使用失败回退
            
        Returns:
            str: LLM 响应内容
        """
        # 检查主要提供商是否可用
        if self.primary_provider not in self.providers:
            logger.warning(
                f"Primary provider {self.primary_provider} not available, "
                f"trying fallbacks"
            )
            if not use_fallback or not self.providers:
                return "Error: No LLM provider available"
        else:
            # 尝试使用主要提供商
            try:
                client = self.providers[self.primary_provider]
                response = await client.chat(
                    messages=messages,
                    model=model,
                    temperature=temperature,
                    max_tokens=max_tokens
                )
                
                # 检查响应是否为错误
                if not response.startswith("Error:"):
                    return response
                    
                logger.warning(
                    "Primary provider failed", 
                    provider=self.primary_provider, 
                    error=response
                )
                
                # 如果不使用回退，则直接返回错误
                if not use_fallback:
                    return response
                    
            except Exception as e:
                logger.exception(
                    "Error with primary provider", 
                    provider=self.primary_provider, 
                    error=str(e)
                )
                
                # 如果不使用回退，则直接返回错误
                if not use_fallback:
                    return f"Error: {str(e)}"
        
        # 尝试回退提供商
        for provider_name in self.fallback_order:
            # 跳过主要提供商和不可用的提供商
            if provider_name == self.primary_provider or provider_name not in self.providers:
                continue
                
            logger.info(f"Trying fallback provider: {provider_name}")
            
            try:
                client = self.providers[provider_name]
                response = await client.chat(
                    messages=messages,
                    model=model,
                    temperature=temperature,
                    max_tokens=max_tokens
                )
                
                # 检查响应是否为错误
                if not response.startswith("Error:"):
                    return response
                    
                logger.warning(
                    "Fallback provider failed", 
                    provider=provider_name, 
                    error=response
                )
                
            except Exception as e:
                logger.exception(
                    "Error with fallback provider", 
                    provider=provider_name, 
                    error=str(e)
                )
        
        # 所有提供商都失败
        return "Error: All LLM providers failed"


# 创建全局实例
llm_router = LLMProviderRouter()
