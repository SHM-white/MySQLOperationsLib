# Python 使用同步Repository的示例代码

## 安装和导入

```python
import clr
import sys

# 添加.NET程序集路径
sys.path.append(r"path\to\your\RepositoriesCore.dll")

# 导入.NET程序集
clr.AddReference("RepositoriesCore")

# 导入命名空间
from RepositoriesCore import RepositoriesFactory
from System.Collections.Generic import Dictionary
from System import String, Object, DateTime, Boolean
```

## 基本使用

### 1. 创建Repository实例

```python
# 数据库连接字符串
connection_string = "Server=localhost;Database=test;Uid=root;Pwd=password;"

# 创建不同类型的Repository
employees_repo = RepositoriesFactory.CreateEmployeesSyncRepository(connection_string)
activity_logs_repo = RepositoriesFactory.CreateActivityLogsSyncRepository(connection_string)
recommendations_repo = RepositoriesFactory.CreateRecommendationsSyncRepository(connection_string)

# 或者使用通用工厂方法
employees_repo_alt = RepositoriesFactory.CreateSyncRepository("employees", connection_string)
```

### 2. 初始化数据库

```python
# 初始化数据库表结构
if not employees_repo.DatabaseIsInitialized():
    success = employees_repo.InitializeDatabase()
    print(f"Database initialized: {success}")
```

### 3. 添加记录

```python
# 创建员工记录
employee_dict = Dictionary[String, Object]()
employee_dict["UUID"] = "emp-001"
employee_dict["UserId"] = "USER001"
employee_dict["Name"] = "张三"
employee_dict["Department"] = "IT部门"
employee_dict["WorkstationId"] = "W001"
employee_dict["Online"] = True
employee_dict["CreatedAt"] = DateTime.Now
employee_dict["UpdatedAt"] = DateTime.Now

# 添加记录
records = clr.Convert([employee_dict], clr.GetClrType(clr.GetClrType(Dictionary[String, Object]).MakeArrayType()))
success = employees_repo.AddNewRecords(records)
print(f"Employee added: {success}")
```

### 4. 搜索记录

```python
# 根据用户ID搜索员工
user_id = "USER001"
employees = employees_repo.GetEmployeesByUserId(user_id)

if employees:
    for emp in employees:
        print(f"Employee: {emp['Name']} - {emp['Department']}")

# 搜索在线员工
online_employees = employees_repo.GetOnlineEmployees()
if online_employees:
    print(f"Found {len(online_employees)} online employees")
```

### 5. 更新记录

```python
# 更新员工信息
update_dict = Dictionary[String, Object]()
update_dict["Online"] = False
update_dict["UpdatedAt"] = DateTime.Now

success = employees_repo.UpdateRecord("emp-001", update_dict)
print(f"Employee updated: {success}")
```

### 6. 活动日志操作

```python
# 添加活动日志
activity_dict = Dictionary[String, Object]()
activity_dict["UUID"] = "activity-001"
activity_dict["UserId"] = "USER001"
activity_dict["UserUUID"] = "emp-001"
activity_dict["ActivityType"] = "工作"
activity_dict["DetailInformation"] = "{\"task\": \"编程\"}"
activity_dict["StartTime"] = DateTime.Now.AddHours(-2)
activity_dict["EndTime"] = DateTime.Now
activity_dict["Duration"] = 7200  # 2小时
activity_dict["CreatedAt"] = DateTime.Now

activity_records = clr.Convert([activity_dict], clr.GetClrType(clr.GetClrType(Dictionary[String, Object]).MakeArrayType()))
success = activity_logs_repo.AddNewRecords(activity_records)

# 根据用户ID获取活动日志
activities = activity_logs_repo.GetActivityLogsByUserId("USER001")
if activities:
    for activity in activities:
        print(f"Activity: {activity['ActivityType']} - Duration: {activity['Duration']}s")

# 获取指定日期范围内的活动
start_date = DateTime.Now.AddDays(-7)
end_date = DateTime.Now
date_range_activities = activity_logs_repo.GetActivityLogsInDateRange("USER001", start_date, end_date)
```

### 7. 推荐操作

```python
# 添加推荐
recommendation_dict = Dictionary[String, Object]()
recommendation_dict["UUID"] = "rec-001"
recommendation_dict["UserId"] = "USER001"
recommendation_dict["UserUUID"] = "emp-001"
recommendation_dict["CreateTime"] = DateTime.Now
recommendation_dict["IsPushed"] = False
recommendation_dict["Content"] = "建议多休息"

rec_records = clr.Convert([recommendation_dict], clr.GetClrType(clr.GetClrType(Dictionary[String, Object]).MakeArrayType()))
success = recommendations_repo.AddNewRecords(rec_records)

# 获取未推送的推荐
unpushed = recommendations_repo.GetUnpushedRecommendations()
if unpushed:
    print(f"Found {len(unpushed)} unpushed recommendations")

# 标记推荐为已推送
success = recommendations_repo.MarkRecommendationAsPushed("rec-001")
print(f"Recommendation marked as pushed: {success}")
```

## 错误处理

```python
try:
    # Repository操作
    success = employees_repo.AddNewRecords(records)
    if not success:
        print("Failed to add records")
except Exception as e:
    print(f"Error: {str(e)}")
```

## 注意事项

1. **数据类型转换**: 
   - 在Python中创建Dictionary时，需要指定泛型类型
   - 日期使用 `DateTime.Now` 或 `DateTime(year, month, day, hour, minute, second)`
   - 布尔值使用 `True`/`False`

2. **数组转换**:
   - 使用 `clr.Convert()` 将Python列表转换为.NET数组

3. **连接字符串**:
   - 确保数据库连接字符串格式正确
   - 包含所有必要的连接参数

4. **错误处理**:
   - 所有方法都返回明确的成功/失败状态
   - 使用try-catch捕获异常

5. **性能考虑**:
   - 同步方法会阻塞线程，适合简单的脚本使用
   - 对于高并发场景，建议使用原始的异步版本