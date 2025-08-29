import logging
from contextlib import asynccontextmanager

import structlog
from fastapi import FastAPI, Request, Depends
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from app.api.routes import health
from app.core.config import settings
from app.core.logging import setup_logging
from app.core.metrics import setup_metrics

# 设置日志
setup_logging()
logger = structlog.get_logger(__name__)

# 使用 lifespan 事件替代 @app.on_event
@asynccontextmanager
async def lifespan(app: FastAPI):
    logger.info("Starting Health Guardian API", environment=settings.APP_ENV)
    # 暂时禁用调度器
    # scheduler_service.start()
    yield
    logger.info("Shutting down Health Guardian API")


# 创建 FastAPI 应用
app = FastAPI(
    title="Health Guardian API",
    description="健康守护者 API",
    version="0.1.0",
    docs_url="/docs" if not settings.is_production else None,
    redoc_url="/redoc" if not settings.is_production else None,
    lifespan=lifespan,
)

# 添加 CORS 中间件
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # 在生产环境中应该限制来源
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 设置 Prometheus 指标
setup_metrics(app)

# 注册路由
app.include_router(health.router)


# 全局异常处理
@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    logger.exception("Unhandled exception", path=request.url.path, error=str(exc))
    return JSONResponse(
        status_code=500,
        content={
            "status": "error",
            "message": "Internal server error",
            "detail": str(exc) if settings.DEBUG else "An unexpected error occurred"
        },
    )


# 入口点
if __name__ == "__main__":
    import uvicorn
    
    uvicorn.run(
        "app.main:app",
        host="0.0.0.0",
        port=settings.API_PORT,
        reload=settings.is_development,
        log_level=settings.LOG_LEVEL.lower(),
    )
