"""
简化的工厂类测试 - 避免泛型类型加载问题
"""
import os
import sys

def test_factory_static_method():
    """测试工厂静态方法调用"""
    try:
        # 加载程序集
        import pythonnet
        pythonnet.load("coreclr")
        import clr
        
        # 添加DLL路径
        base_dir = os.path.dirname(__file__)
        dll_path = os.path.join(base_dir, "RepositoriesCore", "bin", "Release", "net9.0", "RepositoriesCore.dll")
        if not os.path.exists(dll_path):
            dll_path = os.path.join(base_dir, "RepositoriesCore", "bin", "Debug", "net9.0", "RepositoriesCore.dll")
        if not os.path.exists(dll_path):
            dll_path = os.path.join(base_dir, "RepositoriesCore", "bin", "Release", "net6.0", "RepositoriesCore.dll")
        
        print(f"DLL路径: {dll_path}")
        print(f"DLL存在: {os.path.exists(dll_path)}")
        
        clr.AddReference(dll_path)
        
        # 获取工厂类型
        from System import AppDomain
        loaded_assemblies = list(AppDomain.CurrentDomain.GetAssemblies())
        print(f"已加载程序集数量: {len(loaded_assemblies)}")
        
        factory_type = None
        for asm in loaded_assemblies:
            try:
                factory_type = asm.GetType('RepositoriesCore.RepositoriesFactory', False)
                if factory_type is not None:
                    print(f"✅ 找到工厂类型: {factory_type}")
                    break
            except Exception as e:
                continue
        
        if factory_type is None:
            print("❌ 未找到工厂类型")
            return
        
        # 列出工厂方法
        methods = factory_type.GetMethods()
        print(f"工厂类型方法:")
        for method in methods:
            if method.IsStatic and method.IsPublic:
                print(f"  - {method.Name}: {method}")
        
        # 尝试调用静态方法
        method = factory_type.GetMethod('CreateEmployeesRepository')
        if method is not None:
            print(f"✅ 找到CreateEmployeesRepository方法: {method}")
            
            # 尝试调用方法 - 使用反射避免直接实例化
            try:
                result = method.Invoke(None, ["Server=localhost;Database=test;Uid=root;Pwd=password;"])
                print(f"✅ 方法调用成功，返回类型: {type(result)}")
                print(f"返回对象: {result}")
            except Exception as e:
                print(f"❌ 方法调用失败: {e}")
        else:
            print("❌ 未找到CreateEmployeesRepository方法")
        
    except Exception as e:
        print(f"❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()


if __name__ == "__main__":
    test_factory_static_method()
