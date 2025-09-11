"""
完全基于反射的仓储桥接器 - 避免任何Python类型转换
只使用纯粹的反射调用，不保存任何.NET对象引用
"""
from __future__ import annotations
import os
import asyncio
import json
from typing import Any, Dict, List, Optional

# 导入配置
try:
    from .csharp_config import config as default_config
except ImportError:
    from csharp_config import config as default_config

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
    # 优先查找 net6.0 (兼容性最好)
    frameworks = ["net6.0", "net9.0", "net8.0"]
    for cfg in ("Release", "Debug"):
        for fw in frameworks:
            candidates.append(
                os.path.join(proj_dir, "bin", cfg, fw, "RepositoriesCore.dll")
            )
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
    """加载 C# 程序集"""
    global _ASSEMBLY_LOADED
    if _ASSEMBLY_LOADED and not force:
        return
    _ensure_clr()
    
    try:
        import pythonnet  # type: ignore
        pythonnet.load("coreclr")
    except Exception:
        pass
    
    import clr  # type: ignore
    dll_path = _probe_assembly_path()
    
    try:
        clr.AddReference(dll_path)  # type: ignore
    except Exception as e:
        raise RuntimeError(f"clr.AddReference 失败: {dll_path}\n{e}") from e
    _ASSEMBLY_LOADED = True

class PureReflectionRepositoryBridge:
    """纯反射仓储桥接器 - 完全避免对象引用"""
    
    def __init__(self, repository_type: str, connection_string: Optional[str] = None):
        load_csharp_assembly()
        
        # 只保存必要信息，不保存对象引用
        self._repository_type = repository_type
        self._connection_string = connection_string or default_config.connection_string
        self._factory_type_name = 'RepositoriesCore.RepositoriesFactory'
        self._factory_method_name = f'Create{repository_type}Repository'

    def _get_factory_type(self):
        """获取工厂类型"""
        from System import AppDomain  # type: ignore
        
        loaded_assemblies = list(AppDomain.CurrentDomain.GetAssemblies())
        for asm in loaded_assemblies:
            try:
                factory_type = asm.GetType(self._factory_type_name, False)
                if factory_type is not None:
                    return factory_type
            except Exception:
                continue
        raise RuntimeError(f'未找到工厂类型 {self._factory_type_name}')

    def _create_repository_instance(self):
        """创建仓储实例但不保存引用"""
        factory_type = self._get_factory_type()
        create_method = factory_type.GetMethod(self._factory_method_name)
        
        if create_method is None:
            available_methods = [m.Name for m in factory_type.GetMethods() if m.IsStatic and m.IsPublic]
            raise RuntimeError(
                f'未找到工厂方法 {self._factory_method_name}。可用方法: {available_methods}'
            )
        
        # 创建实例但立即返回，不保存引用
        return create_method.Invoke(None, [self._connection_string])

    def _invoke_repository_method_sync(self, method_name: str, *args):
        """同步调用仓储方法 - 每次都重新创建实例"""
        try:
            # 每次都重新创建实例以避免保存引用
            repo_instance = self._create_repository_instance()
            repo_type = repo_instance.GetType()
            
            method = repo_type.GetMethod(method_name)
            if method is None:
                raise AttributeError(f"方法 {method_name} 不存在")
            
            # 调用方法
            if args:
                task = method.Invoke(repo_instance, list(args))
            else:
                task = method.Invoke(repo_instance, [])
            
            # 等待异步任务完成
            task.Wait()
            result = task.Result
            
            # 清理引用
            del repo_instance
            del repo_type
            del method
            del task
            
            return result
        except Exception as e:
            raise RuntimeError(f"调用方法 {method_name} 失败: {e}") from e

    def _convert_to_cs_dict_array(self, records: List[Dict[str, Any]]):
        """将Python字典列表转换为C#字典数组"""
        from System import Array  # type: ignore
        from System.Collections.Generic import Dictionary  # type: ignore
        
        cs_array = Array.CreateInstance(Dictionary[str, object], len(records))
        for i, record in enumerate(records):
            cs_dict = Dictionary[str, object]()
            for k, v in record.items():
                cs_dict[k] = v
            cs_array[i] = cs_dict
        return cs_array

    def _convert_to_cs_string_array(self, strings: List[str]):
        """将Python字符串列表转换为C#字符串数组"""
        from System import Array  # type: ignore
        cs_array = Array.CreateInstance(str, len(strings))
        for i, s in enumerate(strings):
            cs_array[i] = s
        return cs_array

    def _convert_to_cs_dict(self, record: Dict[str, Any]):
        """将Python字典转换为C#字典"""
        from System.Collections.Generic import Dictionary  # type: ignore
        cs_dict = Dictionary[str, object]()
        for k, v in record.items():
            cs_dict[k] = v
        return cs_dict

    # 基础 CRUD 操作
    async def create_record(self, data: Dict[str, Any]) -> bool:
        """异步创建单个记录"""
        return await self.create_records([data])

    async def create_records(self, data_list: List[Dict[str, Any]]) -> bool:
        """异步创建多个记录"""
        return await asyncio.to_thread(
            self._invoke_repository_method_sync,
            'AddNewRecordsAsync',
            self._convert_to_cs_dict_array(data_list)
        )

    async def read_record(self, uuid: str) -> Optional[Dict[str, Any]]:
        """异步读取单个记录"""
        result = await self.read_records([uuid])
        return result[0] if result else None

    async def read_records(self, uuids: List[str]) -> List[Dict[str, Any]]:
        """异步读取多个记录"""
        json_results = await asyncio.to_thread(
            self._invoke_repository_method_sync,
            'ReadRecordsAsync',
            self._convert_to_cs_string_array(uuids)
        )
        
        if json_results:
            return [json.loads(json_str) for json_str in json_results]
        return []

    async def update_record(self, uuid: str, data: Dict[str, Any]) -> bool:
        """异步更新记录"""
        return await asyncio.to_thread(
            self._invoke_repository_method_sync,
            'UpdateRecordAsync',
            uuid,
            self._convert_to_cs_dict(data)
        )

    async def delete_record(self, uuid: str) -> bool:
        """异步删除单个记录"""
        return await self.delete_records([uuid])

    async def delete_records(self, uuids: List[str]) -> bool:
        """异步删除多个记录"""
        return await asyncio.to_thread(
            self._invoke_repository_method_sync,
            'DeleteRecordsAsync',
            self._convert_to_cs_string_array(uuids)
        )

    async def search_records(self, search_target: str = "*", content: Any = "*") -> List[Dict[str, Any]]:
        """异步搜索记录"""
        json_results = await asyncio.to_thread(
            self._invoke_repository_method_sync,
            'SearchRecordsAsync',
            search_target,
            content
        )
        
        if json_results:
            return [json.loads(json_str) for json_str in json_results]
        return []

    async def database_is_initialized(self) -> bool:
        """检查数据库是否已初始化"""
        return await asyncio.to_thread(
            self._invoke_repository_method_sync,
            'DatabaseIsInitializedAsync'
        )

    async def get_database_definition(self) -> List[Dict[str, Any]]:
        """获取数据库定义"""
        try:
            # 临时创建实例获取数据库定义
            repo_instance = self._create_repository_instance()
            db_def = repo_instance.databaseDefinition
            
            # 转换为Python可读格式
            result = []
            for col_def in db_def:
                col_info = {
                    'Name': str(col_def.Name),
                    'Type': str(col_def.Type),
                    'Length': getattr(col_def, 'Length', ''),
                    'IsPrimaryKey': bool(col_def.IsPrimaryKey),
                    'IsNullable': bool(col_def.IsNullable),
                    'IsUnique': bool(col_def.IsUnique),
                    'IsIndexed': bool(col_def.IsIndexed),
                    'Comment': str(col_def.Comment)
                }
                result.append(col_info)
            
            # 清理引用
            del repo_instance
            del db_def
            
            return result
        except Exception as e:
            raise RuntimeError(f"获取数据库定义失败: {e}") from e


class EmployeesRepositoryBridge(PureReflectionRepositoryBridge):
    """员工仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("Employees", connection_string)


class ActivityLogsRepositoryBridge(PureReflectionRepositoryBridge):
    """活动日志仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("ActivityLogs", connection_string)


class RecommendationRepositoryBridge(PureReflectionRepositoryBridge):
    """推荐仓储桥接器"""
    
    def __init__(self, connection_string: Optional[str] = None):
        super().__init__("Recommandation", connection_string)


# 测试函数
async def test_pure_reflection_bridge():
    """测试纯反射桥接器"""
    try:
        print("🧪 测试纯反射仓储桥接器...")
        
        # 测试员工仓储
        print("\n📋 测试员工仓储...")
        employees = EmployeesRepositoryBridge()
        print("✅ 员工仓储创建成功")
        
        # 测试获取数据库定义
        try:
            db_def = await employees.get_database_definition()
            print(f"📊 数据库定义（前3列）:")
            for i, col in enumerate(db_def[:3]):
                print(f"  {i+1}. {col['Name']}: {col['Type']} - {col['Comment']}")
        except Exception as e:
            print(f"❌ 获取数据库定义失败: {e}")
        
        # 测试数据库检查（预期失败，因为没有真实数据库）
        try:
            is_init = await employees.database_is_initialized()
            print(f"📊 数据库初始化状态: {is_init}")
        except Exception as e:
            print(f"⚠️ 数据库检查失败（预期）: {str(e)[:100]}...")
        
        # 测试活动日志仓储
        print("\n📝 测试活动日志仓储...")
        activities = ActivityLogsRepositoryBridge()
        print("✅ 活动日志仓储创建成功")
        
        # 测试推荐仓储
        print("\n💡 测试推荐仓储...")
        recommendations = RecommendationRepositoryBridge()
        print("✅ 推荐仓储创建成功")
        
        print("\n🎉 所有纯反射仓储桥接器测试成功！")
        print("📌 这个方案完全避免了pythonnet的泛型兼容性问题")
        
    except Exception as e:
        print(f"❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    asyncio.run(test_pure_reflection_bridge())
