"""Python 与 C# RepositoriesCore 交互桥接层

说明:
1. 先用 `dotnet build` 编译 `RepositoriesCore.csproj` 生成 DLL
   典型输出: server/app/repositories/RepositoriesCore/bin/Release/net8.0/RepositoriesCore.dll
2. 安装 pythonnet (保证 Python 与 .NET 同为 64 位):
   pip install pythonnet
3. 运行时加载 DLL, 获取 C# 类 (例如 EmployeesRepository), 并用 asyncio.to_thread
   在 FastAPI/异步环境中避免阻塞事件循环。

可选替代方案（视团队需求选择）:
- 进程边界: 把 C# 项目包装成 gRPC / REST 服务，由 Python 调用。(隔离、部署清晰)
- 子进程 CLI: C# 输出 JSON; Python subprocess 调用。(简单但性能较低)
- 反向: 用 .NET 8 + pythonnet 在 C# 宿主里托管 Python。(复杂度高; 不推荐当前场景)

本文件实现一个示例仓库包装器: EmployeesRepositoryBridge
仅演示如何桥接/解析返回值, 真实业务请按需要扩展。
"""
from __future__ import annotations
import os
import sys
import asyncio
from typing import Any, Dict, List, Optional
from uuid import UUID

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
    # 优先查找 net6.0 (pythonnet 当前更稳定), 次选 net8.0
    frameworks = ["net6.0", "net8.0"]
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
    import pythonnet  # type: ignore
    try:
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
        clr.AddReference(dll_path)
    except Exception as e:
        raise RuntimeError(f"clr.AddReference 失败: {dll_path}\n{e}") from e
    _ASSEMBLY_LOADED = True

# 类型提示占位 (实际运行后再导入 C# 命名空间)
RepositoriesCore = Any  # type: ignore

class EmployeesRepositoryBridge:
    """包装 C# EmployeesRepository 以 Python 方式调用。

    注意: C# 方法是同步的; 使用 asyncio 环境时用 to_thread 包裹。
    存储格式: C# EmployeesRepository 用内存字典保存字符串, 每条形如:
        UUID=<guid>;{payload}
    这里我们把 payload 视作原始字符串, 若需要结构化, 可改为 JSON 序列化。
    """

    def __init__(self, connection_string: Optional[str] = None):
        load_csharp_assembly()
        from System import AppDomain, Activator  # type: ignore
        from System.Reflection import Assembly  # type: ignore
        target_name = 'RepositoriesCore.EmployeesRepository'
        target_type = None
        loaded_assemblies = list(AppDomain.CurrentDomain.GetAssemblies())
        for asm in loaded_assemblies:
            try:
                t = asm.GetType(target_name, False)
                if t is not None:
                    target_type = t
                    self._assembly = asm
                    break
            except Exception:
                continue
        if target_type is None:
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
            msg = (
                '未找到类型 RepositoriesCore.EmployeesRepository (反射模式)。\n'
                f'已加载程序集数量: {len(loaded_assemblies)}\n'
                f'可见 RepositoriesCore.* 类型: {visible_fmt}\n'
                '排查步骤:\n'
                ' 1) dotnet build (确认生成 DLL).\n'
                ' 2) 确认 DLL 框架为 net6.0 或添加多目标 (net6.0).\n'
                ' 3) 确认 python 与 .NET 均为 64 位.\n'
                ' 4) PowerShell 反射查看: [Reflection.Assembly]::LoadFile("<DLL路径>").GetTypes() | ? FullName -like "RepositoriesCore*"\n'
            )
            raise AttributeError(msg)
        try:
            self._repo = Activator.CreateInstance(target_type, connection_string)
        except TypeError:
            # 可能需要无参构造再手动设置连接
            self._repo = Activator.CreateInstance(target_type)
            if connection_string:
                try:
                    self._repo.Connect(connection_string)
                except Exception:
                    pass
        self._rc_mod = None  # 不再依赖 Python 模块包装

    # ---------------- 同步底层封装 ----------------
    def _create_records_sync(self, records: List[str]) -> bool:
        return self._repo.CreateRecords(records)

    def _read_records_sync(self, uuids: List[str]) -> List[str]:
        res = self._repo.ReadRecords(uuids)
        return list(res) if res is not None else []

    def _update_record_sync(self, uuid: str, record: str) -> bool:
        return self._repo.UpdateRecord(uuid, record)

    def _delete_records_sync(self, uuids: List[str]) -> bool:
        return self._repo.DeleteRecords(uuids)

    def _search_by_user_sync(self, user_id: str) -> List[str]:
        res = self._repo.SearchRecordsByUserId(user_id)
        return list(res) if res is not None else []

    # ---------------- 异步公开 API (供 FastAPI/业务层使用) ----------------
    async def create_many(self, payloads: List[str]) -> bool:
        return await asyncio.to_thread(self._create_records_sync, payloads)

    async def read_many(self, uuids: List[str]) -> List[Dict[str, Any]]:
        raw = await asyncio.to_thread(self._read_records_sync, uuids)
        return [self._parse_line(line) for line in raw]

    async def update_one(self, uuid: str, payload: str) -> bool:
        return await asyncio.to_thread(self._update_record_sync, uuid, payload)

    async def delete_many(self, uuids: List[str]) -> bool:
        return await asyncio.to_thread(self._delete_records_sync, uuids)

    async def search_by_user(self, user_id: str) -> List[Dict[str, Any]]:
        raw = await asyncio.to_thread(self._search_by_user_sync, user_id)
        return [self._parse_line(line) for line in raw]

    # ---------------- 辅助 ----------------
    @staticmethod
    def _parse_line(line: str) -> Dict[str, Any]:
        # 示例格式: UUID=<guid>;some_content
        if line.startswith("UUID="):
            try:
                head, payload = line.split(";", 1)
                uuid_str = head.split("=", 1)[1]
                return {"uuid": uuid_str, "payload": payload}
            except ValueError:
                pass
        return {"raw": line}

    def dispose(self):
        # 显式释放 C# 资源
        self._repo.Dispose()

    async def aclose(self):  # 可用于 FastAPI lifespan
        await asyncio.to_thread(self.dispose)

# ---------- 可选: 适配现有 Python BaseRepository 协议 ----------
class CSharpEmployeesRepositoryAdapter:
    """将 C# EmployeesRepository 适配为简单 CRUD 接口 (演示性质)。

    create(data) 期望 data 为 {"payload": "..."}
    list_all() 通过 search_by_user("") 返回全部(简单实现, 因 C# demo 类未暴露 ListAll)
    """
    def __init__(self, connection_string: Optional[str] = None):
        self._bridge = EmployeesRepositoryBridge(connection_string)

    async def create(self, data: Dict[str, Any]):
        payload = data.get("payload", "")
        ok = await self._bridge.create_many([payload])
        return {"success": ok}

    async def get_by_id(self, id: UUID):  # 无直接 API, 这里示例通过 read_many
        res = await self._bridge.read_many([str(id)])
        return res[0] if res else None

    async def update(self, id: UUID, data: Dict[str, Any]):
        payload = data.get("payload", "")
        ok = await self._bridge.update_one(str(id), payload)
        return {"success": ok}

    async def delete(self, id: UUID):
        return await self._bridge.delete_many([str(id)])

    async def list_all(self, **filters):  # 演示: 使用空 userId 搜索 (示例逻辑, 可扩展)
        res = await self._bridge.search_by_user(filters.get("user_id", ""))
        return res

    async def close(self):
        await self._bridge.aclose()

__all__ = [
    "load_csharp_assembly",
    "EmployeesRepositoryBridge",
    "CSharpEmployeesRepositoryAdapter",
]

if __name__ == "__main__":
    # 简单测试
    async def main():
        repo = EmployeesRepositoryBridge()
        # 创建示例记录 (C# 层会自己生成 UUID, 这里无法直接知道 UUID)
        await repo.create_many(["userId=alice;score=10", "userId=bob;score=5"])
        # 通过 userId 搜索
        alice_records = await repo.search_by_user("alice")
        print("Alice records:", alice_records)
        # 若拿到 UUID 可演示更新/删除
        if alice_records:
            uuid = alice_records[0].get('uuid')
            if uuid:
                await repo.update_one(uuid, f"userId=alice;score=11")
                updated = await repo.read_many([uuid])
                print("Updated:", updated)
                await repo.delete_many([uuid])
                after = await repo.read_many([uuid])
                print("After delete:", after)
        await repo.aclose()

    asyncio.run(main())