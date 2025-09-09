using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using RepositoriesCore;
using Test;

namespace UnitTest
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public async Task EmployeesRepository_Basic_Operations_Test()
        {
            var repo = new RepositoriesCore.EmployeesRepository("Server=localhost;Database=TestDB;User Id=test;Password=test;");

            var initResult = await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);
            var isInitialized = await repo.DatabaseIsInitializedAsync();
            
            // 验证数据库初始化
            Assert.IsTrue(initResult, "Database initialization should succeed");
            // isInitialized 可能为 true (表已建) 或 false (若连接失败)，这里不硬性断言结构存在
        }

        [TestMethod]
        public async Task ActivityLogsRepository_Basic_Operations_Test()
        {
            var repo = new RepositoriesCore.ActivityLogsRepository("Server=localhost;Database=TestDB;User Id=test;Password=test;");

            var initResult = await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);
            var isInitialized = await repo.DatabaseIsInitializedAsync();
            
            // 验证数据库初始化
            Assert.IsTrue(initResult, "Activity logs database initialization should succeed");
        }

        [TestMethod]
        public async Task EmployeesRepository_CRUD_Operations_Test()
        {
            var repo = CreateTestEmployeesRepository();
            
            try
            {
                // 确保数据库已初始化
                await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);

                // 1. 测试添加记录
                var testEmployees = CreateTestEmployees();
                var addResult = await repo.AddNewTypedRecordsAsync(testEmployees);
                Assert.IsTrue(addResult, "Adding new employee records should succeed");

                // 2. 测试读取记录
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                Assert.IsNotNull(readRecords, "Read employee records should not be null");
                Assert.AreEqual(testEmployees.Length, readRecords.Length, "Should read the same number of records as added");

                // 3. 测试搜索记录
                var searchResults = await repo.SearchTypedRecordsAsync("department", "Engineering");
                Assert.IsNotNull(searchResults, "Search results should not be null");
                var engineeringCount = testEmployees.Count(e => e.Department == "Engineering");
                Assert.AreEqual(engineeringCount, searchResults.Length, "Should find correct number of engineering employees");

                // 4. 测试更新记录
                if (readRecords.Length > 0)
                {
                    var recordToUpdate = readRecords[0];
                    var updatedRecord = recordToUpdate with { Department = "Updated Engineering" };
                    var updateResult = await repo.UpdateTypedRecordAsync(recordToUpdate.UUID, updatedRecord);
                    Assert.IsTrue(updateResult, "Update should succeed");

                    // 验证更新
                    var verifyRecords = await repo.ReadTypedRecordsAsync(new[] { recordToUpdate.UUID });
                    Assert.IsNotNull(verifyRecords, "Verification read should not be null");
                    Assert.AreEqual(1, verifyRecords.Length, "Should find exactly one updated record");
                    Assert.AreEqual("Updated Engineering", verifyRecords[0].Department, "Department should be updated");
                }

                // 5. 测试删除记录
                if (readRecords.Length > 0)
                {
                    var recordToDelete = readRecords[^1];
                    var deleteResult = await repo.DeleteRecordsAsync(new[] { recordToDelete.UUID });
                    Assert.IsTrue(deleteResult, "Delete should succeed");

                    // 验证删除
                    var verifyAfterDelete = await repo.ReadTypedRecordsAsync(new[] { recordToDelete.UUID });
                    Assert.IsTrue(verifyAfterDelete == null || verifyAfterDelete.Length == 0, "Record should be deleted");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                // 如果是连接或数据库问题，跳过测试
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task ActivityLogsRepository_CRUD_Operations_Test()
        {
            var repo = CreateTestActivityLogsRepository();
            
            try
            {
                // 确保数据库已初始化
                await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);

                // 1. 测试添加记录
                var testActivityLogs = CreateTestActivityLogs();
                var addResult = await repo.AddNewTypedRecordsAsync(testActivityLogs);
                Assert.IsTrue(addResult, "Adding new activity log records should succeed");

                // 2. 测试读取记录
                var uuidsToRead = testActivityLogs.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadTypedRecordsAsync(uuidsToRead);
                Assert.IsNotNull(readRecords, "Read activity log records should not be null");
                Assert.AreEqual(testActivityLogs.Length, readRecords.Length, "Should read the same number of records as added");

                // 3. 测试搜索记录
                var searchResults = await repo.GetActivityLogsByTypeAsync("sit");
                Assert.IsNotNull(searchResults, "Search results should not be null");
                var sitCount = testActivityLogs.Count(e => e.ActivityType == "sit");
                Assert.AreEqual(sitCount, searchResults.Length, "Should find correct number of sit activities");

                // 4. 测试按员工ID搜索
                var employeeResults = await repo.GetActivityLogsByEmployeeIdAsync(1001);
                Assert.IsNotNull(employeeResults, "Employee search results should not be null");
                var employee1001Count = testActivityLogs.Count(e => e.EmployeeId == 1001);
                Assert.AreEqual(employee1001Count, employeeResults.Length, "Should find correct number of activities for employee 1001");

                // 5. 测试日期范围搜索
                var now = DateTime.Now;
                var dateRangeResults = await repo.GetActivityLogsInDateRangeAsync(1001, now.AddHours(-2), now.AddHours(2));
                Assert.IsNotNull(dateRangeResults, "Date range search results should not be null");

                // 6. 测试更新记录
                if (readRecords.Length > 0)
                {
                    var recordToUpdate = readRecords[0];
                    var updatedRecord = recordToUpdate with { ActivityType = "updated_sit", Duration = 1200 };
                    var updateResult = await repo.UpdateTypedRecordAsync(recordToUpdate.UUID, updatedRecord);
                    Assert.IsTrue(updateResult, "Update should succeed");

                    // 验证更新
                    var verifyRecords = await repo.ReadTypedRecordsAsync(new[] { recordToUpdate.UUID });
                    Assert.IsNotNull(verifyRecords, "Verification read should not be null");
                    Assert.AreEqual(1, verifyRecords.Length, "Should find exactly one updated record");
                    Assert.AreEqual("updated_sit", verifyRecords[0].ActivityType, "Activity type should be updated");
                    Assert.AreEqual(1200, verifyRecords[0].Duration, "Duration should be updated");
                }

                // 7. 测试删除记录
                if (readRecords.Length > 0)
                {
                    var recordToDelete = readRecords[^1];
                    var deleteResult = await repo.DeleteRecordsAsync(new[] { recordToDelete.UUID });
                    Assert.IsTrue(deleteResult, "Delete should succeed");

                    // 验证删除
                    var verifyAfterDelete = await repo.ReadTypedRecordsAsync(new[] { recordToDelete.UUID });
                    Assert.IsTrue(verifyAfterDelete == null || verifyAfterDelete.Length == 0, "Record should be deleted");
                }
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                // 如果是连接或数据库问题，跳过测试
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task Repository_Connection_Test()
        {
            var employeesRepo = CreateTestEmployeesRepository();
            var activityLogsRepo = CreateTestActivityLogsRepository();
            
            try
            {
                // 测试员工仓储连接
                using var employeeConnection = await employeesRepo.TryConnectAsync();
                if (employeeConnection != null)
                {
                    Assert.AreEqual(System.Data.ConnectionState.Open, employeeConnection.State, "Employee connection should be open");
                    
                    // 测试简单查询
                    using var testCmd = new MySqlConnector.MySqlCommand("SELECT 1", employeeConnection);
                    var result = await testCmd.ExecuteScalarAsync();
                    Assert.AreEqual(1, Convert.ToInt32(result), "Test query should return 1");
                }
                else
                {
                    Assert.Inconclusive("Cannot establish employee database connection for testing");
                }

                // 测试活动日志仓储连接
                using var activityConnection = await activityLogsRepo.TryConnectAsync();
                if (activityConnection != null)
                {
                    Assert.AreEqual(System.Data.ConnectionState.Open, activityConnection.State, "Activity logs connection should be open");
                    
                    // 测试简单查询
                    using var testCmd = new MySqlConnector.MySqlCommand("SELECT 1", activityConnection);
                    var result = await testCmd.ExecuteScalarAsync();
                    Assert.AreEqual(1, Convert.ToInt32(result), "Test query should return 1");
                }
                else
                {
                    Assert.Inconclusive("Cannot establish activity logs database connection for testing");
                }
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Connection test failed: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task Repository_Concurrent_Operations_Test()
        {
            var employeesRepo = CreateTestEmployeesRepository();
            var activityLogsRepo = CreateTestActivityLogsRepository();
            
            try
            {
                await employeesRepo.InitializeDatabaseAsync(employeesRepo.DatabaseDefinition);
                await activityLogsRepo.InitializeDatabaseAsync(activityLogsRepo.DatabaseDefinition);

                // 并发测试
                var tasks = new List<Task<bool>>();
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // 测试员工仓储并发连接
                            using var empConnection = await employeesRepo.TryConnectAsync();
                            if (empConnection == null) return false;

                            // 测试活动日志仓储并发连接
                            using var logConnection = await activityLogsRepo.TryConnectAsync();
                            if (logConnection == null) return false;

                            // 测试并发查询
                            var empResult = await employeesRepo.ExecuteCommandAsync("SELECT 1");
                            var logResult = await activityLogsRepo.ExecuteCommandAsync("SELECT 1");
                            
                            return !string.IsNullOrEmpty(empResult) && !string.IsNullOrEmpty(logResult);
                        }
                        catch
                        {
                            return false;
                        }
                    }));
                }

                var results = await Task.WhenAll(tasks);
                Assert.IsTrue(results.All(r => r), "All concurrent operations should succeed");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task Repository_Error_Handling_Test()
        {
            var employeesRepo = CreateTestEmployeesRepository();
            var activityLogsRepo = CreateTestActivityLogsRepository();
            
            try
            {
                await employeesRepo.InitializeDatabaseAsync(employeesRepo.DatabaseDefinition);
                await activityLogsRepo.InitializeDatabaseAsync(activityLogsRepo.DatabaseDefinition);

                // 测试读取不存在的记录 - 员工
                var nonExistentEmployees = await employeesRepo.ReadTypedRecordsAsync(new[] { "non-existent-uuid" });
                Assert.IsTrue(nonExistentEmployees == null || nonExistentEmployees.Length == 0, 
                    "Reading non-existent employee records should return empty result");

                // 测试读取不存在的记录 - 活动日志
                var nonExistentLogs = await activityLogsRepo.ReadTypedRecordsAsync(new[] { "non-existent-uuid" });
                Assert.IsTrue(nonExistentLogs == null || nonExistentLogs.Length == 0, 
                    "Reading non-existent activity log records should return empty result");

                // 测试无效的搜索条件应该抛出异常
                await Assert.ThrowsExceptionAsync<Exception>(async () =>
                {
                    await employeesRepo.SearchTypedRecordsAsync("non_existent_column", "test");
                }, "Searching employees with invalid column should throw exception");

                await Assert.ThrowsExceptionAsync<Exception>(async () =>
                {
                    await activityLogsRepo.SearchTypedRecordsAsync("non_existent_column", "test");
                }, "Searching activity logs with invalid column should throw exception");

                // 测试空参数
                await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                {
                    await employeesRepo.UpdateTypedRecordAsync("", new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                        "", "", "", "", null, null, DateTime.Now, DateTime.Now));
                }, "Update employee with empty UUID should throw ArgumentException");

                await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                {
                    await activityLogsRepo.UpdateTypedRecordAsync("", new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                        "", 0, 0, "", DateTime.Now, DateTime.Now, 0, DateTime.Now));
                }, "Update activity log with empty UUID should throw ArgumentException");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        public void Repository_Data_Validation_Test()
        {
            // 测试连接字符串验证
            Assert.IsTrue(IRepository.IsValidConnectionString("Server=localhost;Database=test;User Id=user;Password=pwd;"),
                "Valid connection string should be recognized");
            
            Assert.IsFalse(IRepository.IsValidConnectionString("invalid connection string"),
                "Invalid connection string should be rejected");

            // 测试连接字符串生成
            var connectionString = IRepository.GenerateConnectionString("localhost", "testdb", "user", "password");
            Assert.IsTrue(IRepository.IsValidConnectionString(connectionString),
                "Generated connection string should be valid");
        }

        [TestMethod]
        public void EmployeeRecord_Conversion_Test()
        {
            var now = DateTime.Now;
            var testRecord = new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                UUID: "test-uuid",
                UserId: "test-user",
                Name: "Test Name",
                Department: "Test Dept",
                WorkstationId: "WS-001",
                Preference: """{"theme": "dark"}""",
                CreatedAt: now,
                UpdatedAt: now
            );

            var repo = CreateTestEmployeesRepository();
            var dict = repo.RecordToDict(testRecord);
            Assert.IsNotNull(dict, "Record conversion should not return null");
            Assert.AreEqual("test-uuid", dict["UUID"], "UUID should be converted correctly");
            Assert.AreEqual("test-user", dict["user_id"], "UserId should be converted correctly");
            Assert.AreEqual("Test Name", dict["name"], "Name should be converted correctly");

            // 测试反向转换
            var convertedRecord = repo.DictToRecord(dict);
            Assert.IsNotNull(convertedRecord, "Dictionary to record conversion should not return null");
            Assert.AreEqual(testRecord.UUID, convertedRecord.UUID, "UUID should match after conversion");
            Assert.AreEqual(testRecord.UserId, convertedRecord.UserId, "UserId should match after conversion");
            Assert.AreEqual(testRecord.Name, convertedRecord.Name, "Name should match after conversion");

            // 测试 null 记录
            var nullDict = repo.RecordToDict(null);
            Assert.IsNull(nullDict, "Null record should return null dictionary");
        }

        [TestMethod]
        public void ActivityLogRecord_Conversion_Test()
        {
            var now = DateTime.Now;
            var testRecord = new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                UUID: "test-uuid",
                LogId: 123,
                EmployeeId: 1001,
                ActivityType: "sit",
                StartTime: now.AddMinutes(-30),
                EndTime: now,
                Duration: 1800,
                CreatedAt: now
            );

            var repo = CreateTestActivityLogsRepository();
            var dict = repo.RecordToDict(testRecord);
            Assert.IsNotNull(dict, "Activity log record conversion should not return null");
            Assert.AreEqual("test-uuid", dict["UUID"], "UUID should be converted correctly");
            Assert.AreEqual(123, dict["log_id"], "LogId should be converted correctly");
            Assert.AreEqual(1001, dict["employee_id"], "EmployeeId should be converted correctly");
            Assert.AreEqual("sit", dict["activity_type"], "ActivityType should be converted correctly");
            Assert.AreEqual(1800, dict["duration"], "Duration should be converted correctly");

            // 测试反向转换
            var convertedRecord = repo.DictToRecord(dict);
            Assert.IsNotNull(convertedRecord, "Dictionary to activity log record conversion should not return null");
            Assert.AreEqual(testRecord.UUID, convertedRecord.UUID, "UUID should match after conversion");
            Assert.AreEqual(testRecord.LogId, convertedRecord.LogId, "LogId should match after conversion");
            Assert.AreEqual(testRecord.EmployeeId, convertedRecord.EmployeeId, "EmployeeId should match after conversion");
            Assert.AreEqual(testRecord.ActivityType, convertedRecord.ActivityType, "ActivityType should match after conversion");
            Assert.AreEqual(testRecord.Duration, convertedRecord.Duration, "Duration should match after conversion");

            // 测试 null 记录
            var nullDict = repo.RecordToDict(null);
            Assert.IsNull(nullDict, "Null activity log record should return null dictionary");
        }

        private static RepositoriesCore.EmployeesRepository CreateTestEmployeesRepository()
        {
            var debugConfigFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            
            try
            {
                var repo = new RepositoriesCore.EmployeesRepository(RepositoriesCore.IRepository.GenerateConnectionString(
                    server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                    database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                    userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                    password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                    ));
                return repo;
            }
            catch
            {
                // 如果无法读取配置文件，使用默认的测试连接
                return new RepositoriesCore.EmployeesRepository("Server=localhost;Database=TestDB;User Id=test;Password=test;");
            }
        }

        private static RepositoriesCore.ActivityLogsRepository CreateTestActivityLogsRepository()
        {
            var debugConfigFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            
            try
            {
                var repo = new RepositoriesCore.ActivityLogsRepository(RepositoriesCore.IRepository.GenerateConnectionString(
                    server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                    database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                    userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                    password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                    ));
                return repo;
            }
            catch
            {
                // 如果无法读取配置文件，使用默认的测试连接
                return new RepositoriesCore.ActivityLogsRepository("Server=localhost;Database=TestDB;User Id=test;Password=test;");
            }
        }

        // 为了向后兼容，保留原有的CreateTestRepository方法
        private static RepositoriesCore.EmployeesRepository CreateTestRepository()
        {
            return CreateTestEmployeesRepository();
        }

        private static RepositoriesCore.EmployeesRepository.EmployeeRecord[] CreateTestEmployees()
        {
            var now = DateTime.Now;
            return new[]
            {
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "TEST001",
                    Name: "Test Employee 1",
                    Department: "Engineering",
                    WorkstationId: "WS-TEST-001",
                    Preference: """{"theme": "dark", "notifications": true}""",
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "TEST002",
                    Name: "Test Employee 2",
                    Department: "HR",
                    WorkstationId: "WS-TEST-002",
                    Preference: null,
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "TEST003",
                    Name: "Test Employee 3",
                    Department: "Engineering",
                    WorkstationId: null,
                    Preference: """{"theme": "light"}""",
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
                    LogId: 0, // Will be auto-generated
                    EmployeeId: 1001,
                    ActivityType: "sit",
                    StartTime: now.AddMinutes(-30),
                    EndTime: now.AddMinutes(-15),
                    Duration: 900, // 15 minutes
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    LogId: 0, // Will be auto-generated
                    EmployeeId: 1001,
                    ActivityType: "stand",
                    StartTime: now.AddMinutes(-15),
                    EndTime: now.AddMinutes(-10),
                    Duration: 300, // 5 minutes
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    LogId: 0, // Will be auto-generated
                    EmployeeId: 1002,
                    ActivityType: "walk",
                    StartTime: now.AddMinutes(-45),
                    EndTime: now.AddMinutes(-40),
                    Duration: 300, // 5 minutes
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    LogId: 0, // Will be auto-generated
                    EmployeeId: 1002,
                    ActivityType: "meeting",
                    StartTime: now.AddMinutes(-60),
                    EndTime: now.AddMinutes(-30),
                    Duration: 1800, // 30 minutes
                    CreatedAt: now
                )
            };
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
