using RepositoriesCore;
using System.Runtime.InteropServices;
using System.Text;

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

                // 运行完整的数据库操作测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("STARTING COMPREHENSIVE DATABASE OPERATION TESTS");
                Console.WriteLine("=".PadRight(60, '='));
                
                await RunDatabaseOperationTests(repo);

                // 测试并发操作
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("TESTING CONCURRENT OPERATIONS");
                Console.WriteLine("=".PadRight(60, '='));
                await TestConcurrentOperations(repo);

                // 运行性能测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("RUNNING PERFORMANCE TESTS");
                Console.WriteLine("=".PadRight(60, '='));
                await PerformanceTests.RunPerformanceTests(repo);

                // 交互式命令测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("INTERACTIVE TESTING MODE");
                Console.WriteLine("=".PadRight(60, '='));
                await InteractiveTestMode(repo);
            }            
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private static async Task RunDatabaseOperationTests(RepositoriesCore.EmployeesRepository repo)
        {
            Console.WriteLine("1. Testing TryConnectAsync method...");
            using var connection = await repo.TryConnectAsync();
            if (connection != null)
            {
                Console.WriteLine($"✓ TryConnectAsync returned a valid connection. State: {connection.State}");
                using var testCmd = new MySqlConnector.MySqlCommand("SELECT 1", connection);
                var result = await testCmd.ExecuteScalarAsync();
                Console.WriteLine($"✓ Test query result: {result}");
            }
            else
            {
                Console.WriteLine("✗ TryConnectAsync returned null - connection failed");
                return;
            }

            Console.WriteLine("\n2. Testing AddNewRecordsAsync method...");
            var testEmployees = CreateTestEmployees();
            try
            {
                var addResult = await repo.AddNewRecordsAsync(testEmployees);
                Console.WriteLine($"✓ AddNewRecordsAsync result: {addResult} (Added {testEmployees.Length} employees)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ AddNewRecordsAsync failed: {ex.Message}");
                return;
            }

            Console.WriteLine("\n3. Testing ReadRecordsAsync method...");
            try
            {
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadRecordsAsync(uuidsToRead);
                Console.WriteLine($"✓ ReadRecordsAsync result: Found {readRecords?.Length ?? 0} records");
                if (readRecords != null && readRecords.Length > 0)
                {
                    Console.WriteLine($"  First record: {readRecords[0].Name} - {readRecords[0].Department}");
                    Console.WriteLine($"  Record details: UUID={readRecords[0].UUID}, UserId={readRecords[0].UserId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ ReadRecordsAsync failed: {ex.Message}");
                return;
            }

            Console.WriteLine("\n4. Testing SearchRecordsAsync method...");
            try
            {
                var searchResults = await repo.SearchRecordsAsync("department", "Engineering");
                Console.WriteLine($"✓ SearchRecordsAsync result: Found {searchResults?.Length ?? 0} records in Engineering department");
                if (searchResults != null && searchResults.Length > 0)
                {
                    foreach (var emp in searchResults)
                    {
                        Console.WriteLine($"  - {emp.Name} ({emp.UserId})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ SearchRecordsAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n5. Testing UpdateRecordAsync method...");
            try
            {
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadRecordsAsync(uuidsToRead);
                
                if (readRecords != null && readRecords.Length > 0)
                {
                    var recordToUpdate = readRecords[0];
                    var updatedRecord = recordToUpdate with 
                    { 
                        Department = "Updated Engineering",
                        WorkstationId = "WS-UPDATED-001",
                        UpdatedAt = DateTime.Now
                    };
                    
                    Console.WriteLine($"  Updating record: {recordToUpdate.UUID} - {recordToUpdate.Department} -> {updatedRecord.Department}");
                    var updateResult = await repo.UpdateRecordAsync(recordToUpdate.UUID, updatedRecord);
                    Console.WriteLine($"✓ UpdateRecordAsync result: {updateResult}");
                    
                    // 验证更新
                    var verifyRecords = await repo.ReadRecordsAsync(new[] { recordToUpdate.UUID });
                    if (verifyRecords != null && verifyRecords.Length > 0)
                    {
                        Console.WriteLine($"  Verified update: {verifyRecords[0].Department}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ UpdateRecordAsync failed: {ex.Message}");
                Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n6. Testing ExecuteCommandAsync method...");
            try
            {
                var countResult = await repo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{repo.SheetName}`");
                Console.WriteLine($"✓ ExecuteCommandAsync result: Total records count = {countResult}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ ExecuteCommandAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n7. Testing DeleteRecordsAsync method...");
            try
            {
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadRecordsAsync(uuidsToRead);
                
                if (readRecords != null && readRecords.Length > 1)
                {
                    // 删除最后一个记录
                    var recordToDelete = readRecords[^1];
                    var deleteResult = await repo.DeleteRecordsAsync(new[] { recordToDelete.UUID });
                    Console.WriteLine($"✓ DeleteRecordsAsync result: {deleteResult} (Deleted {recordToDelete.Name})");
                    
                    // 验证删除
                    var verifyAfterDelete = await repo.ReadRecordsAsync(new[] { recordToDelete.UUID });
                    Console.WriteLine($"  Verified deletion: Found {verifyAfterDelete?.Length ?? 0} records (should be 0)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ DeleteRecordsAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n8. Testing error handling...");
            try
            {
                await repo.ReadRecordsAsync(new[] { "non-existent-uuid" });
                Console.WriteLine("✓ ReadRecordsAsync with non-existent UUID handled gracefully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error handling test failed: {ex.Message}");
            }

            try
            {
                await repo.SearchRecordsAsync("non_existent_column", "test");
                Console.WriteLine("✗ SearchRecordsAsync with invalid column should have failed");
            }
            catch (Exception)
            {
                Console.WriteLine("✓ SearchRecordsAsync with invalid column properly throws exception");
            }

            Console.WriteLine("\n" + "=".PadRight(60, '='));
            Console.WriteLine("DATABASE OPERATION TESTS COMPLETED");
            Console.WriteLine("=".PadRight(60, '='));
        }

        private static RepositoriesCore.EmployeesRepository.EmployeeRecord[] CreateTestEmployees()
        {
            var now = DateTime.Now;
            return new[]
            {
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR001",
                    Name: "Alice Johnson",
                    Department: "Engineering",
                    WorkstationId: "WS-001",
                    Preference: """{"theme": "dark", "notifications": true}""",
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR002",
                    Name: "Bob Smith",
                    Department: "Engineering",
                    WorkstationId: "WS-002",
                    Preference: """{"theme": "light", "notifications": false}""",
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR003",
                    Name: "Carol Davis",
                    Department: "HR",
                    WorkstationId: "WS-003",
                    Preference: null,
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR004",
                    Name: "David Wilson",
                    Department: "Marketing",
                    WorkstationId: null,
                    Preference: """{"theme": "auto", "notifications": true, "language": "en"}""",
                    CreatedAt: now,
                    UpdatedAt: now
                )
            };
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
                        var result = await repo.ExecuteCommandAsync("SELECT COUNT(*) FROM `Employees`");
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

                        // 测试并发搜索操作
                        var searchResults = await repo.SearchRecordsAsync("department", "Engineering");
                        Console.WriteLine($"Task {taskId}: Search found {searchResults?.Length ?? 0} engineering records");
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

        private static async Task InteractiveTestMode(RepositoriesCore.EmployeesRepository repo)
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  exit          - Quit the program");
            Console.WriteLine("  test          - Run concurrent operations test");
            Console.WriteLine("  perf          - Run performance tests");
            Console.WriteLine("  stress [n]    - Run stress test (default: 100 iterations)");
            Console.WriteLine("  conn          - Test connection");
            Console.WriteLine("  add           - Add a test employee");
            Console.WriteLine("  list          - List all employees");
            Console.WriteLine("  search <dept> - Search by department");
            Console.WriteLine("  count         - Get total employee count");
            Console.WriteLine("  clear         - Clear all employees");
            Console.WriteLine("  help          - Show this help");
            Console.WriteLine("  <SQL>         - Execute custom SQL command");
            Console.WriteLine();

            while (true)
            {
                Console.Write("DB> ");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) continue;

                var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLowerInvariant();

                try
                {
                    switch (command)
                    {
                        case "exit":
                            return;

                        case "test":
                            await TestConcurrentOperations(repo);
                            break;

                        case "perf":
                            await PerformanceTests.RunPerformanceTests(repo);
                            break;

                        case "stress":
                            var iterations = 100;
                            if (parts.Length > 1 && int.TryParse(parts[1], out var customIterations))
                            {
                                iterations = customIterations;
                            }
                            await PerformanceTests.RunStressTest(repo, iterations);
                            break;

                        case "conn":
                            using (var conn = await repo.TryConnectAsync())
                            {
                                Console.WriteLine($"Connection test: {(conn != null ? "Success" : "Failed")}");
                            }
                            break;

                        case "add":
                            await AddTestEmployee(repo);
                            break;

                        case "list":
                            await ListAllEmployees(repo);
                            break;

                        case "search":
                            if (parts.Length > 1)
                            {
                                await SearchEmployees(repo, parts[1]);
                            }
                            else
                            {
                                Console.WriteLine("Usage: search <department>");
                            }
                            break;

                        case "count":
                            var count = await repo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{repo.SheetName}`");
                            Console.WriteLine($"Total employees: {count}");
                            break;

                        case "clear":
                            Console.Write("Are you sure you want to delete all employees? (y/N): ");
                            var confirm = Console.ReadLine();
                            if (confirm?.ToLowerInvariant() == "y")
                            {
                                await repo.ExecuteCommandAsync($"DELETE FROM `{repo.SheetName}`");
                                Console.WriteLine("All employees deleted.");
                            }
                            break;

                        case "help":
                            Console.WriteLine("Available commands:");
                            Console.WriteLine("  exit, test, conn, add, list, search <dept>, count, clear, help, <SQL>");
                            break;

                        default:
                            // 执行自定义 SQL
                            var output = await repo.ExecuteCommandAsync(input);
                            Console.WriteLine($"Result: {output}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }

        private static async Task AddTestEmployee(RepositoriesCore.EmployeesRepository repo)
        {
            var now = DateTime.Now;
            var testEmployee = new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                UUID: Guid.NewGuid().ToString(),
                UserId: $"USR{now.Ticks % 10000:D4}",
                Name: $"Test Employee {now:HHmmss}",
                Department: "Test Department",
                WorkstationId: $"WS-TEST-{now:HHmmss}",
                Preference: """{"theme": "test", "notifications": true}""",
                CreatedAt: now,
                UpdatedAt: now
            );

            var result = await repo.AddNewRecordsAsync(new[] { testEmployee });
            Console.WriteLine($"Added test employee: {testEmployee.Name} - Result: {result}");
        }
        private static async Task ListAllEmployees(RepositoriesCore.EmployeesRepository repo)
        {
            var allEmployees = await repo.ExecuteCommandAsync($"SELECT UUID FROM `{repo.SheetName}`");
            if (string.IsNullOrEmpty(allEmployees))
            {
                Console.WriteLine("No employees found.");
                return;
            }

            // 获取所有 UUID
            var uuids = await GetAllEmployeeUUIDs(repo);
            if (uuids.Length == 0)
            {
                Console.WriteLine("No employees found.");
                return;
            }

            var employees = await repo.ReadRecordsAsync(uuids);
            if (employees != null && employees.Length > 0)
            {
                Console.WriteLine($"Found {employees.Length} employees:");
                foreach (var emp in employees)
                {
                    Console.WriteLine($"  - {emp.Name} ({emp.UserId}) - {emp.Department} - {emp.WorkstationId ?? "No Workstation"}");
                }
            }
            else
            {
                Console.WriteLine("No employees found.");
            }
        }

        private static async Task SearchEmployees(RepositoriesCore.EmployeesRepository repo, string department)
        {
            var results = await repo.SearchRecordsAsync("department", department);
            if (results != null && results.Length > 0)
            {
                Console.WriteLine($"Found {results.Length} employees in {department}:");
                foreach (var emp in results)
                {
                    Console.WriteLine($"  - {emp.Name} ({emp.UserId}) - {emp.WorkstationId ?? "No Workstation"}");
                }
            }
            else
            {
                Console.WriteLine($"No employees found in {department} department.");
            }
        }

        private static async Task<string[]> GetAllEmployeeUUIDs(RepositoriesCore.EmployeesRepository repo)
        {
            using var connection = await repo.TryConnectAsync();
            if (connection == null) return Array.Empty<string>();

            using var cmd = new MySqlConnector.MySqlCommand($"SELECT UUID FROM `{repo.SheetName}`", connection);
            using var reader = await cmd.ExecuteReaderAsync();
            var uuids = new List<string>();
            while (await reader.ReadAsync())
            {
                uuids.Add(reader.GetString("UUID"));
            }
            return uuids.ToArray();
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