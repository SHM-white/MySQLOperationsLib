"""Python 与 C# RepositoriesCore 交互桥接层

说明:
1. 先用 `dotnet build` 编译 `RepositoriesCore.csproj` 生成 DLL
   典型输出: server/app/repositories/RepositoriesCore/bin/Release/net9.0/RepositoriesCore.dll
2. 安装 pythonnet (保证 Python 与 .NET 同为 64 位):
   pip install pythonnet
3. 运行时加载 DLL, 获取 C# 类并用 asyncio.to_thread 在 FastAPI/异步环境中避免阻塞事件循环。

可选替代方案（视团队需求选择）:
- 进程边界: 把 C# 项目包装成 gRPC / REST 服务，由 Python 调用。(隔离、部署清晰)
- 子进程 CLI: C# 输出 JSON; Python subprocess 调用。(简单但性能较低)
- 反向: 用 .NET 9 + pythonnet 在 C# 宿主里托管 Python。(复杂度高; 不推荐当前场景)

本文件实现仓库包装器: EmployeesRepositoryBridge, ActivityLogsRepositoryBridge, RecommendationRepositoryBridge
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
    """基础仓储桥接类，通过工厂静态类提供通用的C#仓储操作"""
    
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
                    f'未找到工厂类型 {target_factory_name}。\n'
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
        except Exception as e:
            raise RuntimeError(f"初始化数据库失败: {e}") from e

    def _add_records_sync(self, records: List[Dict[str, Any]]) -> bool:
        """添加记录（同步版本）"""
        try:
            import System  # type: ignore
            # 将 Python dict 转换为 C# Dictionary<string, object?>[]
            record_array = System.Array.CreateInstance(System.Collections.Generic.Dictionary[str, object], len(records))
            for i, record in enumerate(records):
                cs_dict = System.Collections.Generic.Dictionary[str, object]()
                for k, v in record.items():
                    cs_dict[k] = v
                record_array[i] = cs_dict
            
            task = self._repo.AddNewRecordsAsync(record_array)
            task.Wait()
            return task.Result
        except Exception as e:
            raise RuntimeError(f"添加记录失败: {e}") from e

    def _read_records_sync(self, uuids: List[str]) -> Optional[List[str]]:
        """读取记录（同步版本）"""
        try:
            import System  # type: ignore
            uuid_array = System.Array.CreateInstance(str, len(uuids))
            for i, uuid in enumerate(uuids):
                uuid_array[i] = uuid
            
            task = self._repo.ReadRecordsAsync(uuid_array)
            task.Wait()
            result = task.Result
            return list(result) if result else None
        except Exception as e:
            raise RuntimeError(f"读取记录失败: {e}") from e

    def _update_record_sync(self, uuid: str, record: Dict[str, Any]) -> bool:
        """更新记录（同步版本）"""
        try:
            import System  # type: ignore
            cs_dict = System.Collections.Generic.Dictionary[str, object]()
            for k, v in record.items():
                cs_dict[k] = v
            
            task = self._repo.UpdateRecordAsync(uuid, cs_dict)
            task.Wait()
            return task.Result
        except Exception as e:
            raise RuntimeError(f"更新记录失败: {e}") from e

    def _delete_records_sync(self, uuids: List[str]) -> bool:
        """删除记录（同步版本）"""
        try:
            import System  # type: ignore
            uuid_array = System.Array.CreateInstance(str, len(uuids))
            for i, uuid in enumerate(uuids):
                uuid_array[i] = uuid
            
            task = self._repo.DeleteRecordsAsync(uuid_array)
            task.Wait()
            return task.Result
        except Exception as e:
            raise RuntimeError(f"删除记录失败: {e}") from e

    def _search_records_sync(self, search_target: str, content: Any) -> Optional[List[str]]:
        """搜索记录（同步版本）"""
        try:
            task = self._repo.SearchRecordsAsync(search_target, content)
            task.Wait()
            result = task.Result
            return list(result) if result else None
        except Exception as e:
            raise RuntimeError(f"搜索记录失败: {e}") from e

    # ---------------- 异步封装方法 ----------------
    async def initialize_database(self) -> bool:
        """异步初始化数据库表结构"""
        return await asyncio.to_thread(self._initialize_database_sync)

    async def create_record(self, data: Dict[str, Any]) -> bool:
        """异步创建单个记录"""
        return await asyncio.to_thread(self._add_records_sync, [data])

    async def create_records(self, data_list: List[Dict[str, Any]]) -> bool:
        """异步创建多个记录"""
        return await asyncio.to_thread(self._add_records_sync, data_list)

    async def read_record(self, uuid: str) -> Optional[Dict[str, Any]]:
        """异步读取单个记录"""
        result = await asyncio.to_thread(self._read_records_sync, [uuid])
        if result and len(result) > 0:
            return json.loads(result[0])
        return None

    async def read_records(self, uuids: List[str]) -> List[Dict[str, Any]]:
        """异步读取多个记录"""
        result = await asyncio.to_thread(self._read_records_sync, uuids)
        if result:
            return [json.loads(r) for r in result]
        return []

    async def update_record(self, uuid: str, data: Dict[str, Any]) -> bool:
        """异步更新记录"""
        return await asyncio.to_thread(self._update_record_sync, uuid, data)

    async def delete_record(self, uuid: str) -> bool:
        """异步删除单个记录"""
        return await asyncio.to_thread(self._delete_records_sync, [uuid])

    async def delete_records(self, uuids: List[str]) -> bool:
        """异步删除多个记录"""
        return await asyncio.to_thread(self._delete_records_sync, uuids)

    async def search_records(self, search_target: str = "*", content: Any = "*") -> List[Dict[str, Any]]:
        """异步搜索记录"""
        result = await asyncio.to_thread(self._search_records_sync, search_target, content)
        if result:
            return [json.loads(r) for r in result]
        return []


class EmployeesRepositoryBridge(BaseRepositoryBridge):
    """员工仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("Employees", connection_string)


class ActivityLogsRepositoryBridge(BaseRepositoryBridge):
    """活动日志仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("ActivityLogs", connection_string)

    async def get_logs_by_user_id(self, user_id: str) -> List[Dict[str, Any]]:
        """按用户ID获取活动日志"""
        try:
            task = self._repo.GetActivityLogsByUserIdAsync(user_id)
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取用户活动日志失败: {e}") from e

    async def get_logs_by_type(self, activity_type: str) -> List[Dict[str, Any]]:
        """按活动类型获取日志"""
        try:
            task = self._repo.GetActivityLogsByTypeAsync(activity_type)
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取类型活动日志失败: {e}") from e

    async def get_logs_by_date_range(self, user_id: str, start_date: datetime, end_date: datetime) -> List[Dict[str, Any]]:
        """按日期范围获取活动日志"""
        try:
            from System import DateTime  # type: ignore
            start_dt = DateTime(start_date.year, start_date.month, start_date.day)
            end_dt = DateTime(end_date.year, end_date.month, end_date.day)
            
            task = self._repo.GetActivityLogsInDateRangeAsync(user_id, start_dt, end_dt)
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取日期范围活动日志失败: {e}") from e


class RecommendationRepositoryBridge(BaseRepositoryBridge):
    """推荐仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("Recommandation", connection_string)

    async def get_recommendations_by_user_id(self, user_id: str) -> List[Dict[str, Any]]:
        """按用户ID获取推荐"""
        try:
            task = self._repo.GetRecommendationsByUserIdAsync(user_id)
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取用户推荐失败: {e}") from e

    async def get_recommendations_by_user_uuid(self, user_uuid: str) -> List[Dict[str, Any]]:
        """按用户UUID获取推荐"""
        try:
            task = self._repo.GetRecommendationsByUserUUIDAsync(user_uuid)
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取用户推荐失败: {e}") from e

    async def get_unpushed_recommendations(self) -> List[Dict[str, Any]]:
        """获取未推送的推荐"""
        try:
            task = self._repo.GetUnpushedRecommendationsAsync()
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取未推送推荐失败: {e}") from e

    async def get_pushed_recommendations(self) -> List[Dict[str, Any]]:
        """获取已推送的推荐"""
        try:
            task = self._repo.GetPushedRecommendationsAsync()
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取已推送推荐失败: {e}") from e

    async def get_recommendations_in_date_range(self, user_id: str, start_date: datetime, end_date: datetime) -> List[Dict[str, Any]]:
        """按日期范围获取推荐"""
        try:
            from System import DateTime  # type: ignore
            start_dt = DateTime(start_date.year, start_date.month, start_date.day)
            end_dt = DateTime(end_date.year, end_date.month, end_date.day)
            
            task = self._repo.GetRecommendationsInDateRangeAsync(user_id, start_dt, end_dt)
            result = await asyncio.to_thread(lambda: task.Result)
            if result:
                return [json.loads(r.ToString()) for r in result]
            return []
        except Exception as e:
            raise RuntimeError(f"获取日期范围推荐失败: {e}") from e


# 测试函数
async def test_factory_bridge():
    """测试工厂桥接器"""
    try:
        print("测试工厂静态类桥接器...")
        
        # 测试员工仓储
        employees = EmployeesRepositoryBridge()
        print("✅ 员工仓储创建成功")
        
        # 测试活动日志仓储
        activities = ActivityLogsRepositoryBridge()
        print("✅ 活动日志仓储创建成功")
        
        # 测试推荐仓储
        recommendations = RecommendationRepositoryBridge()
        print("✅ 推荐仓储创建成功")
        
        print("🎉 所有仓储桥接器创建成功！")
        
    except Exception as e:
        print(f"❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(test_factory_bridge())
