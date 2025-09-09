using RepositoriesCore;
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
                Console.WriteLine($"Repo connection string valid: {IRepository.IsValidConnectionString(repo.ConnectionString)}");
                Console.WriteLine($"Repo IsInitialized: {await repo.DatabaseIsInitializedAsync()}");

                Console.WriteLine($"Repo sheet name: {repo.SheetName}");
                
                if (!await repo.DatabaseIsInitializedAsync())
                {
                    Console.WriteLine("Initializing database with new connection logic...");
                    await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);
                    Console.WriteLine($"Repo initialized the database successfully.");
                }

                // 测试新的连接逻辑 - 并发操作测试
                Console.WriteLine("\nTesting new connection logic with concurrent operations...");
                await TestConcurrentOperations(repo);

                // 测试异步读取记录（使用异步 JSON 序列化）
                Console.WriteLine("\nTesting async ReadRecordsAsync with new connection management...");
                try
                {
                    var testUUIDs = new[] { "test-uuid-1", "test-uuid-2" };
                    var records = await repo.ReadRecordsAsync(testUUIDs);
                    Console.WriteLine($"Read {records?.Length ?? 0} records successfully using new connection logic.");
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

                // 测试 TryConnectAsync 返回 MySqlConnection?
                Console.WriteLine("\nTesting TryConnectAsync method:");
                using var connection = await repo.TryConnectAsync();
                if (connection != null)
                {
                    Console.WriteLine($"TryConnectAsync returned a valid connection. State: {connection.State}");
                    // 测试连接
                    using var testCmd = new MySqlConnector.MySqlCommand("SELECT 1", connection);
                    var result = await testCmd.ExecuteScalarAsync();
                    Console.WriteLine($"Test query result: {result}");
                }
                else
                {
                    Console.WriteLine("TryConnectAsync returned null - connection failed");
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
                    if (input.Equals("conn", StringComparison.OrdinalIgnoreCase))
                    {
                        using var conn = await repo.TryConnectAsync();
                        Console.WriteLine($"Connection test: {(conn != null ? "Success" : "Failed")}");
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
            Console.WriteLine("Starting concurrent operations test with new connection logic...");
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
                        
                        // 测试并发连接获取
                        using var connection = await repo.TryConnectAsync();
                        if (connection != null)
                        {
                            Console.WriteLine($"Task {taskId}: Got connection successfully");
                        }
                        else
                        {
                            Console.WriteLine($"Task {taskId}: Failed to get connection");
                        }
                        
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