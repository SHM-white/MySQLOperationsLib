"""
通过反射操作仓储实例 - 避免泛型问题
"""
import os
import json

def test_repository_operations():
    """测试仓储操作"""
    try:
        # 加载程序集
        import pythonnet
        pythonnet.load("coreclr")
        import clr
        
        # 添加DLL路径
        base_dir = os.path.dirname(__file__)
        dll_path = os.path.join(base_dir, "RepositoriesCore", "bin", "Release", "net6.0", "RepositoriesCore.dll")
        
        clr.AddReference(dll_path)
        
        # 获取工厂并创建实例
        from System import AppDomain
        loaded_assemblies = list(AppDomain.CurrentDomain.GetAssemblies())
        
        factory_type = None
        for asm in loaded_assemblies:
            try:
                factory_type = asm.GetType('RepositoriesCore.RepositoriesFactory', False)
                if factory_type is not None:
                    break
            except Exception:
                continue
        
        # 创建员工仓储实例
        create_method = factory_type.GetMethod('CreateEmployeesRepository')
        employees_repo = create_method.Invoke(None, ["Server=localhost;Database=test;Uid=root;Pwd=password;"])
        
        print(f"✅ 员工仓储实例: {employees_repo}")
        
        # 尝试调用实例方法 - 使用反射避免泛型问题
        repo_type = employees_repo.GetType()
        print(f"仓储类型: {repo_type}")
        
        # 列出所有方法
        methods = repo_type.GetMethods()
        print("可用方法:")
        for method in methods:
            if not method.Name.startswith('get_') and not method.Name.startswith('set_') and not method.Name in ['ToString', 'GetHashCode', 'GetType', 'Equals']:
                print(f"  - {method.Name}: {method}")
        
        # 尝试调用数据库初始化检查方法
        try:
            check_method = repo_type.GetMethod('DatabaseIsInitializedAsync')
            if check_method:
                print(f"✅ 找到DatabaseIsInitializedAsync方法")
                
                # 调用异步方法并等待结果
                task = check_method.Invoke(employees_repo, [])
                print(f"初始化检查任务: {task}")
                
                # 等待任务完成
                task.Wait()
                result = task.Result
                print(f"✅ 数据库初始化状态: {result}")
            else:
                print("❌ 未找到DatabaseIsInitializedAsync方法")
        except Exception as e:
            print(f"数据库检查失败: {e}")
        
        # 尝试获取数据库定义
        try:
            db_def_property = repo_type.GetProperty('databaseDefinition')
            if db_def_property:
                db_def = db_def_property.GetValue(employees_repo)
                print(f"✅ 数据库定义: {db_def}")
                
                # 列举列定义
                count = 0
                for col in db_def:
                    print(f"  列{count}: {col}")
                    count += 1
                    if count > 3:  # 只显示前几列
                        break
            else:
                print("❌ 未找到databaseDefinition属性")
        except Exception as e:
            print(f"获取数据库定义失败: {e}")
        
        print("🎉 基础反射操作测试成功！")
        
    except Exception as e:
        print(f"❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    test_repository_operations()
