from typing import AsyncGenerator, Optional, Protocol, Any
import logging
from contextlib import asynccontextmanager

# 创建日志记录器
logger = logging.getLogger(__name__)


class AsyncSessionProtocol(Protocol):
    """数据库会话协议，定义数据库会话应该具有的方法"""
    
    async def commit(self) -> None:
        """提交事务"""
        ...
    
    async def rollback(self) -> None:
        """回滚事务"""
        ...
    
    async def close(self) -> None:
        """关闭会话"""
        ...
    
    async def execute(self, statement: Any, *args, **kwargs) -> Any:
        """执行SQL语句"""
        ...
    
    async def scalar(self, statement: Any, *args, **kwargs) -> Any:
        """执行SQL语句并返回第一个结果的第一列"""
        ...


class MockSession:
    """模拟数据库会话，用于开发和测试"""
    
    async def commit(self) -> None:
        logger.debug("Mock: commit called")
    
    async def rollback(self) -> None:
        logger.debug("Mock: rollback called")
    
    async def close(self) -> None:
        logger.debug("Mock: close called")
    
    async def execute(self, statement: Any, *args, **kwargs) -> Any:
        logger.debug(f"Mock: execute called with {statement}")
        return []
    
    async def scalar(self, statement: Any, *args, **kwargs) -> Any:
        logger.debug(f"Mock: scalar called with {statement}")
        return None


@asynccontextmanager
async def get_session() -> AsyncGenerator[AsyncSessionProtocol, None]:
    """
    获取数据库会话的上下文管理器
    
    示例:
    ```
    async with get_session() as session:
        # 使用session操作数据库
    ```
    """
    session = MockSession()
    try:
        yield session
        await session.commit()
    except Exception as e:
        await session.rollback()
        logger.error(f"数据库会话错误: {str(e)}")
        raise
    finally:
        await session.close()


async def get_db() -> AsyncGenerator[AsyncSessionProtocol, None]:
    """
    数据库会话依赖，用于FastAPI依赖注入
    
    示例:
    ```
    @app.get("/items/")
    async def read_items(db: AsyncSessionProtocol = Depends(get_db)):
        # 使用db操作数据库
    ```
    """
    session = MockSession()
    try:
        yield session
        await session.commit()
    except Exception:
        await session.rollback()
        raise
    finally:
        await session.close()
