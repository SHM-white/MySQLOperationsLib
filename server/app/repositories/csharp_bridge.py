"""Python 与 C# RepositoriesCore 交互桥接层

说明:
1. 先用 `dotnet build` 编译 `RepositoriesCore.csproj` 生成 DLL
   典型输出: server/app/repositories/RepositoriesCore/bin/Release/net8.0/RepositoriesCore.dll
2. 安装 pythonnet (保证 Python 与 .NET 同为 64 位):
   pip install pythonnet
3. 运行时加载 DLL, 获取 C# 类并用 asyncio.to_thread 在 FastAPI/异步环境中避免阻塞事件循环。

可选替代方案（视团队需求选择）:
- 进程边界: 把 C# 项目包装成 gRPC / class EmployeesRepositoryBridge(BaseRepositoryBridge):
    """员工仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("Employees", connection_string)服务，由 Python 调用。(隔离、部署清晰)
- 子进程 CLI: C# 输出 JSON; Python subprocess 调用。(简单但性能较低)
- 反向: 用 .NET 8 + pythonnet 在 C# 宿主里托管 Python。(复杂度高; 不推荐当前场景)

本文件实现仓库包装器: EmployeesRepositoryBridge, ActivityLogsRepositoryBridge
支持标准CRUD操作及特定业务查询方法。
"""
from __future__ import annotations
import os
import sys
import asyncio
from typing import Any, Dict, List, Optional, Union
from uuid import UUID
from datetime import datetime
import json

# 导入配置
try:
    from .csharp_config import config as default_config
except ImportError:
    from csharp_config import config as default_config

# 延迟导入 pythonnet, 以便在未安装时给出友好提示
_CLR_IMPORTED = False

def _ensure_clr():
    global _CLR_IMPORTED
    if _CLR_IMPORTED:
        return
    try:
        import clr  # type: ignore  # noqa: F401
        _CLR_IMPORTED = True
    except ModuleNotFoundError as e:
        raise RuntimeError("需要先安装 pythonnet: pip install pythonnet") from e


def _probe_assembly_path() -> str:
    """寻找 RepositoriesCore.dll 的最可能路径."""
    base_dir = os.path.dirname(__file__)
    proj_dir = os.path.join(base_dir, "RepositoriesCore")
    candidates: List[str] = []
    # 优先查找 net9.0, 次选 net6.0, net8.0
    frameworks = ["net9.0", "net6.0", "net8.0"]
    for cfg in ("Release", "Debug"):
        for fw in frameworks:
            candidates.append(
                os.path.join(proj_dir, "bin", cfg, fw, "RepositoriesCore.dll")
            )
    # 允许通过环境变量覆盖
    env_path = os.environ.get("REPOSITORIES_CORE_DLL")
    if env_path:
        candidates.insert(0, env_path)
    for p in candidates:
        if os.path.exists(p):
            return p
    raise FileNotFoundError(
        "未找到 RepositoriesCore.dll, 请先在 RepositoriesCore 目录执行: dotnet build -c Release"
    )

_ASSEMBLY_LOADED = False

def load_csharp_assembly(force: bool = False) -> None:
    """加载 C# 程序集。

    Args:
        force: 是否强制重新加载 (通常不需要)
    """
    global _ASSEMBLY_LOADED
    if _ASSEMBLY_LOADED and not force:
        return
    _ensure_clr()
    
    # 初始化pythonnet
    try:
        import pythonnet  # type: ignore
        pythonnet.load("coreclr")  # pythonnet >=3
    except Exception:
        pass
    
    import clr  # type: ignore
    dll_path = _probe_assembly_path()
    dll_dir = os.path.dirname(dll_path)
    
    # 将 DLL 目录放在最前，避免同名源目录遮蔽
    if dll_dir in sys.path:
        sys.path.remove(dll_dir)
    sys.path.insert(0, dll_dir)
    
    # 清除已有同名模块缓存
    if 'RepositoriesCore' in sys.modules:
        del sys.modules['RepositoriesCore']
    
    # 直接按路径引用，避免名字解析失败
    try:
        clr.AddReference(dll_path)  # type: ignore
    except Exception as e:
        raise RuntimeError(f"clr.AddReference 失败: {dll_path}\n{e}") from e
    _ASSEMBLY_LOADED = True

# 类型提示占位 (实际运行后再导入 C# 命名空间)
RepositoriesCore = Any  # type: ignore

class BaseRepositoryBridge:
    """基础仓储桥接类，提供通用的C#仓储操作"""
    
    def __init__(self, repository_type: str, connection_string: Optional[str] = None):
        load_csharp_assembly()
        
        # 使用工厂静态类创建仓储实例
        try:
            from System import AppDomain  # type: ignore
            
            target_factory_name = 'RepositoriesCore.RepositoriesFactory'
            factory_type = None
            loaded_assemblies = list(AppDomain.CurrentDomain.GetAssemblies())
            
            # 查找工厂类型
            for asm in loaded_assemblies:
                try:
                    factory_type = asm.GetType(target_factory_name, False)
                    if factory_type is not None:
                        self._assembly = asm
                        break
                except Exception:
                    continue
                    
            if factory_type is None:
                # 枚举所有 RepositoriesCore.* 类型 (仅诊断)
                visible = []
                for asm in loaded_assemblies:
                    try:
                        for t in asm.GetTypes():
                            name = getattr(t, 'FullName', None)
                            if name and name.startswith('RepositoriesCore.'):
                                visible.append(name)
                    except Exception:
                        pass
                visible_fmt = visible or []
                raise RuntimeError(
                    f'未找到工厂类型 {target_factory_name} (反射模式)。\n'
                    f'已加载程序集数量: {len(loaded_assemblies)}\n'
                    f'可见 RepositoriesCore.* 类型: {visible_fmt}\n'
                    '请检查 DLL 是否正确编译及加载。'
                )
            
            # 根据仓储类型调用相应的工厂方法
            factory_method_name = f'Create{repository_type}Repository'
            factory_method = factory_type.GetMethod(factory_method_name)
            
            if factory_method is None:
                available_methods = [m.Name for m in factory_type.GetMethods() if m.IsStatic and m.IsPublic]
                raise RuntimeError(
                    f'未找到工厂方法 {factory_method_name}。\n'
                    f'可用的静态方法: {available_methods}'
                )
            
            # 创建仓储实例
            conn_str = connection_string or default_config.connection_string
            self._repo = factory_method.Invoke(None, [conn_str])
            self._repository_type = repository_type
            
        except Exception as e:
            raise RuntimeError(f'通过工厂类创建 {repository_type} 仓储实例失败: {e}') from e

    # ---------------- 通用同步底层封装 ----------------
    def _initialize_database_sync(self) -> bool:
        """初始化数据库表结构"""
        try:
            task = self._repo.InitializeDatabaseAsync(self._repo.databaseDefinition)
            # 同步等待异步任务完成
            import System.Threading.Tasks  # type: ignore
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _database_is_initialized_sync(self) -> bool:
        """检查数据库是否已初始化"""
        try:
            task = self._repo.DatabaseIsInitializedAsync()
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _create_record_sync(self, record_dict: Dict[str, Any]) -> bool:
        """创建单条记录"""
        try:
            task = self._repo.CreateRecordAsync(record_dict)
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _create_records_sync(self, records: List[Dict[str, Any]]) -> bool:
        """批量创建记录"""
        try:
            task = self._repo.CreateRecordsAsync(records)
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _read_record_sync(self, uuid: str) -> Optional[Dict[str, Any]]:
        """读取单条记录"""
        try:
            task = self._repo.ReadRecordAsync(uuid)
            task.Wait()
            result = task.Result
            return self._convert_to_dict(result) if result else None
        except Exception:
            return None

    def _read_records_sync(self, uuids: List[str]) -> List[Dict[str, Any]]:
        """批量读取记录"""
        try:
            task = self._repo.ReadRecordsAsync(uuids)
            task.Wait()
            results = task.Result
            return [self._convert_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _update_record_sync(self, uuid: str, record_dict: Dict[str, Any]) -> bool:
        """更新记录"""
        try:
            task = self._repo.UpdateRecordAsync(uuid, record_dict)
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _delete_record_sync(self, uuid: str) -> bool:
        """删除单条记录"""
        try:
            task = self._repo.DeleteRecordAsync(uuid)
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _delete_records_sync(self, uuids: List[str]) -> bool:
        """批量删除记录"""
        try:
            task = self._repo.DeleteRecordsAsync(uuids)
            task.Wait()
            return task.Result
        except Exception:
            return False

    def _search_records_sync(self, column: str, value: Any) -> List[Dict[str, Any]]:
        """按列搜索记录"""
        try:
            task = self._repo.SearchRecordsAsync(column, str(value))
            task.Wait()
            results = task.Result
            return [self._convert_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _list_all_records_sync(self) -> List[Dict[str, Any]]:
        """列出所有记录"""
        try:
            task = self._repo.ListAllRecordsAsync()
            task.Wait()
            results = task.Result
            return [self._convert_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    # ---------------- 异步公开 API ----------------
    async def initialize_database(self) -> bool:
        """初始化数据库表结构"""
        return await asyncio.to_thread(self._initialize_database_sync)

    async def database_is_initialized(self) -> bool:
        """检查数据库是否已初始化"""
        return await asyncio.to_thread(self._database_is_initialized_sync)

    async def create_record(self, record_dict: Dict[str, Any]) -> bool:
        """创建单条记录"""
        return await asyncio.to_thread(self._create_record_sync, record_dict)

    async def create_records(self, records: List[Dict[str, Any]]) -> bool:
        """批量创建记录"""
        return await asyncio.to_thread(self._create_records_sync, records)

    async def read_record(self, uuid: str) -> Optional[Dict[str, Any]]:
        """读取单条记录"""
        return await asyncio.to_thread(self._read_record_sync, uuid)

    async def read_records(self, uuids: List[str]) -> List[Dict[str, Any]]:
        """批量读取记录"""
        return await asyncio.to_thread(self._read_records_sync, uuids)

    async def update_record(self, uuid: str, record_dict: Dict[str, Any]) -> bool:
        """更新记录"""
        return await asyncio.to_thread(self._update_record_sync, uuid, record_dict)

    async def delete_record(self, uuid: str) -> bool:
        """删除单条记录"""
        return await asyncio.to_thread(self._delete_record_sync, uuid)

    async def delete_records(self, uuids: List[str]) -> bool:
        """批量删除记录"""
        return await asyncio.to_thread(self._delete_records_sync, uuids)

    async def search_records(self, column: str, value: Any) -> List[Dict[str, Any]]:
        """按列搜索记录"""
        return await asyncio.to_thread(self._search_records_sync, column, value)

    async def list_all_records(self) -> List[Dict[str, Any]]:
        """列出所有记录"""
        return await asyncio.to_thread(self._list_all_records_sync)

    # ---------------- 辅助方法 ----------------
    def _convert_to_dict(self, csharp_dict) -> Dict[str, Any]:
        """将C#字典转换为Python字典"""
        if csharp_dict is None:
            return {}
        
        result = {}
        try:
            # 遍历C#字典的键值对
            for kv in csharp_dict:
                key = str(kv.Key)
                value = kv.Value
                
                # 处理DateTime类型
                if hasattr(value, 'ToString') and 'DateTime' in str(type(value)):
                    result[key] = value.ToString("yyyy-MM-dd HH:mm:ss")
                # 处理其他.NET类型
                elif value is not None:
                    result[key] = self._convert_dotnet_value(value)
                else:
                    result[key] = None
        except Exception:
            # 如果转换失败，尝试直接转换
            result = dict(csharp_dict) if csharp_dict else {}
            
        return result

    def _convert_dotnet_value(self, value) -> Any:
        """转换.NET值为Python值"""
        if value is None:
            return None
        
        value_type = str(type(value))
        
        # DateTime类型
        if 'DateTime' in value_type:
            return value.ToString("yyyy-MM-dd HH:mm:ss")
        # Guid类型
        elif 'Guid' in value_type:
            return str(value)
        # 布尔类型
        elif isinstance(value, bool):
            return value
        # 数值类型
        elif isinstance(value, (int, float)):
            return value
        # 字符串类型
        else:
            return str(value)

    async def aclose(self):
        """异步关闭资源 - 保留接口兼容性，但无需实际操作"""
        pass


class EmployeesRepositoryBridge(BaseRepositoryBridge):
    """员工仓储桥接类"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("EmployeesRepository", connection_string)

    async def search_by_user_id(self, user_id: str) -> List[Dict[str, Any]]:
        """按用户ID搜索员工记录"""
        return await self.search_records("UserId", user_id)

    async def search_by_name(self, name: str) -> List[Dict[str, Any]]:
        """按姓名搜索员工记录"""
        return await self.search_records("Name", name)

    async def search_by_department(self, department: str) -> List[Dict[str, Any]]:
        """按部门搜索员工记录"""
        return await self.search_records("Department", department)

    async def get_online_employees(self) -> List[Dict[str, Any]]:
        """获取在线员工"""
        return await self.search_records("Online", True)


class RecommendationRepositoryBridge(BaseRepositoryBridge):
    """推荐仓储桥接类"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("RecommandationRepository", connection_string)

    async def get_recommendations_by_user_id(self, user_id: str) -> List[Dict[str, Any]]:
        """按用户ID获取推荐"""
        return await asyncio.to_thread(self._get_recommendations_by_user_id_sync, user_id)

    async def get_recommendations_by_user_uuid(self, user_uuid: str) -> List[Dict[str, Any]]:
        """按用户UUID获取推荐"""
        return await asyncio.to_thread(self._get_recommendations_by_user_uuid_sync, user_uuid)

    async def get_unpushed_recommendations(self) -> List[Dict[str, Any]]:
        """获取未推送的推荐"""
        return await asyncio.to_thread(self._get_unpushed_recommendations_sync)

    async def get_pushed_recommendations(self) -> List[Dict[str, Any]]:
        """获取已推送的推荐"""
        return await asyncio.to_thread(self._get_pushed_recommendations_sync)

    async def get_recommendations_in_date_range(self, user_id: str, start_date: datetime, end_date: datetime) -> List[Dict[str, Any]]:
        """获取指定日期范围内的推荐"""
        return await asyncio.to_thread(self._get_recommendations_in_date_range_sync, user_id, start_date, end_date)

    def _get_recommendations_by_user_id_sync(self, user_id: str) -> List[Dict[str, Any]]:
        """同步获取用户推荐"""
        try:
            task = self._repo.GetRecommendationsByUserIdAsync(user_id)
            task.Wait()
            results = task.Result
            return [self._convert_recommendation_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _get_recommendations_by_user_uuid_sync(self, user_uuid: str) -> List[Dict[str, Any]]:
        """同步按用户UUID获取推荐"""
        try:
            task = self._repo.GetRecommendationsByUserUUIDAsync(user_uuid)
            task.Wait()
            results = task.Result
            return [self._convert_recommendation_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _get_unpushed_recommendations_sync(self) -> List[Dict[str, Any]]:
        """同步获取未推送推荐"""
        try:
            task = self._repo.GetUnpushedRecommendationsAsync()
            task.Wait()
            results = task.Result
            return [self._convert_recommendation_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _get_pushed_recommendations_sync(self) -> List[Dict[str, Any]]:
        """同步获取已推送推荐"""
        try:
            task = self._repo.GetPushedRecommendationsAsync()
            task.Wait()
            results = task.Result
            return [self._convert_recommendation_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _get_recommendations_in_date_range_sync(self, user_id: str, start_date: datetime, end_date: datetime) -> List[Dict[str, Any]]:
        """同步获取日期范围内的推荐"""
        try:
            # 转换Python datetime到.NET DateTime
            from System import DateTime  # type: ignore
            start_dt = DateTime(start_date.year, start_date.month, start_date.day, 
                               start_date.hour, start_date.minute, start_date.second)
            end_dt = DateTime(end_date.year, end_date.month, end_date.day,
                             end_date.hour, end_date.minute, end_date.second)
            
            task = self._repo.GetRecommendationsInDateRangeAsync(user_id, start_dt, end_dt)
            task.Wait()
            results = task.Result
            return [self._convert_recommendation_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _convert_recommendation_to_dict(self, recommendation) -> Dict[str, Any]:
        """将C#推荐记录转换为Python字典"""
        if recommendation is None:
            return {}
        
        try:
            return {
                "UUID": str(recommendation.UUID),
                "UserId": str(recommendation.UserId),
                "UserUUID": str(recommendation.UserUUID),
                "CreateTime": recommendation.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                "IsPushed": bool(recommendation.IsPushed),
                "Content": str(recommendation.Content)
            }
        except Exception:
            return {"raw": str(recommendation)}


class ActivityLogsRepositoryBridge(BaseRepositoryBridge):
    """活动日志仓储桥接类"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("ActivityLogsRepository", connection_string)

    async def get_logs_by_user_id(self, user_id: str) -> List[Dict[str, Any]]:
        """按用户ID获取活动日志"""
        return await asyncio.to_thread(self._get_logs_by_user_id_sync, user_id)

    async def get_logs_by_activity_type(self, activity_type: str) -> List[Dict[str, Any]]:
        """按活动类型获取活动日志"""
        return await asyncio.to_thread(self._get_logs_by_activity_type_sync, activity_type)

    async def get_logs_in_date_range(self, user_id: str, start_date: datetime, end_date: datetime) -> List[Dict[str, Any]]:
        """获取指定日期范围内的活动日志"""
        return await asyncio.to_thread(self._get_logs_in_date_range_sync, user_id, start_date, end_date)

    def _get_logs_by_user_id_sync(self, user_id: str) -> List[Dict[str, Any]]:
        """同步获取用户活动日志"""
        try:
            task = self._repo.GetActivityLogsByUserIdAsync(user_id)
            task.Wait()
            results = task.Result
            return [self._convert_activity_log_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _get_logs_by_activity_type_sync(self, activity_type: str) -> List[Dict[str, Any]]:
        """同步按活动类型获取日志"""
        try:
            task = self._repo.GetActivityLogsByTypeAsync(activity_type)
            task.Wait()
            results = task.Result
            return [self._convert_activity_log_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _get_logs_in_date_range_sync(self, user_id: str, start_date: datetime, end_date: datetime) -> List[Dict[str, Any]]:
        """同步获取日期范围内的日志"""
        try:
            # 转换Python datetime到.NET DateTime
            from System import DateTime  # type: ignore
            start_dt = DateTime(start_date.year, start_date.month, start_date.day, 
                               start_date.hour, start_date.minute, start_date.second)
            end_dt = DateTime(end_date.year, end_date.month, end_date.day,
                             end_date.hour, end_date.minute, end_date.second)
            
            task = self._repo.GetActivityLogsInDateRangeAsync(user_id, start_dt, end_dt)
            task.Wait()
            results = task.Result
            return [self._convert_activity_log_to_dict(r) for r in results] if results else []
        except Exception:
            return []

    def _convert_activity_log_to_dict(self, activity_log) -> Dict[str, Any]:
        """将C#活动日志记录转换为Python字典"""
        if activity_log is None:
            return {}
        
        try:
            return {
                "UUID": str(activity_log.UUID),
                "UserId": str(activity_log.UserId),
                "UserUUID": str(activity_log.UserUUID),
                "ActivityType": str(activity_log.ActivityType),
                "DetailInformation": str(activity_log.DetailInformation),
                "StartTime": activity_log.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
                "EndTime": activity_log.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                "Duration": int(activity_log.Duration),
                "CreatedAt": activity_log.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }
        except Exception:
            return {"raw": str(activity_log)}


# ---------- 可选: 适配现有 Python BaseRepository 协议 ----------
class CSharpEmployeesRepositoryAdapter:
    """将 C# EmployeesRepository 适配为简单 CRUD 接口 (演示性质)。

    create(data) 期望 data 为员工信息字典
    list_all() 返回所有员工记录
    """
    def __init__(self, connection_string: Optional[str] = None):
        self._bridge = EmployeesRepositoryBridge(connection_string)

    async def create(self, data: Dict[str, Any]):
        """创建员工记录"""
        # 确保必要字段存在
        if "UUID" not in data:
            import uuid
            data["UUID"] = str(uuid.uuid4())
        if "CreatedAt" not in data:
            data["CreatedAt"] = datetime.now()
        if "UpdatedAt" not in data:
            data["UpdatedAt"] = datetime.now()
        
        ok = await self._bridge.create_record(data)
        return {"success": ok, "uuid": data["UUID"]}

    async def get_by_id(self, id: Union[UUID, str]):
        """根据UUID获取员工记录"""
        res = await self._bridge.read_record(str(id))
        return res

    async def update(self, id: Union[UUID, str], data: Dict[str, Any]):
        """更新员工记录"""
        data["UpdatedAt"] = datetime.now()
        ok = await self._bridge.update_record(str(id), data)
        return {"success": ok}

    async def delete(self, id: Union[UUID, str]):
        """删除员工记录"""
        return await self._bridge.delete_record(str(id))

    async def list_all(self, **filters):
        """列出所有员工记录"""
        if "user_id" in filters:
            return await self._bridge.search_by_user_id(filters["user_id"])
        elif "department" in filters:
            return await self._bridge.search_by_department(filters["department"])
        elif "name" in filters:
            return await self._bridge.search_by_name(filters["name"])
        else:
            return await self._bridge.list_all_records()

    async def close(self):
        """关闭资源"""
        await self._bridge.aclose()


class CSharpActivityLogsRepositoryAdapter:
    """将 C# ActivityLogsRepository 适配为简单 CRUD 接口"""
    
    def __init__(self, connection_string: Optional[str] = None):
        self._bridge = ActivityLogsRepositoryBridge(connection_string)

    async def create(self, data: Dict[str, Any]):
        """创建活动日志记录"""
        if "UUID" not in data:
            import uuid
            data["UUID"] = str(uuid.uuid4())
        if "CreatedAt" not in data:
            data["CreatedAt"] = datetime.now()
        
        ok = await self._bridge.create_record(data)
        return {"success": ok, "uuid": data["UUID"]}

    async def get_by_id(self, id: Union[UUID, str]):
        """根据UUID获取活动日志记录"""
        return await self._bridge.read_record(str(id))

    async def get_by_user_id(self, user_id: str):
        """根据用户ID获取活动日志"""
        return await self._bridge.get_logs_by_user_id(user_id)

    async def get_by_activity_type(self, activity_type: str):
        """根据活动类型获取日志"""
        return await self._bridge.get_logs_by_activity_type(activity_type)

    async def get_in_date_range(self, user_id: str, start_date: datetime, end_date: datetime):
        """获取日期范围内的活动日志"""
        return await self._bridge.get_logs_in_date_range(user_id, start_date, end_date)

    async def update(self, id: Union[UUID, str], data: Dict[str, Any]):
        """更新活动日志记录"""
        ok = await self._bridge.update_record(str(id), data)
        return {"success": ok}

    async def delete(self, id: Union[UUID, str]):
        """删除活动日志记录"""
        return await self._bridge.delete_record(str(id))

    async def list_all(self, **filters):
        """列出所有活动日志记录"""
        if "user_id" in filters:
            return await self._bridge.get_logs_by_user_id(filters["user_id"])
        elif "activity_type" in filters:
            return await self._bridge.get_logs_by_activity_type(filters["activity_type"])
        else:
            return await self._bridge.list_all_records()

    async def close(self):
        """关闭资源"""
        await self._bridge.aclose()


class CSharpRecommendationRepositoryAdapter:
    """将 C# RecommandationRepository 适配为简单 CRUD 接口"""
    
    def __init__(self, connection_string: Optional[str] = None):
        self._bridge = RecommendationRepositoryBridge(connection_string)

    async def create(self, data: Dict[str, Any]):
        """创建推荐记录"""
        if "UUID" not in data:
            import uuid
            data["UUID"] = str(uuid.uuid4())
        if "CreateTime" not in data:
            data["CreateTime"] = datetime.now()
        if "IsPushed" not in data:
            data["IsPushed"] = False
        
        ok = await self._bridge.create_record(data)
        return {"success": ok, "uuid": data["UUID"]}

    async def get_by_id(self, id: Union[UUID, str]):
        """根据UUID获取推荐记录"""
        return await self._bridge.read_record(str(id))

    async def get_by_user_id(self, user_id: str):
        """根据用户ID获取推荐"""
        return await self._bridge.get_recommendations_by_user_id(user_id)

    async def get_by_user_uuid(self, user_uuid: str):
        """根据用户UUID获取推荐"""
        return await self._bridge.get_recommendations_by_user_uuid(user_uuid)

    async def get_unpushed(self):
        """获取未推送的推荐"""
        return await self._bridge.get_unpushed_recommendations()

    async def get_pushed(self):
        """获取已推送的推荐"""
        return await self._bridge.get_pushed_recommendations()

    async def get_in_date_range(self, user_id: str, start_date: datetime, end_date: datetime):
        """获取日期范围内的推荐"""
        return await self._bridge.get_recommendations_in_date_range(user_id, start_date, end_date)

    async def mark_as_pushed(self, id: Union[UUID, str]):
        """标记推荐为已推送"""
        return await self._bridge.update_record(str(id), {"IsPushed": True})

    async def update(self, id: Union[UUID, str], data: Dict[str, Any]):
        """更新推荐记录"""
        ok = await self._bridge.update_record(str(id), data)
        return {"success": ok}

    async def delete(self, id: Union[UUID, str]):
        """删除推荐记录"""
        return await self._bridge.delete_record(str(id))

    async def list_all(self, **filters):
        """列出所有推荐记录"""
        if "user_id" in filters:
            return await self._bridge.get_recommendations_by_user_id(filters["user_id"])
        elif "user_uuid" in filters:
            return await self._bridge.get_recommendations_by_user_uuid(filters["user_uuid"])
        elif "is_pushed" in filters:
            if filters["is_pushed"]:
                return await self._bridge.get_pushed_recommendations()
            else:
                return await self._bridge.get_unpushed_recommendations()
        else:
            return await self._bridge.list_all_records()

    async def close(self):
        """关闭资源"""
        await self._bridge.aclose()


class RepositoryFactory:
    """仓储工厂类，简化仓储实例创建"""
    
    def __init__(self, connection_string: Optional[str] = None):
        self.connection_string = connection_string or default_config.connection_string

    def create_employees_repository(self) -> EmployeesRepositoryBridge:
        """创建员工仓储"""
        return EmployeesRepositoryBridge(self.connection_string)

    def create_activity_logs_repository(self) -> ActivityLogsRepositoryBridge:
        """创建活动日志仓储"""
        return ActivityLogsRepositoryBridge(self.connection_string)

    def create_recommendation_repository(self) -> RecommendationRepositoryBridge:
        """创建推荐仓储"""
        return RecommendationRepositoryBridge(self.connection_string)

    def create_employees_adapter(self) -> CSharpEmployeesRepositoryAdapter:
        """创建员工仓储适配器"""
        return CSharpEmployeesRepositoryAdapter(self.connection_string)

    def create_activity_logs_adapter(self) -> CSharpActivityLogsRepositoryAdapter:
        """创建活动日志仓储适配器"""
        return CSharpActivityLogsRepositoryAdapter(self.connection_string)

    def create_recommendation_adapter(self) -> CSharpRecommendationRepositoryAdapter:
        """创建推荐仓储适配器"""
        return CSharpRecommendationRepositoryAdapter(self.connection_string)
    
    async def initialize_all_databases(self) -> bool:
        """初始化所有数据库表"""
        try:
            employees_repo = self.create_employees_repository()
            activity_logs_repo = self.create_activity_logs_repository()
            recommendation_repo = self.create_recommendation_repository()
            
            # 初始化员工表
            employees_init = await employees_repo.initialize_database()
            
            # 初始化活动日志表
            activity_logs_init = await activity_logs_repo.initialize_database()
            
            # 初始化推荐表
            recommendation_init = await recommendation_repo.initialize_database()
            
            # 关闭资源
            await employees_repo.aclose()
            await activity_logs_repo.aclose()
            await recommendation_repo.aclose()
            
            return employees_init and activity_logs_init and recommendation_init
        except Exception:
            return False


__all__ = [
    "load_csharp_assembly",
    "BaseRepositoryBridge",
    "EmployeesRepositoryBridge",
    "ActivityLogsRepositoryBridge",
    "RecommendationRepositoryBridge",
    "CSharpEmployeesRepositoryAdapter",
    "CSharpActivityLogsRepositoryAdapter",
    "CSharpRecommendationRepositoryAdapter",
    "RepositoryFactory",
]

if __name__ == "__main__":
    # 简单测试
    async def main():
        # 测试员工仓储
        employees_repo = EmployeesRepositoryBridge()
        
        # 初始化数据库
        await employees_repo.initialize_database()
        
        # 创建示例员工记录
        import uuid
        employee_data = {
            "UUID": str(uuid.uuid4()),
            "UserId": "alice",
            "Name": "Alice Smith",
            "Department": "IT",
            "WorkstationId": "WS001",
            "Preference": json.dumps({"theme": "dark", "notifications": True}),
            "Online": True,
            "CreatedAt": datetime.now(),
            "UpdatedAt": datetime.now()
        }
        
        create_result = await employees_repo.create_record(employee_data)
        print("Created employee:", create_result)
        
        # 通过 UserId 搜索
        alice_records = await employees_repo.search_by_user_id("alice")
        print("Alice records:", alice_records)
        
        # 更新记录
        if alice_records:
            employee_uuid = alice_records[0].get('UUID')
            if employee_uuid:
                update_data = employee_data.copy()
                update_data["Department"] = "Engineering"
                update_data["UpdatedAt"] = datetime.now()
                
                await employees_repo.update_record(employee_uuid, update_data)
                updated = await employees_repo.read_record(employee_uuid)
                print("Updated employee:", updated)
                
                # 删除记录
                await employees_repo.delete_record(employee_uuid)
                after_delete = await employees_repo.read_record(employee_uuid)
                print("After delete:", after_delete)
        
        await employees_repo.aclose()
        
        # 测试活动日志仓储
        activity_repo = ActivityLogsRepositoryBridge()
        
        # 初始化数据库
        await activity_repo.initialize_database()
        
        # 创建示例活动日志
        activity_data = {
            "UUID": str(uuid.uuid4()),
            "UserId": "alice",
            "UserUUID": str(uuid.uuid4()),
            "ActivityType": "meeting",
            "DetailInformation": json.dumps({"room": "A101", "participants": 5}),
            "StartTime": datetime.now(),
            "EndTime": datetime.now(),
            "Duration": 60,
            "CreatedAt": datetime.now()
        }
        
        create_result = await activity_repo.create_record(activity_data)
        print("Created activity log:", create_result)
        
        # 按用户ID搜索活动日志
        user_logs = await activity_repo.get_logs_by_user_id("alice")
        print("User activity logs:", user_logs)
        
        await activity_repo.aclose()

    asyncio.run(main())