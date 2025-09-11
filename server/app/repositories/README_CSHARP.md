# C# 仓储桥接模块

本模块提供了Python与C# RepositoriesCore项目之间的桥接功能，允许在Python FastAPI应用中使用C#实现的高性能数据仓储。

## 文件结构

```
repositories/
├── RepositoriesCore/          # C# 仓储核心项目
│   ├── EmployeesRepository.cs # 员工仓储
│   ├── ActivityLogsRepository.cs # 活动日志仓储
│   ├── RepositoryManagerBase.cs # 仓储基类
│   └── ...
├── csharp_bridge.py          # Python-C# 桥接层
├── csharp_config.py          # C# 仓储配置
├── csharp_usage_examples.py  # 使用示例
└── README_CSHARP.md         # 本文档
```

## 环境要求与兼容性说明

### C# 环境
- .NET 8.0 SDK
- MySQL 数据库

### Python 环境
- Python 3.8+
- pythonnet 包
- 其他依赖见 requirements.txt

## 最终结论 🎯

经过全面测试，我们确认了问题的根本原因和解决方案：

### 问题根源
- **pythonnet 3.0.5 与 .NET 6.0/9.0 泛型兼容性问题**
- 核心错误：`System.Threading.Tasks.Task`1` 类型加载失败
- 影响：任何包含泛型的C#类都无法被pythonnet正确转换为Python对象

### 已验证的事实
✅ C#代码完全正常工作  
✅ 工厂静态类可以被Python发现和调用  
✅ 基础反射操作正常  
❌ 工厂方法返回的泛型仓储实例无法转换为Python对象  

### 推荐解决方案

**方案1: REST API桥接** (🌟 强烈推荐)
- 创建ASP.NET Core Web API包装C#仓储
- Python通过HTTP调用API
- 优点：架构清晰、性能好、易扩展

**方案2: 控制台应用+subprocess** (🔧 可行方案)  
- 已实现的ConsoleApp项目
- Python通过subprocess调用
- 优点：简单易用、充分利用现有代码

**方案3: 等待pythonnet更新** (⏳ 长期方案)
- 监控pythonnet对新.NET版本的支持
- 当前需要等待pythonnet 3.1+版本

详细分析请参考：`INTEGRATION_SUMMARY.md`

## 安装配置

### 1. 编译C#项目

```bash
cd server/app/repositories/RepositoriesCore
dotnet build -c Release
```

编译成功后会在 `bin/Release/net8.0/` 目录下生成 `RepositoriesCore.dll`。

### 2. 安装Python依赖

```bash
pip install pythonnet
```

### 3. 配置数据库连接

通过环境变量配置MySQL连接：

```bash
export MYSQL_HOST=localhost
export MYSQL_PORT=3306
export MYSQL_USER=root
export MYSQL_PASSWORD=your_password
export MYSQL_DATABASE=health_guardian
```

或在代码中直接配置：

```python
from repositories.csharp_config import config

config.connection_string = "Server=localhost;Port=3306;Database=health_guardian;Uid=root;Pwd=your_password;CharSet=utf8mb4;SslMode=None;"
```

## 基本使用

### 1. 使用工厂类（推荐）

```python
from repositories.csharp_bridge import RepositoryFactory

# 创建工厂实例
factory = RepositoryFactory()

# 初始化所有数据库表
await factory.initialize_all_databases()

# 创建员工仓储
employees_repo = factory.create_employees_repository()

# 创建活动日志仓储
activity_repo = factory.create_activity_logs_repository()

# 创建推荐仓储
recommendation_repo = factory.create_recommendation_repository()
```

### 2. 直接使用桥接类

```python
from repositories.csharp_bridge import EmployeesRepositoryBridge, ActivityLogsRepositoryBridge

# 创建员工仓储
employees_repo = EmployeesRepositoryBridge("Server=localhost;Database=health_guardian;...")

# 初始化数据库
await employees_repo.initialize_database()

# 创建员工记录
employee_data = {
    "UUID": "550e8400-e29b-41d4-a716-446655440000",
    "UserId": "emp001",
    "Name": "张三",
    "Department": "软件开发部",
    "Online": True,
    "CreatedAt": datetime.now(),
    "UpdatedAt": datetime.now()
}

await employees_repo.create_record(employee_data)
```

### 3. 使用适配器类

适配器类提供了更符合Python习惯的API：

```python
from repositories.csharp_bridge import CSharpEmployeesRepositoryAdapter

# 创建适配器
adapter = CSharpEmployeesRepositoryAdapter()

# 创建员工
result = await adapter.create({
    "UserId": "emp001",
    "Name": "张三",
    "Department": "软件开发部"
})

# 查询员工
employee = await adapter.get_by_id(result["uuid"])

# 更新员工
await adapter.update(result["uuid"], {"Department": "技术架构部"})

# 查询所有员工
all_employees = await adapter.list_all()

# 按条件查询
it_employees = await adapter.list_all(department="IT部门")
```

## 主要类说明

### BaseRepositoryBridge
所有仓储桥接类的基类，提供通用的CRUD操作：

- `initialize_database()` - 初始化数据库表
- `create_record(data)` - 创建记录
- `read_record(uuid)` - 读取记录
- `update_record(uuid, data)` - 更新记录
- `delete_record(uuid)` - 删除记录
- `search_records(column, value)` - 按列搜索
- `list_all_records()` - 列出所有记录

### EmployeesRepositoryBridge
员工仓储桥接类，继承自BaseRepositoryBridge，额外提供：

- `search_by_user_id(user_id)` - 按用户ID搜索
- `search_by_name(name)` - 按姓名搜索
- `search_by_department(department)` - 按部门搜索
- `get_online_employees()` - 获取在线员工

### ActivityLogsRepositoryBridge
活动日志仓储桥接类，继承自BaseRepositoryBridge，额外提供：

- `get_logs_by_user_id(user_id)` - 按用户ID获取日志
- `get_logs_by_activity_type(activity_type)` - 按活动类型获取日志
- `get_logs_in_date_range(user_id, start_date, end_date)` - 获取日期范围内的日志

### RecommendationRepositoryBridge
推荐仓储桥接类，继承自BaseRepositoryBridge，额外提供：

- `get_recommendations_by_user_id(user_id)` - 按用户ID获取推荐
- `get_recommendations_by_user_uuid(user_uuid)` - 按用户UUID获取推荐
- `get_unpushed_recommendations()` - 获取未推送的推荐
- `get_pushed_recommendations()` - 获取已推送的推荐
- `get_recommendations_in_date_range(user_id, start_date, end_date)` - 获取日期范围内的推荐

### 适配器类
- `CSharpEmployeesRepositoryAdapter` - 员工仓储适配器
- `CSharpActivityLogsRepositoryAdapter` - 活动日志仓储适配器
- `CSharpRecommendationRepositoryAdapter` - 推荐仓储适配器

提供更符合Python习惯的API，自动处理UUID生成、时间戳等。

### RepositoryFactory
仓储工厂类，简化仓储实例的创建和管理：

- `create_employees_repository()` - 创建员工仓储
- `create_activity_logs_repository()` - 创建活动日志仓储
- `create_recommendation_repository()` - 创建推荐仓储
- `create_employees_adapter()` - 创建员工适配器
- `create_activity_logs_adapter()` - 创建活动日志适配器
- `create_recommendation_adapter()` - 创建推荐适配器
- `initialize_all_databases()` - 初始化所有数据库表

## 数据模型

### 员工记录 (EmployeeRecord)
```python
{
    "UUID": "550e8400-e29b-41d4-a716-446655440000",      # 主键
    "UserId": "emp001",                                   # 用户ID
    "Name": "张三",                                       # 姓名
    "Department": "软件开发部",                           # 部门
    "WorkstationId": "WS001",                            # 工位编号（可选）
    "Preference": '{"theme": "dark"}',                   # 偏好设置（JSON字符串）
    "Online": True,                                      # 是否在线
    "CreatedAt": "2023-12-07 10:30:00",                 # 创建时间
    "UpdatedAt": "2023-12-07 10:30:00"                  # 更新时间
}
```

### 活动日志记录 (ActivityLogRecord)
```python
{
    "UUID": "550e8400-e29b-41d4-a716-446655440001",      # 主键
    "UserId": "emp001",                                   # 用户ID
    "UserUUID": "550e8400-e29b-41d4-a716-446655440000",  # 用户UUID（外键）
    "ActivityType": "meeting",                            # 活动类型
    "DetailInformation": '{"room": "A101"}',             # 详细信息（JSON字符串）
    "StartTime": "2023-12-07 09:00:00",                 # 开始时间
    "EndTime": "2023-12-07 10:30:00",                   # 结束时间
    "Duration": 90,                                      # 持续时间（分钟）
    "CreatedAt": "2023-12-07 10:30:00"                  # 创建时间
}
```

### 推荐记录 (RecommendationRecord)
```python
{
    "UUID": "550e8400-e29b-41d4-a716-446655440002",      # 主键
    "UserId": "emp001",                                   # 用户ID
    "UserUUID": "550e8400-e29b-41d4-a716-446655440000",  # 用户UUID（外键）
    "CreateTime": "2023-12-07 10:30:00",                # 创建时间
    "IsPushed": False,                                   # 是否已推送
    "Content": '{"type": "health_tip", "message": "..."}'# 推荐内容（JSON字符串）
}
```

## 错误处理

桥接层会自动处理C#异常，将其转换为Python异常或返回默认值：

```python
try:
    result = await employees_repo.create_record(invalid_data)
except Exception as e:
    print(f"创建失败: {e}")

# 或检查返回值
success = await employees_repo.create_record(data)
if not success:
    print("创建失败")
```

## 性能考虑

1. **连接池**: C#仓储内部使用连接池管理数据库连接
2. **异步操作**: 所有操作都是异步的，避免阻塞事件循环
3. **批量操作**: 支持批量创建、读取、删除操作
4. **资源管理**: 使用`async with`或手动调用`aclose()`释放资源

```python
# 推荐的资源管理方式
async with employees_repo:
    await employees_repo.create_record(data)
    # 自动释放资源

# 或手动管理
try:
    await employees_repo.create_record(data)
finally:
    await employees_repo.aclose()
```

## 测试

运行使用示例：

```bash
cd server/app/repositories
python csharp_usage_examples.py
```

## 故障排查

### 1. DLL未找到
确保已编译C#项目且DLL存在：
```bash
ls server/app/repositories/RepositoriesCore/bin/Release/net8.0/RepositoriesCore.dll
```

### 2. pythonnet导入失败
```bash
pip install pythonnet
```

### 3. 数据库连接失败
检查MySQL服务状态和连接字符串配置。

### 4. 类型不匹配
确保传递给C#的数据类型正确，特别是日期时间和UUID字段。

## 注意事项

1. **线程安全**: C#仓储类是线程安全的，可以在多个asyncio任务中共享
2. **异常处理**: 总是检查操作返回值或捕获异常
3. **资源释放**: 及时释放仓储资源，避免内存泄漏
4. **数据类型**: 注意Python与C#之间的数据类型转换
5. **事务支持**: C#仓储支持事务，可通过连接字符串配置
