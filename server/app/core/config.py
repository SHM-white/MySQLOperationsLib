from typing import Optional, Dict, Any, List
import os
from pydantic import Field, field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """
    应用配置，使用 Pydantic Settings 管理
    从环境变量或 .env 文件加载配置
    """
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    # 基础配置
    APP_ENV: str = Field("dev", description="运行环境: dev, test, prod")
    API_PORT: int = Field(8080, description="API 服务端口")
    DEBUG: bool = Field(False, description="调试模式")
    
    # 数据库配置
    DATABASE_URL: str = Field(..., description="PostgreSQL 连接 URL")
    
    # Redis 配置
    REDIS_URL: Optional[str] = Field(None, description="Redis 连接 URL")
    
    # LLM 配置
    LLM_PROVIDER: str = Field("openai", description="LLM 提供商: openai, deepseek, tongyi")
    OPENAI_API_KEY: Optional[str] = Field(None, description="OpenAI API Key")
    DEEPSEEK_API_KEY: Optional[str] = Field(None, description="DeepSeek API Key")
    TONGYI_API_KEY: Optional[str] = Field(None, description="通义 API Key")
    
    # 钉钉配置
    DINGTALK_SIGN_SECRET: Optional[str] = Field(None, description="钉钉签名密钥")
    DINGTALK_ROBOT_TOKEN: Optional[str] = Field(None, description="钉钉机器人 Token")
    
    # 规则引擎配置
    RULE_COOLDOWN_MINUTES: int = Field(30, description="规则触发冷却时间(分钟)")
    
    # 日志配置
    LOG_LEVEL: str = Field("INFO", description="日志级别")
    JSON_LOGS: bool = Field(False, description="是否输出 JSON 格式日志")
    
    @field_validator("APP_ENV")
    def validate_app_env(cls, v: str) -> str:
        if v not in ["dev", "test", "prod"]:
            raise ValueError("APP_ENV must be one of dev, test, or prod")
        return v

    @field_validator("LOG_LEVEL")
    def validate_log_level(cls, v: str) -> str:
        allowed = ["DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL"]
        if v.upper() not in allowed:
            raise ValueError(f"LOG_LEVEL must be one of {', '.join(allowed)}")
        return v.upper()
    
    @property
    def is_development(self) -> bool:
        """是否为开发环境"""
        return self.APP_ENV == "dev"
    
    @property
    def is_production(self) -> bool:
        """是否为生产环境"""
        return self.APP_ENV == "prod"


# 创建全局配置单例
settings = Settings()
