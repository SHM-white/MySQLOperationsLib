"""C# 仓储使用示例

展示如何使用C#仓储桥接类进行数据操作。
"""
import asyncio
import uuid
from datetime import datetime
import json

from csharp_bridge import (
    RepositoryFactory,
    EmployeesRepositoryBridge,
    ActivityLogsRepositoryBridge,
    RecommendationRepositoryBridge,
    CSharpEmployeesRepositoryAdapter,
    CSharpActivityLogsRepositoryAdapter,
    CSharpRecommendationRepositoryAdapter
)
from csharp_config import config


async def example_direct_repository_usage():
    """直接使用仓储桥接类的示例"""
    print("=== 直接使用仓储桥接类 ===")
    
    # 创建员工仓储实例
    employees_repo = EmployeesRepositoryBridge(config.connection_string)
    
    try:
        # 初始化数据库表
        print("初始化员工数据库表...")
        init_result = await employees_repo.initialize_database()
        print(f"初始化结果: {init_result}")
        
        # 创建员工记录
        employee_id = str(uuid.uuid4())
        employee_data = {
            "UUID": employee_id,
            "UserId": "emp001",
            "Name": "张三",
            "Department": "软件开发部",
            "WorkstationId": "WS001",
            "Preference": json.dumps({
                "theme": "dark",
                "notifications": True,
                "language": "zh-CN"
            }),
            "Online": True,
            "CreatedAt": datetime.now(),
            "UpdatedAt": datetime.now()
        }
        
        print("创建员工记录...")
        create_result = await employees_repo.create_record(employee_data)
        print(f"创建结果: {create_result}")
        
        # 查询员工记录
        print("查询员工记录...")
        employee = await employees_repo.read_record(employee_id)
        print(f"查询结果: {employee}")
        
        # 按UserId搜索
        print("按UserId搜索...")
        search_results = await employees_repo.search_by_user_id("emp001")
        print(f"搜索结果: {search_results}")
        
        # 更新员工记录
        print("更新员工记录...")
        employee_data["Department"] = "技术架构部"
        employee_data["UpdatedAt"] = datetime.now()
        update_result = await employees_repo.update_record(employee_id, employee_data)
        print(f"更新结果: {update_result}")
        
        # 验证更新
        updated_employee = await employees_repo.read_record(employee_id)
        print(f"更新后的记录: {updated_employee}")
        
        # 删除员工记录
        print("删除员工记录...")
        delete_result = await employees_repo.delete_record(employee_id)
        print(f"删除结果: {delete_result}")
        
        # 验证删除
        deleted_employee = await employees_repo.read_record(employee_id)
        print(f"删除后的记录: {deleted_employee}")
        
    finally:
        await employees_repo.aclose()


async def example_activity_logs_usage():
    """活动日志仓储使用示例"""
    print("\n=== 活动日志仓储使用示例 ===")
    
    # 创建活动日志仓储实例
    activity_repo = ActivityLogsRepositoryBridge(config.connection_string)
    
    try:
        # 初始化数据库表
        print("初始化活动日志数据库表...")
        init_result = await activity_repo.initialize_database()
        print(f"初始化结果: {init_result}")
        
        # 创建活动日志记录
        activity_id = str(uuid.uuid4())
        user_uuid = str(uuid.uuid4())
        activity_data = {
            "UUID": activity_id,
            "UserId": "emp001",
            "UserUUID": user_uuid,
            "ActivityType": "meeting",
            "DetailInformation": json.dumps({
                "title": "团队会议",
                "room": "A101",
                "participants": ["张三", "李四", "王五"],
                "agenda": ["项目进度", "技术讨论", "下周计划"]
            }),
            "StartTime": datetime.now(),
            "EndTime": datetime.now(),
            "Duration": 90,  # 90分钟
            "CreatedAt": datetime.now()
        }
        
        print("创建活动日志记录...")
        create_result = await activity_repo.create_record(activity_data)
        print(f"创建结果: {create_result}")
        
        # 按用户ID查询活动日志
        print("按用户ID查询活动日志...")
        user_logs = await activity_repo.get_logs_by_user_id("emp001")
        print(f"用户活动日志: {user_logs}")
        
        # 按活动类型查询
        print("按活动类型查询...")
        meeting_logs = await activity_repo.get_logs_by_activity_type("meeting")
        print(f"会议活动日志: {meeting_logs}")
        
        # 删除活动日志
        print("删除活动日志...")
        delete_result = await activity_repo.delete_record(activity_id)
        print(f"删除结果: {delete_result}")
        
    finally:
        await activity_repo.aclose()


async def example_recommendation_usage():
    """推荐仓储使用示例"""
    print("\n=== 推荐仓储使用示例 ===")
    
    # 创建推荐仓储实例
    recommendation_repo = RecommendationRepositoryBridge(config.connection_string)
    
    try:
        # 初始化数据库表
        print("初始化推荐数据库表...")
        init_result = await recommendation_repo.initialize_database()
        print(f"初始化结果: {init_result}")
        
        # 创建推荐记录
        recommendation_id = str(uuid.uuid4())
        user_uuid = str(uuid.uuid4())
        recommendation_data = {
            "UUID": recommendation_id,
            "UserId": "emp001",
            "UserUUID": user_uuid,
            "CreateTime": datetime.now(),
            "IsPushed": False,
            "Content": json.dumps({
                "type": "health_tip",
                "title": "健康提醒",
                "message": "建议您每隔1小时起身活动5分钟，有助于缓解久坐疲劳。",
                "priority": "medium",
                "category": "exercise"
            })
        }
        
        print("创建推荐记录...")
        create_result = await recommendation_repo.create_record(recommendation_data)
        print(f"创建结果: {create_result}")
        
        # 按用户ID查询推荐
        print("按用户ID查询推荐...")
        user_recommendations = await recommendation_repo.get_recommendations_by_user_id("emp001")
        print(f"用户推荐: {user_recommendations}")
        
        # 获取未推送的推荐
        print("获取未推送的推荐...")
        unpushed_recommendations = await recommendation_repo.get_unpushed_recommendations()
        print(f"未推送推荐: {unpushed_recommendations}")
        
        # 标记为已推送
        print("标记推荐为已推送...")
        recommendation_data["IsPushed"] = True
        update_result = await recommendation_repo.update_record(recommendation_id, recommendation_data)
        print(f"更新结果: {update_result}")
        
        # 验证推送状态
        pushed_recommendations = await recommendation_repo.get_pushed_recommendations()
        print(f"已推送推荐: {pushed_recommendations}")
        
        # 删除推荐记录
        print("删除推荐记录...")
        delete_result = await recommendation_repo.delete_record(recommendation_id)
        print(f"删除结果: {delete_result}")
        
    finally:
        await recommendation_repo.aclose()


async def example_adapter_usage():
    """使用适配器类的示例"""
    print("\n=== 使用适配器类 ===")
    
    # 创建员工仓储适配器
    employees_adapter = CSharpEmployeesRepositoryAdapter(config.connection_string)
    
    try:
        # 创建员工
        employee_data = {
            "UserId": "emp002",
            "Name": "李四",
            "Department": "产品设计部",
            "WorkstationId": "WS002",
            "Preference": json.dumps({"theme": "light"}),
            "Online": False
        }
        
        print("使用适配器创建员工...")
        create_result = await employees_adapter.create(employee_data)
        print(f"创建结果: {create_result}")
        
        # 查询所有员工
        print("查询所有员工...")
        all_employees = await employees_adapter.list_all()
        print(f"所有员工: {all_employees}")
        
        # 按部门查询
        print("按部门查询...")
        design_employees = await employees_adapter.list_all(department="产品设计部")
        print(f"设计部员工: {design_employees}")
        
    finally:
        await employees_adapter.close()
    
    # 测试推荐适配器
    recommendation_adapter = CSharpRecommendationRepositoryAdapter(config.connection_string)
    
    try:
        # 创建推荐
        recommendation_data = {
            "UserId": "emp002",
            "UserUUID": str(uuid.uuid4()),
            "Content": json.dumps({
                "type": "nutrition_tip",
                "message": "建议多喝水，保持身体水分平衡"
            })
        }
        
        print("使用适配器创建推荐...")
        create_result = await recommendation_adapter.create(recommendation_data)
        print(f"创建结果: {create_result}")
        
        # 查询未推送的推荐
        print("查询未推送的推荐...")
        unpushed = await recommendation_adapter.get_unpushed()
        print(f"未推送推荐: {unpushed}")
        
        # 标记为已推送
        if unpushed:
            recommendation_uuid = unpushed[0].get("UUID")
            if recommendation_uuid:
                await recommendation_adapter.mark_as_pushed(recommendation_uuid)
                print("推荐已标记为推送")
        
    finally:
        await recommendation_adapter.close()


async def example_factory_usage():
    """使用工厂类的示例"""
    print("\n=== 使用工厂类 ===")
    
    # 创建仓储工厂
    factory = RepositoryFactory(config.connection_string)
    
    # 初始化所有数据库
    print("初始化所有数据库...")
    init_result = await factory.initialize_all_databases()
    print(f"初始化结果: {init_result}")
    
    # 使用工厂创建仓储实例
    employees_repo = factory.create_employees_repository()
    activity_repo = factory.create_activity_logs_repository()
    recommendation_repo = factory.create_recommendation_repository()
    
    try:
        # 创建员工
        employee_id = str(uuid.uuid4())
        employee_data = {
            "UUID": employee_id,
            "UserId": "emp003",
            "Name": "王五",
            "Department": "测试部",
            "Online": True,
            "CreatedAt": datetime.now(),
            "UpdatedAt": datetime.now()
        }
        
        await employees_repo.create_record(employee_data)
        print("通过工厂创建的员工仓储创建了员工记录")
        
        # 创建活动日志
        activity_id = str(uuid.uuid4())
        activity_data = {
            "UUID": activity_id,
            "UserId": "emp003",
            "UserUUID": employee_id,
            "ActivityType": "testing",
            "DetailInformation": json.dumps({"test_case": "用户登录测试"}),
            "StartTime": datetime.now(),
            "EndTime": datetime.now(),
            "Duration": 30,
            "CreatedAt": datetime.now()
        }
        
        await activity_repo.create_record(activity_data)
        print("通过工厂创建的活动日志仓储创建了活动记录")
        
        # 创建推荐
        recommendation_id = str(uuid.uuid4())
        recommendation_data = {
            "UUID": recommendation_id,
            "UserId": "emp003",
            "UserUUID": employee_id,
            "CreateTime": datetime.now(),
            "IsPushed": False,
            "Content": json.dumps({"message": "测试完成后记得休息片刻"})
        }
        
        await recommendation_repo.create_record(recommendation_data)
        print("通过工厂创建的推荐仓储创建了推荐记录")
        
        # 查询验证
        employees = await employees_repo.list_all_records()
        activities = await activity_repo.list_all_records()
        recommendations = await recommendation_repo.list_all_records()
        
        print(f"当前员工数量: {len(employees)}")
        print(f"当前活动日志数量: {len(activities)}")
        print(f"当前推荐数量: {len(recommendations)}")
        
    finally:
        await employees_repo.aclose()
        await activity_repo.aclose()
        await recommendation_repo.aclose()


async def main():
    """主函数，运行所有示例"""
    print("C# 仓储桥接使用示例")
    print("===================")
    
    try:
        await example_direct_repository_usage()
        await example_activity_logs_usage()
        await example_recommendation_usage()
        await example_adapter_usage()
        await example_factory_usage()
        
        print("\n所有示例执行完成！")
        
    except Exception as e:
        print(f"执行示例时出错: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    # 运行示例
    asyncio.run(main())
