using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestUtils
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            try 
            {                
                var repo = CreateRepository();
                Console.WriteLine($"Repo1 is connected: {repo.IsConnected()}");
                Console.WriteLine($"Repo1 IsInitialized: {await repo.DatabaseIsInitializedAsync()}");
                
                var repo2 = await CreateRepositoryAsync();
                Console.WriteLine($"Repo2 is connected: {repo2.IsConnected()}");
                Console.WriteLine($"Repo2 IsInitialized: {await repo2.DatabaseIsInitializedAsync()}");
                
                if (!await repo.DatabaseIsInitializedAsync())
                {
                    Console.WriteLine("Initializing database with fixed SQL generation...");
                    await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);
                    Console.WriteLine($"Repo1 initialized the database successfully.");
                }

                // 测试 MySqlConnection 重用修复 - 并发操作测试
                Console.WriteLine("\nTesting MySqlConnection reuse fix with concurrent operations...");
                await TestConcurrentOperations(repo);

                // 测试异步读取记录（使用异步 JSON 序列化）
                Console.WriteLine("\nTesting async ReadRecordsAsync with JSON serialization...");
                try
                {
                    var testUUIDs = new[] { "test-uuid-1", "test-uuid-2" };
                    var records = await repo.ReadRecordsAsync(testUUIDs);
                    Console.WriteLine($"Read {records?.Length ?? 0} records successfully using async JSON serialization.");
                    if (records != null && records.Length > 0)
                    {
                        Console.WriteLine($"First record: {records[0]}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ReadRecordsAsync test completed (expected if no data): {ex.Message}");
                }

                // 显示数据库定义以验证修复
                Console.WriteLine("\nDatabase Definition:");
                foreach (var col in repo.DatabaseDefinition)
                {
                    Console.WriteLine($"  {col.Name}: {col.Type} " +
                        $"(PK: {col.IsPrimaryKey}, Auto: {col.AutoIncrement}, " +
                        $"Default: {col.DefaultValue ?? "null"})");
                }

                Console.WriteLine($"\nType 'exit' to quit, or enter SQL commands to test:");
                while (true)
                {
                    Console.Write("SQL>");
                    var input = Console.ReadLine();
                    if (string.IsNullOrEmpty(input)) continue;
                    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
                    if (input.Equals("test", StringComparison.OrdinalIgnoreCase))
                    {
                        await TestConcurrentOperations(repo);
                        continue;
                    }
                    
                    try
                    {
                        var output = await repo.ExecuteCommandAsync(input);
                        Console.WriteLine($"Result: {output}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            }            
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private static async Task TestConcurrentOperations(RepositoriesCore.EmployeesRepository repo)
        {
            Console.WriteLine("Starting concurrent operations test...");
            var tasks = new List<Task>();
            
            // 创建多个并发任务
            for (int i = 0; i < 5; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // 测试并发查询操作
                        var result = await repo.ExecuteCommandAsync("SELECT 1");
                        Console.WriteLine($"Task {taskId}: Query result = {result}");
                        
                        // 测试并发读取操作
                        var records = await repo.ReadRecordsAsync(new[] { $"test-{taskId}" });
                        Console.WriteLine($"Task {taskId}: Read {records?.Length ?? 0} records");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Task {taskId}: Error = {ex.Message}");
                    }
                }));
            }
            
            await Task.WhenAll(tasks);
            Console.WriteLine("Concurrent operations test completed!");
        }

        public static RepositoriesCore.EmployeesRepository CreateRepository()
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

        public static async Task<RepositoriesCore.EmployeesRepository> CreateRepositoryAsync()
        {
            var debugConfigFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            var connectionString = RepositoriesCore.IRepository.GenerateConnectionString(
                server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                );
            
            var repo = new RepositoriesCore.EmployeesRepository(null); // 先创建不连接
            await repo.ConnectAsync(connectionString); // 异步连接
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