using RepositoriesCore;
using System.Runtime.InteropServices;
using System.Text;
using TestUtils;

namespace Test
{
    public class TestProgram
    {
        static async Task Main(string[] args)
        {
            try 
            {                
                var employeesRepo = CreateEmployeesRepository();
                var activityLogsRepo = CreateActivityLogsRepository();

                Console.WriteLine("=== REPOSITORIES INITIALIZATION ===");
                Console.WriteLine($"Employees repo connection valid: {IRepository.IsValidConnectionString(employeesRepo.ConnectionString)}");
                Console.WriteLine($"Employees repo IsInitialized: {await employeesRepo.DatabaseIsInitializedAsync()}");
                Console.WriteLine($"Employees repo sheet name: {employeesRepo.SheetName}");
                
                Console.WriteLine($"Activity logs repo connection valid: {IRepository.IsValidConnectionString(activityLogsRepo.ConnectionString)}");
                Console.WriteLine($"Activity logs repo IsInitialized: {await activityLogsRepo.DatabaseIsInitializedAsync()}");
                Console.WriteLine($"Activity logs repo sheet name: {activityLogsRepo.SheetName}");

                // 初始化数据库
                await employeesRepo.InitializeDatabaseAsync(employeesRepo.databaseDefinition);
                await activityLogsRepo.InitializeDatabaseAsync(activityLogsRepo.databaseDefinition);

                // 运行员工仓储测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("STARTING EMPLOYEES REPOSITORY TESTS");
                Console.WriteLine("=".PadRight(60, '='));
                await RunEmployeesRepositoryTests(employeesRepo);

                // 运行活动日志仓储测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("STARTING ACTIVITY LOGS REPOSITORY TESTS");
                Console.WriteLine("=".PadRight(60, '='));
                await RunActivityLogsRepositoryTests(activityLogsRepo);

                // 测试并发操作
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("TESTING CONCURRENT OPERATIONS");
                Console.WriteLine("=".PadRight(60, '='));
                await TestConcurrentOperations(employeesRepo, activityLogsRepo);

                // 运行性能测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("RUNNING PERFORMANCE TESTS");
                Console.WriteLine("=".PadRight(60, '='));
                await PerformanceTests.RunPerformanceTests(employeesRepo);
                await ActivityLogsPerformanceTests.RunPerformanceTests(activityLogsRepo);

                // 交互式命令测试
                Console.WriteLine("\n" + "=".PadRight(60, '='));
                Console.WriteLine("INTERACTIVE TESTING MODE");
                Console.WriteLine("=".PadRight(60, '='));
                await InteractiveTestMode(employeesRepo, activityLogsRepo);
            }            
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        private static async Task RunEmployeesRepositoryTests(EmployeesRepository repo)
        {
            Console.WriteLine("1. Testing employees TryConnectAsync method...");
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

            Console.WriteLine("\n2. Testing employees AddNewTypedRecordsAsync method...");
            var testEmployees = CreateTestEmployees();
            try
            {
                var addResult = await repo.AddNewTypedRecordsAsync(testEmployees);
                Console.WriteLine($"✓ AddNewTypedRecordsAsync result: {addResult} (Added {testEmployees.Length} employees)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ AddNewTypedRecordsAsync failed: {ex.Message}");
                return;
            }

            Console.WriteLine("\n3. Testing employees ReadTypedRecordsAsync method...");
            try
            {
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                Console.WriteLine($"✓ ReadTypedRecordsAsync result: Found {readRecords?.Length ?? 0} records");
                if (readRecords != null && readRecords.Length > 0)
                {
                    Console.WriteLine($"  First record: {readRecords[0].Name} - {readRecords[0].Department}");
                    Console.WriteLine($"  Record details: UUID={readRecords[0].UUID}, UserId={readRecords[0].UserId}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ ReadTypedRecordsAsync failed: {ex.Message}");
                return;
            }

            Console.WriteLine("\n4. Testing employees SearchTypedRecordsAsync method...");
            try
            {
                var searchResults = await repo.SearchTypedRecordsAsync("department", "Engineering");
                Console.WriteLine($"✓ SearchTypedRecordsAsync result: Found {searchResults?.Length ?? 0} records in Engineering department");
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
                Console.WriteLine($"✗ SearchTypedRecordsAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n5. Testing employees UpdateTypedRecordAsync method...");
            try
            {
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                
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
                    var updateResult = await repo.UpdateTypedRecordAsync(recordToUpdate.UUID, updatedRecord);
                    Console.WriteLine($"✓ UpdateTypedRecordAsync result: {updateResult}");
                    
                    // 验证更新
                    var verifyRecords = await repo.ReadTypedRecordsAsync(new[] { recordToUpdate.UUID });
                    if (verifyRecords != null && verifyRecords.Length > 0)
                    {
                        Console.WriteLine($"  Verified update: {verifyRecords[0].Department}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ UpdateTypedRecordAsync failed: {ex.Message}");
                Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine("\n6. Testing employees ExecuteCommandAsync method...");
            try
            {
                var countResult = await repo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{repo.SheetName}`");
                Console.WriteLine($"✓ ExecuteCommandAsync result: Total records count = {countResult}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ ExecuteCommandAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n7. Testing employees DeleteRecordsAsync method...");
            try
            {
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                
                if (readRecords != null && readRecords.Length > 1)
                {
                    // 删除最后一个记录
                    var recordToDelete = readRecords[^1];
                    var deleteResult = await repo.DeleteRecordsAsync(new[] { recordToDelete.UUID });
                    Console.WriteLine($"✓ DeleteRecordsAsync result: {deleteResult} (Deleted {recordToDelete.Name})");
                    
                    // 验证删除
                    var verifyAfterDelete = await repo.ReadTypedRecordsAsync(new[] { recordToDelete.UUID });
                    Console.WriteLine($"  Verified deletion: Found {verifyAfterDelete?.Length ?? 0} records (should be 0)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ DeleteRecordsAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n" + "=".PadRight(60, '='));
            Console.WriteLine("EMPLOYEES REPOSITORY TESTS COMPLETED");
            Console.WriteLine("=".PadRight(60, '='));
        }

        private static async Task RunActivityLogsRepositoryTests(ActivityLogsRepository repo)
        {
            Console.WriteLine("1. Testing activity logs TryConnectAsync method...");
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

            Console.WriteLine("\n2. Testing activity logs AddNewTypedRecordsAsync method...");
            var testActivityLogs = CreateTestActivityLogs();
            try
            {
                var addResult = await repo.AddNewTypedRecordsAsync(testActivityLogs);
                Console.WriteLine($"✓ AddNewTypedRecordsAsync result: {addResult} (Added {testActivityLogs.Length} activity logs)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ AddNewTypedRecordsAsync failed: {ex.Message}");
                return;
            }

            Console.WriteLine("\n3. Testing activity logs ReadTypedRecordsAsync method...");
            try
            {
                var uuidsToRead = testActivityLogs.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                Console.WriteLine($"✓ ReadTypedRecordsAsync result: Found {readRecords?.Length ?? 0} records");
                if (readRecords != null && readRecords.Length > 0)
                {
                    Console.WriteLine($"  First record: Employee {readRecords[0].UserId} - {readRecords[0].ActivityType}");
                    Console.WriteLine($"  Record details: UUID={readRecords[0].UUID}, Duration={readRecords[0].Duration}s");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ ReadTypedRecordsAsync failed: {ex.Message}");
                return;
            }

            Console.WriteLine("\n4. Testing activity logs GetActivityLogsByUserIdAsync method...");
            try
            {
                var employeeResults = await repo.GetActivityLogsByUserIdAsync("USER001");
                Console.WriteLine($"✓ GetActivityLogsByUserIdAsync result: Found {employeeResults?.Length ?? 0} records for user USER001");
                if (employeeResults != null && employeeResults.Length > 0)
                {
                    foreach (var log in employeeResults)
                    {
                        Console.WriteLine($"  - {log.ActivityType} ({log.Duration}s)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ GetActivityLogsByUserIdAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n5. Testing activity logs GetActivityLogsByTypeAsync method...");
            try
            {
                var typeResults = await repo.GetActivityLogsByTypeAsync("sit");
                Console.WriteLine($"✓ GetActivityLogsByTypeAsync result: Found {typeResults?.Length ?? 0} 'sit' activity records");
                if (typeResults != null && typeResults.Length > 0)
                {
                    foreach (var log in typeResults)
                    {
                        Console.WriteLine($"  - Employee {log.UserId}: {log.Duration}s");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ GetActivityLogsByTypeAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n6. Testing activity logs GetActivityLogsInDateRangeAsync method...");
            try
            {
                var now = DateTime.Now;
                var startDate = now.AddHours(-1);
                var endDate = now.AddHours(1);
                var dateRangeResults = await repo.GetActivityLogsInDateRangeAsync("USER001", startDate, endDate);
                Console.WriteLine($"✓ GetActivityLogsInDateRangeAsync result: Found {dateRangeResults?.Length ?? 0} records in date range");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ GetActivityLogsInDateRangeAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n7. Testing activity logs UpdateTypedRecordAsync method...");
            try
            {
                var uuidsToRead = testActivityLogs.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                
                if (readRecords != null && readRecords.Length > 0)
                {
                    var recordToUpdate = readRecords[0];
                    var updatedRecord = recordToUpdate with 
                    { 
                        ActivityType = "updated_activity",
                        Duration = recordToUpdate.Duration + 300
                    };
                    
                    Console.WriteLine($"  Updating record: {recordToUpdate.UUID} - {recordToUpdate.ActivityType} -> {updatedRecord.ActivityType}");
                    var updateResult = await repo.UpdateTypedRecordAsync(recordToUpdate.UUID, updatedRecord);
                    Console.WriteLine($"✓ UpdateTypedRecordAsync result: {updateResult}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ UpdateTypedRecordAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n8. Testing activity logs DeleteRecordsAsync method...");
            try
            {
                var uuidsToRead = testActivityLogs.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                
                if (readRecords != null && readRecords.Length > 1)
                {
                    // 删除最后一个记录
                    var recordToDelete = readRecords[^1];
                    var deleteResult = await repo.DeleteRecordsAsync(new[] { recordToDelete.UUID });
                    Console.WriteLine($"✓ DeleteRecordsAsync result: {deleteResult} (Deleted activity log for employee {recordToDelete.UserId})");
                    
                    // 验证删除
                    var verifyAfterDelete = await repo.ReadTypedRecordsAsync(new[] { recordToDelete.UUID });
                    Console.WriteLine($"  Verified deletion: Found {verifyAfterDelete?.Length ?? 0} records (should be 0)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ DeleteRecordsAsync failed: {ex.Message}");
            }

            Console.WriteLine("\n" + "=".PadRight(60, '='));
            Console.WriteLine("ACTIVITY LOGS REPOSITORY TESTS COMPLETED");
            Console.WriteLine("=".PadRight(60, '='));
        }

        private static EmployeesRepository.EmployeeRecord[] CreateTestEmployees()
        {
            var now = DateTime.Now;
            return new[]
            {
                new EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR001",
                    Name: "Alice Johnson",
                    Department: "Engineering",
                    WorkstationId: "WS-001",
                    Preference: """{"theme": "dark", "notifications": true}""",
                    Online: false,
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR002",
                    Name: "Bob Smith",
                    Department: "Engineering",
                    WorkstationId: "WS-002",
                    Preference: """{"theme": "light", "notifications": false}""",
                    Online: false,
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR003",
                    Name: "Carol Davis",
                    Department: "HR",
                    WorkstationId: "WS-003",
                    Preference: null,
                    Online: false,
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USR004",
                    Name: "David Wilson",
                    Department: "Marketing",
                    WorkstationId: null,
                    Preference: """{"theme": "auto", "notifications": true, "language": "en"}""",
                    Online: false,
                    CreatedAt: now,
                    UpdatedAt: now
                )
            };
        }

        private static RepositoriesCore.ActivityLogsRepository.ActivityLogRecord[] CreateTestActivityLogs()
        {
            var now = DateTime.Now;
            return new[]
            {
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USER001",
                    ActivityType: "sit",
                    DetailInformation: """{"position": "desk_chair", "location": "office"}""",
                    StartTime: now.AddMinutes(-30),
                    EndTime: now.AddMinutes(-15),
                    Duration: 900, // 15 minutes
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USER002",
                    ActivityType: "stand",
                    DetailInformation: """{"location": "standing_desk"}""",
                    StartTime: now.AddMinutes(-15),
                    EndTime: now.AddMinutes(-10),
                    Duration: 300, // 5 minutes
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USER003",
                    ActivityType: "walk",
                    DetailInformation: """{"route": "office_corridor", "distance": "50m"}""",
                    StartTime: now.AddMinutes(-45),
                    EndTime: now.AddMinutes(-40),
                    Duration: 300, // 5 minutes
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "USER004",
                    ActivityType: "meeting",
                    DetailInformation: """{"type": "standing_meeting", "participants": 5}""",
                    StartTime: now.AddMinutes(-60),
                    EndTime: now.AddMinutes(-30),
                    Duration: 1800, // 30 minutes
                    CreatedAt: now
                )
            };
        }

        private static async Task TestConcurrentOperations(EmployeesRepository employeesRepo, ActivityLogsRepository activityLogsRepo)
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
                        // 测试员工仓储并发操作
                        var employeeResult = await employeesRepo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{employeesRepo.SheetName}`");
                        Console.WriteLine($"Task {taskId}: Employee count = {employeeResult}");
                        
                        // 测试活动日志仓储并发操作
                        var activityResult = await activityLogsRepo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{activityLogsRepo.SheetName}`");
                        Console.WriteLine($"Task {taskId}: Activity logs count = {activityResult}");
                        
                        // 测试并发连接获取
                        using var employeeConnection = await employeesRepo.TryConnectAsync();
                        using var activityConnection = await activityLogsRepo.TryConnectAsync();
                        
                        Console.WriteLine($"Task {taskId}: Connections - Employee: {employeeConnection != null}, Activity: {activityConnection != null}");
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

        private static async Task InteractiveTestMode(EmployeesRepository employeesRepo, ActivityLogsRepository activityLogsRepo)
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  exit          - Quit the program");
            Console.WriteLine("  test          - Run concurrent operations test");
            Console.WriteLine("  perf          - Run performance tests");
            Console.WriteLine("  stress [n]    - Run stress test (default: 100 iterations)");
            Console.WriteLine("  conn          - Test connections");
            Console.WriteLine("  add-emp       - Add a test employee");
            Console.WriteLine("  add-log       - Add a test activity log");
            Console.WriteLine("  list-emp      - List all employees");
            Console.WriteLine("  list-logs     - List activity logs");
            Console.WriteLine("  search-emp <dept> - Search employees by department");
            Console.WriteLine("  search-logs <type> - Search activity logs by type");
            Console.WriteLine("  search-user <userId> - Search activity logs by user ID");
            Console.WriteLine("  count         - Get record counts");
            Console.WriteLine("  clear-emp     - Clear all employees");
            Console.WriteLine("  clear-logs    - Clear all activity logs");
            Console.WriteLine("  help          - Show this help");
            Console.WriteLine("  <SQL>         - Execute custom SQL command on employees DB");
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
                            await TestConcurrentOperations(employeesRepo, activityLogsRepo);
                            break;

                        case "perf":
                            await PerformanceTests.RunPerformanceTests(employeesRepo);
                            await ActivityLogsPerformanceTests.RunPerformanceTests(activityLogsRepo);
                            break;

                        case "stress":
                            var iterations = 100;
                            if (parts.Length > 1 && int.TryParse(parts[1], out var customIterations))
                            {
                                iterations = customIterations;
                            }
                            await PerformanceTests.RunStressTest(employeesRepo, iterations);
                            break;

                        case "conn":
                            using (var empConn = await employeesRepo.TryConnectAsync())
                            using (var logConn = await activityLogsRepo.TryConnectAsync())
                            {
                                Console.WriteLine($"Employee connection: {(empConn != null ? "Success" : "Failed")}");
                                Console.WriteLine($"Activity logs connection: {(logConn != null ? "Success" : "Failed")}");
                            }
                            break;

                        case "add-emp":
                            await AddTestEmployee(employeesRepo);
                            break;

                        case "add-log":
                            await AddTestActivityLog(activityLogsRepo);
                            break;

                        case "list-emp":
                            await ListAllEmployees(employeesRepo);
                            break;

                        case "list-logs":
                            await ListActivityLogs(activityLogsRepo);
                            break;

                        case "search-emp":
                            if (parts.Length > 1)
                            {
                                await SearchEmployees(employeesRepo, parts[1]);
                            }
                            else
                            {
                                Console.WriteLine("Usage: search-emp <department>");
                            }
                            break;

                        case "search-logs":
                            if (parts.Length > 1)
                            {
                                await SearchActivityLogsByType(activityLogsRepo, parts[1]);
                            }
                            else
                            {
                                Console.WriteLine("Usage: search-logs <activity_type>");
                            }
                            break;

                        case "search-user":
                            if (parts.Length > 1)
                            {
                                await SearchActivityLogsByUser(activityLogsRepo, parts[1]);
                            }
                            else
                            {
                                Console.WriteLine("Usage: search-user <user_id>");
                            }
                            break;

                        case "count":
                            var empCount = await employeesRepo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{employeesRepo.SheetName}`");
                            var logCount = await activityLogsRepo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{activityLogsRepo.SheetName}`");
                            Console.WriteLine($"Total employees: {empCount}");
                            Console.WriteLine($"Total activity logs: {logCount}");
                            break;

                        case "clear-emp":
                            Console.Write("Are you sure you want to delete all employees? (y/N): ");
                            var confirmEmp = Console.ReadLine();
                            if (confirmEmp?.ToLowerInvariant() == "y")
                            {
                                await employeesRepo.ExecuteCommandAsync($"DELETE FROM `{employeesRepo.SheetName}`");
                                Console.WriteLine("All employees deleted.");
                            }
                            break;

                        case "clear-logs":
                            Console.Write("Are you sure you want to delete all activity logs? (y/N): ");
                            var confirmLogs = Console.ReadLine();
                            if (confirmLogs?.ToLowerInvariant() == "y")
                            {
                                await activityLogsRepo.ExecuteCommandAsync($"DELETE FROM `{activityLogsRepo.SheetName}`");
                                Console.WriteLine("All activity logs deleted.");
                            }
                            break;

                        case "help":
                            Console.WriteLine("Available commands:");
                            Console.WriteLine("  exit, test, perf, stress [n], conn, add-emp, add-log");
                            Console.WriteLine("  list-emp, list-logs, search-emp <dept>, search-logs <type>, search-user <userId>");
                            Console.WriteLine("  count, clear-emp, clear-logs, help, <SQL>");
                            break;

                        default:
                            // 执行自定义 SQL (默认在员工数据库上)
                            var output = await employeesRepo.ExecuteCommandAsync(input);
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

        private static async Task AddTestEmployee(EmployeesRepository repo)
        {
            var now = DateTime.Now;
            var testEmployee = new EmployeesRepository.EmployeeRecord(
                UUID: Guid.NewGuid().ToString(),
                UserId: $"USR{now.Ticks % 10000:D4}",
                Name: $"Test Employee {now:HHmmss}",
                Department: "Test Department",
                WorkstationId: $"WS-TEST-{now:HHmmss}",
                Preference: """{"theme": "test", "notifications": true}""",
                Online: false,
                CreatedAt: now,
                UpdatedAt: now
            );

            var result = await repo.AddNewTypedRecordsAsync(new[] { testEmployee });
            Console.WriteLine($"Added test employee: {testEmployee.Name} - Result: {result}");
        }

        private static async Task AddTestActivityLog(ActivityLogsRepository repo)
        {
            var now = DateTime.Now;
            var testLog = new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                UUID: Guid.NewGuid().ToString(),
                UserId: $"USER{new Random().Next(1, 999):D3}",
                ActivityType: "test_activity",
                DetailInformation: """{"type": "test", "duration": 600}""",
                StartTime: now.AddMinutes(-10),
                EndTime: now,
                Duration: 600, // 10 minutes
                CreatedAt: now
            );

            var result = await repo.AddNewTypedRecordsAsync(new[] { testLog });
            Console.WriteLine($"Added test activity log: Employee {testLog.UserId} - {testLog.ActivityType} - Result: {result}");
        }

        private static async Task ListAllEmployees(EmployeesRepository repo)
        {
            var uuids = await GetAllEmployeeUUIDs(repo);
            if (uuids.Length == 0)
            {
                Console.WriteLine("No employees found.");
                return;
            }

            var employees = await repo.ReadTypedRecordsAsync(uuids);
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

        private static async Task ListActivityLogs(ActivityLogsRepository repo)
        {
            var uuids = await GetAllActivityLogUUIDs(repo);
            if (uuids.Length == 0)
            {
                Console.WriteLine("No activity logs found.");
                return;
            }

            var logs = await repo.ReadTypedRecordsAsync(uuids.Take(20).ToArray()); // Limit to 20 for display
            if (logs != null && logs.Length > 0)
            {
                Console.WriteLine($"Found {logs.Length} activity logs (showing first 20):");
                foreach (var log in logs)
                {
                    Console.WriteLine($"  - Employee {log.UserId}: {log.ActivityType} ({log.Duration}s) - {log.StartTime:HH:mm:ss}");
                }
            }
            else
            {
                Console.WriteLine("No activity logs found.");
            }
        }

        private static async Task SearchEmployees(EmployeesRepository repo, string department)
        {
            var results = await repo.SearchTypedRecordsAsync("department", department);
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

        private static async Task SearchActivityLogsByType(ActivityLogsRepository repo, string activityType)
        {
            var results = await repo.GetActivityLogsByTypeAsync(activityType);
            if (results != null && results.Length > 0)
            {
                Console.WriteLine($"Found {results.Length} '{activityType}' activity logs:");
                foreach (var log in results)
                {
                    Console.WriteLine($"  - Employee {log.UserId}: {log.Duration}s at {log.StartTime:HH:mm:ss}");
                }
            }
            else
            {
                Console.WriteLine($"No '{activityType}' activity logs found.");
            }
        }

        private static async Task SearchActivityLogsByUser(ActivityLogsRepository repo, string userId)
        {
            var results = await repo.GetActivityLogsByUserIdAsync(userId);
            if (results != null && results.Length > 0)
            {
                Console.WriteLine($"Found {results.Length} activity logs for user {userId}:");
                foreach (var log in results)
                {
                    Console.WriteLine($"  - {log.ActivityType}: {log.Duration}s at {log.StartTime:HH:mm:ss}");
                }
            }
            else
            {
                Console.WriteLine($"No activity logs found for user {userId}.");
            }
        }

        private static async Task<string[]> GetAllEmployeeUUIDs(EmployeesRepository repo)
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

        private static async Task<string[]> GetAllActivityLogUUIDs(ActivityLogsRepository repo)
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

        public static EmployeesRepository CreateEmployeesRepository()
        {
            var debugConfigFile = Path.Combine(Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            var repo = new EmployeesRepository(IRepository.GenerateConnectionString(
                server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                ));
            return repo;
        }

        public static ActivityLogsRepository CreateActivityLogsRepository()
        {
            var debugConfigFile = Path.Combine(Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            var repo = new ActivityLogsRepository(IRepository.GenerateConnectionString(
                server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                ));
            return repo;
        }
    }
    
    public class IniFileHandler
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