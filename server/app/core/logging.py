import logging
import sys
from typing import Dict, Any

import structlog
from structlog.types import Processor

from app.core.config import settings


def setup_logging() -> None:
    """
    配置结构化日志系统
    支持开发时人类可读格式和生产环境的 JSON 格式
    """
    # 设置日志级别
    log_level = logging.getLevelName(settings.LOG_LEVEL)
    
    # 基础处理器配置
    processors: list[Processor] = [
        # 添加日志级别名称
        structlog.stdlib.add_log_level,
        # 添加调用者信息
        structlog.processors.CallsiteParameterAdder(
            [
                structlog.processors.CallsiteParameter.FILENAME,
                structlog.processors.CallsiteParameter.FUNC_NAME,
                structlog.processors.CallsiteParameter.LINENO,
            ]
        ),
        # 添加时间戳
        structlog.processors.TimeStamper(fmt="%Y-%m-%d %H:%M:%S"),
        # 添加环境和版本信息
        structlog.processors.StackInfoRenderer(),
        structlog.processors.format_exc_info,
    ]
    
    # 区分开发环境和生产环境的日志格式
    if settings.JSON_LOGS or settings.is_production:
        # 生产环境使用 JSON 格式
        processors.extend([
            structlog.processors.dict_tracebacks,
            structlog.processors.JSONRenderer()
        ])
    else:
        # 开发环境使用彩色控制台输出
        processors.extend([
            structlog.dev.ConsoleRenderer(
                colors=True,
                exception_formatter=structlog.dev.rich_traceback
            )
        ])
    
    # 配置 structlog
    structlog.configure(
        processors=processors,
        logger_factory=structlog.stdlib.LoggerFactory(),
        wrapper_class=structlog.stdlib.BoundLogger,
        cache_logger_on_first_use=True,
    )

    # 配置 Python 标准日志
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=log_level,
    )
    
    # 降低第三方库日志级别，减少噪音
    for logger_name in ["uvicorn", "uvicorn.error", "fastapi"]:
        logging.getLogger(logger_name).setLevel(log_level)
    
    # 设置其他库的日志级别，避免过多日志
    for noisy_logger in ["httpx", "asyncio", "sqlalchemy.engine.Engine"]:
        logging.getLogger(noisy_logger).setLevel(logging.WARNING)
