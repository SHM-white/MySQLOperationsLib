from fastapi import APIRouter, Depends
from pydantic import BaseModel
from typing import Dict, Any, Optional

from app.core.config import settings
from app.db.session import get_db

router = APIRouter(tags=["健康检查"])


class HealthResponse(BaseModel):
    """健康检查响应模型"""
    status: str
    version: str
    environment: str
    database: Optional[str] = None
    details: Optional[Dict[str, Any]] = None


@router.get("/healthz", response_model=HealthResponse)
async def health_check():
    """
    健康检查端点
    
    返回服务状态和环境信息，用于监控和检查部署
    """
    return {
        "status": "ok",
        "version": "0.1.0",  # 版本信息可以从环境变量或配置中获取
        "environment": settings.APP_ENV,
        "details": {
            "debug_mode": settings.DEBUG
        }
    }


@router.get("/healthz/db", response_model=HealthResponse)
async def database_health_check(db=Depends(get_db)):
    """
    数据库连接健康检查
    
    检查数据库连接是否正常
    """
    try:
        # 执行简单查询检查数据库连接
        result = await db.execute("SELECT 1")
        db_status = "ok" if result else "error"
        
        return {
            "status": "ok" if db_status == "ok" else "error",
            "version": "0.1.0",
            "environment": settings.APP_ENV,
            "database": db_status,
            "details": {
                "debug_mode": settings.DEBUG
            }
        }
        
    except Exception as e:
        return {
            "status": "error",
            "version": "0.1.0",
            "environment": settings.APP_ENV,
            "database": "error",
            "details": {
                "debug_mode": settings.DEBUG,
                "error": str(e)
            }
        }
