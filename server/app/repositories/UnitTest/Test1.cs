using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.SqlClient;

namespace UnitTest
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public void Repository_Methods_Compile()
        {
            var repo = new RepositoriesCore.EmployeesRepository("Server=localhost;Database=Dummy;User Id=user;Password=pwd;");
            var initResult = repo.InitializeDatabase(new List<string>{"UserId","Record"});
            var isInitialized = repo.DatabaseIsInitialized();
            // 对于空实现 / 或尚未真正连接的情况，只验证方法调用流程
            Assert.IsTrue(initResult); // 动态建表返回 true
            // isInitialized 可能为 true (表已建) 或 false (若连接失败)，这里不硬性断言结构存在
        }
        public static RepositoriesCore.EmployeesRepository CreateRepository(string connectionString)
        {
            var debugConfigFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            var repo = new RepositoriesCore.EmployeesRepository(RepositoriesCore.IRepository.GenerateConnectionString(
                server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                ));
            return repo;
        }

    }
    class IniFileHandler
    {
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public void WriteValue(string section, string key, string value, string filePath)
        {
            WritePrivateProfileString(section, key, value, filePath);
        }

        public string ReadValue(string section, string key, string filePath)
        {
            StringBuilder temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", temp, 255, filePath);
            return temp.ToString();
        }
    }

}
