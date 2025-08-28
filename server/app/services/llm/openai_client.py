import json
from typing import List, Dict, Any, Optional

import openai
import structlog
from openai import AsyncOpenAI
from prometheus_client import Counter, Histogram

from app.core.config import settings
from app.services.llm.base import BaseLLMClient, ChatMessage

logger = structlog.get_logger(__name__)

# 指标
llm_calls_counter = Counter(
    "hg_llm_calls_total", 
    "Total number of LLM API calls",
    ["provider", "model", "status"]
)
llm_duration = Histogram(
    "hg_llm_duration_seconds", 
    "Duration of LLM API calls in seconds",
    ["provider", "model"]
)
llm_tokens = Counter(
    "hg_llm_tokens_total", 
    "Total number of tokens used in LLM API calls",
    ["provider", "model", "type"]
)


class OpenAIClient(BaseLLMClient):
    """
    OpenAI API 客户端
    """
    
    def __init__(self):
        super().__init__()
        self.api_key = settings.OPENAI_API_KEY
        self.client = AsyncOpenAI(api_key=self.api_key)
        self.default_model = "gpt-3.5-turbo"
    
    async def chat(
        self, 
        messages: List[ChatMessage], 
        model: Optional[str] = None, 
        temperature: float = 0.7, 
        max_tokens: int = 512
    ) -> str:
        """
        使用 OpenAI API 进行对话
        
        Args:
            messages: 消息列表
            model: 模型名称，默认为 gpt-3.5-turbo
            temperature: 生成温度
            max_tokens: 最大 token 数
            
        Returns:
            str: LLM 响应内容
        """
        if not self.api_key:
            logger.error("OpenAI API key not configured")
            return "Error: OpenAI API key not configured"
        
        # 使用默认模型
        if not model:
            model = self.default_model
        
        # 格式化消息
        formatted_messages = self.format_messages(messages)
        
        try:
            # 调用 OpenAI API
            with llm_duration.labels(provider="openai", model=model).time():
                response = await self.client.chat.completions.create(
                    model=model,
                    messages=[dict(m) for m in formatted_messages],
                    temperature=temperature,
                    max_tokens=max_tokens
                )
            
            # 记录指标
            llm_calls_counter.labels(
                provider="openai", 
                model=model, 
                status="success"
            ).inc()
            
            # 记录 token 使用情况
            if hasattr(response, "usage") and response.usage:
                llm_tokens.labels(
                    provider="openai", 
                    model=model, 
                    type="prompt"
                ).inc(response.usage.prompt_tokens)
                
                llm_tokens.labels(
                    provider="openai", 
                    model=model, 
                    type="completion"
                ).inc(response.usage.completion_tokens)
            
            # 提取响应文本
            response_text: str = response.choices[0].message.content if response.choices[0].message.content != None else ""
            # 验证响应
            if not self.validate_response(response_text):
                logger.warning("Invalid LLM response", response=response_text[:100])
                return "Error: Invalid LLM response"
            
            return response_text
            
        except openai.APIError as e:
            logger.error("OpenAI API error", error=str(e))
            llm_calls_counter.labels(
                provider="openai", 
                model=model, 
                status="error"
            ).inc()
            return f"Error: {str(e)}"
        
        except Exception as e:
            logger.exception("Unexpected error in OpenAI client", error=str(e))
            llm_calls_counter.labels(
                provider="openai", 
                model=model, 
                status="error"
            ).inc()
            return f"Error: {str(e)}"
