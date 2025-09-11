"""C# 仓储配置文件

管理C#仓储的数据库连接字符串和其他配置项。
"""
import os
from typing import Optional

class CSharpRepositoryConfig:
    """C#仓储配置类"""
    
    def __init__(self):
        self._connection_string: Optional[str] = None
        self._load_from_env()
    
    def _load_from_env(self):
        """从环境变量加载配置"""
        # 优先从环境变量获取MySQL连接字符串
        mysql_host = os.getenv("MYSQL_HOST", "localhost")
        mysql_port = os.getenv("MYSQL_PORT", "3306")
        mysql_user = os.getenv("MYSQL_USER", "root")
        mysql_password = os.getenv("MYSQL_PASSWORD", "")
        mysql_database = os.getenv("MYSQL_DATABASE", "health_guardian")
        
        # 构建MySQL连接字符串
        if mysql_password:
            self._connection_string = (
                f"Server={mysql_host};Port={mysql_port};"
                f"Database={mysql_database};Uid={mysql_user};"
                f"Pwd={mysql_password};CharSet=utf8mb4;"
                f"SslMode=None;AllowPublicKeyRetrieval=True;"
            )
        else:
            self._connection_string = (
                f"Server={mysql_host};Port={mysql_port};"
                f"Database={mysql_database};Uid={mysql_user};"
                f"CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True;"
            )
    
    @property
    def connection_string(self) -> Optional[str]:
        """获取数据库连接字符串"""
        return self._connection_string
    
    @connection_string.setter
    def connection_string(self, value: Optional[str]):
        """设置数据库连接字符串"""
        self._connection_string = value
    
    def get_mysql_connection_string(
        self, 
        host: str = "localhost", 
        port: int = 3306,
        database: str = "health_guardian",
        username: str = "root",
        password: str = ""
    ) -> str:
        """构建MySQL连接字符串"""
        if password:
            return (
                f"Server={host};Port={port};"
                f"Database={database};Uid={username};"
                f"Pwd={password};CharSet=utf8mb4;"
                f"SslMode=None;AllowPublicKeyRetrieval=True;"
            )
        else:
            return (
                f"Server={host};Port={port};"
                f"Database={database};Uid={username};"
                f"CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True;"
            )
    
    def get_test_connection_string(self) -> str:
        """获取测试用的连接字符串（使用内存数据库或测试数据库）"""
        # 对于测试，可以使用一个专门的测试数据库
        return self.get_mysql_connection_string(
            database="health_guardian_test"
        )

# 全局配置实例
config = CSharpRepositoryConfig()
