from typing import Dict, Any, Optional, List, Union, Type

from fastapi import Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import ValidationError as PydanticValidationError

import structlog

logger = structlog.get_logger(__name__)


class BaseAPIException(Exception):
    """
    API 异常基类
    """
    def __init__(
        self, 
        status_code: int, 
        message: str, 
        detail: Optional[Union[str, Dict[str, Any], List[Dict[str, Any]]]] = None
    ):
        self.status_code = status_code
        self.message = message
        self.detail = detail
        super().__init__(message)


class NotFoundError(BaseAPIException):
    """
    资源未找到异常
    """
    def __init__(
        self, 
        message: str = "Resource not found", 
        detail: Optional[Union[str, Dict[str, Any]]] = None
    ):
        super().__init__(status_code=404, message=message, detail=detail)


class ValidationError(BaseAPIException):
    """
    数据验证异常
    """
    def __init__(
        self, 
        message: str = "Validation error", 
        detail: Optional[Union[str, Dict[str, Any], List[Dict[str, Any]]]] = None
    ):
        super().__init__(status_code=422, message=message, detail=detail)


class AuthenticationError(BaseAPIException):
    """
    认证异常
    """
    def __init__(
        self, 
        message: str = "Authentication error", 
        detail: Optional[Union[str, Dict[str, Any]]] = None
    ):
        super().__init__(status_code=401, message=message, detail=detail)


class PermissionDeniedError(BaseAPIException):
    """
    权限异常
    """
    def __init__(
        self, 
        message: str = "Permission denied", 
        detail: Optional[Union[str, Dict[str, Any]]] = None
    ):
        super().__init__(status_code=403, message=message, detail=detail)


class ServerError(BaseAPIException):
    """
    服务器异常
    """
    def __init__(
        self, 
        message: str = "Internal server error", 
        detail: Optional[Union[str, Dict[str, Any]]] = None
    ):
        super().__init__(status_code=500, message=message, detail=detail)


class BadRequestError(BaseAPIException):
    """
    错误请求异常
    """
    def __init__(
        self, 
        message: str = "Bad request", 
        detail: Optional[Union[str, Dict[str, Any]]] = None
    ):
        super().__init__(status_code=400, message=message, detail=detail)


def add_exception_handlers(app):
    """
    添加异常处理器
    
    Args:
        app: FastAPI 应用
    """
    # 处理自定义 API 异常
    @app.exception_handler(BaseAPIException)
    async def api_exception_handler(request: Request, exc: BaseAPIException):
        logger.warning(
            "API exception", 
            status_code=exc.status_code, 
            message=exc.message, 
            detail=exc.detail,
            path=request.url.path
        )
        
        return JSONResponse(
            status_code=exc.status_code,
            content={
                "status": "error",
                "message": exc.message,
                "detail": exc.detail
            }
        )
    
    # 处理请求验证异常
    @app.exception_handler(RequestValidationError)
    async def validation_exception_handler(request: Request, exc: RequestValidationError):
        logger.warning(
            "Validation error", 
            errors=exc.errors(), 
            body=exc.body,
            path=request.url.path
        )
        
        return JSONResponse(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            content={
                "status": "error",
                "message": "Validation error",
                "detail": exc.errors()
            }
        )
    
    # 处理 Pydantic 验证异常
    @app.exception_handler(PydanticValidationError)
    async def pydantic_validation_exception_handler(request: Request, exc: PydanticValidationError):
        logger.warning(
            "Pydantic validation error", 
            errors=getattr(exc, 'errors', None),  # 使用 getattr 以防止属性不存在
            path=request.url.path
        )
        
        return JSONResponse(
            status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
            content={
                "status": "error",
                "message": "Validation error",
                "detail": getattr(exc, 'errors', None)  # 使用 getattr 获取属性
            }
        )
