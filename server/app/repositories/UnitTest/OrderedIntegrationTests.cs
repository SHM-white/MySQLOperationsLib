using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RepositoriesCore;
using Test;

namespace UnitTest
{
    [TestClass]
    [TestCategory("IntegrationTests")]
    public sealed class RepositoryIntegrationTests
    {
        private static RepositoriesCore.EmployeesRepository? _employeesRepo;
        private static RepositoriesCore.ActivityLogsRepository? _activityLogsRepo;
        private static RepositoriesCore.EmployeesRepository.EmployeeRecord[]? _testEmployees;
        private static RepositoriesCore.ActivityLogsRepository.ActivityLogRecord[]? _testActivityLogs;
        private static readonly SemaphoreSlim _testDataSemaphore = new(1, 1); // 用于同步测试数据操作

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            try
            {
                // 初始化仓储实例
                _employeesRepo = Test.TestProgram.CreateEmployeesRepository();
                _activityLogsRepo = Test.TestProgram.CreateActivityLogsRepository();

                // 确保数据库连接可用
                using var empConnection = await _employeesRepo.TryConnectAsync();
                using var logConnection = await _activityLogsRepo.TryConnectAsync();
                
                if (empConnection == null || logConnection == null)
                {
                    throw new InvalidOperationException("Cannot establish database connections for testing");
                }

                // 初始化数据库
                await _employeesRepo.InitializeDatabaseAsync(_employeesRepo.databaseDefinition);
                await _activityLogsRepo.InitializeDatabaseAsync(_activityLogsRepo.databaseDefinition);

                // 验证数据库初始化
                var empInitialized = await _employeesRepo.DatabaseIsInitializedAsync();
                var logInitialized = await _activityLogsRepo.DatabaseIsInitializedAsync();
                
                if (!empInitialized || !logInitialized)
                {
                    throw new InvalidOperationException("Database initialization failed");
                }

                // 清理可能存在的测试数据
                await CleanupExistingTestData();

                context.WriteLine("Database initialization completed successfully");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize test class: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清理已存在的测试数据
        /// </summary>
        private static async Task CleanupExistingTestData()
        {
            try
            {
                if (_employeesRepo != null)
                {
                    var existingEmpIds = new[] { 
                        "11111111-1111-1111-1111-111111111111", 
                        "22222222-2222-2222-2222-222222222222" 
                    };
                    await _employeesRepo.DeleteRecordsAsync(existingEmpIds);
                    // 等待删除完成
                    await Task.Delay(100);
                }

                if (_activityLogsRepo != null)
                {
                    var existingLogIds = new[] { 
                        "33333333-3333-3333-3333-333333333333", 
                        "44444444-4444-4444-4444-444444444444" 
                    };
                    await _activityLogsRepo.DeleteRecordsAsync(existingLogIds);
                    // 等待删除完成
                    await Task.Delay(100);
                }
            }
            catch
            {
                // 忽略清理错误，可能数据不存在
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            await _testDataSemaphore.WaitAsync();
            try
            {
                // 清理测试数据
                if (_employeesRepo != null && _testEmployees != null)
                {
                    var empUuids = _testEmployees.Select(e => e.UUID).ToArray();
                    await _employeesRepo.DeleteRecordsAsync(empUuids);
                }

                if (_activityLogsRepo != null && _testActivityLogs != null)
                {
                    var logUuids = _testActivityLogs.Select(e => e.UUID).ToArray();
                    await _activityLogsRepo.DeleteRecordsAsync(logUuids);
                }

                // 重置静态变量
                _testEmployees = null;
                _testActivityLogs = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cleanup error (non-critical): {ex.Message}");
            }
            finally
            {
                _testDataSemaphore.Release();
                _employeesRepo?.Dispose();
                _activityLogsRepo?.Dispose();
                _testDataSemaphore?.Dispose();
            }
        }

        [TestMethod]
        [Priority(1)]
        public async Task Test01_DatabaseConnection_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be initialized");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be initialized");

            // 测试连接
            using var empConnection = await _employeesRepo.TryConnectAsync();
            using var logConnection = await _activityLogsRepo.TryConnectAsync();

            Assert.IsNotNull(empConnection, "Employee database connection should be available");
            Assert.IsNotNull(logConnection, "Activity logs database connection should be available");
            
            Assert.AreEqual(System.Data.ConnectionState.Open, empConnection.State, "Employee connection should be open");
            Assert.AreEqual(System.Data.ConnectionState.Open, logConnection.State, "Activity logs connection should be open");
        }

        [TestMethod]
        [Priority(2)]
        public async Task Test02_DatabaseInitialization_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be initialized");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be initialized");

            var empInitialized = await _employeesRepo.DatabaseIsInitializedAsync();
            var logInitialized = await _activityLogsRepo.DatabaseIsInitializedAsync();

            Assert.IsTrue(empInitialized, "Employees database should be initialized");
            Assert.IsTrue(logInitialized, "Activity logs database should be initialized");
        }

        [TestMethod]
        [Priority(3)]
        public async Task Test03_AddTestData_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");

            await _testDataSemaphore.WaitAsync();
            try
            {
                // 重置静态变量以确保数据一致性
                _testEmployees = null;
                _testActivityLogs = null;

                // 确保测试数据已创建
                _testEmployees = CreateTestEmployees();
                _testActivityLogs = CreateTestActivityLogs();

                // 先清理可能存在的旧数据
                var empUuids = _testEmployees.Select(e => e.UUID).ToArray();
                var logUuids = _testActivityLogs.Select(e => e.UUID).ToArray();
                
                try
                {
                    await _employeesRepo.DeleteRecordsAsync(empUuids);
                    await _activityLogsRepo.DeleteRecordsAsync(logUuids);
                }
                catch
                {
                    // 忽略删除错误，可能数据不存在
                }

                // 添加员工数据
                var empAddResult = await _employeesRepo.AddNewTypedRecordsAsync(_testEmployees);
                Assert.IsTrue(empAddResult, "Adding employee records should succeed");

                // 添加活动日志数据
                var logAddResult = await _activityLogsRepo.AddNewTypedRecordsAsync(_testActivityLogs);
                Assert.IsTrue(logAddResult, "Adding activity log records should succeed");

                // 验证数据确实存在
                var finalEmpCheck = await _employeesRepo.ReadTypedRecordsAsync(empUuids);
                var finalLogCheck = await _activityLogsRepo.ReadTypedRecordsAsync(logUuids);
                
                Assert.IsNotNull(finalEmpCheck, "Employee records should exist after add operation");
                Assert.AreEqual(_testEmployees.Length, finalEmpCheck.Length, "All employee records should exist");
                
                Assert.IsNotNull(finalLogCheck, "Activity log records should exist after add operation");
                Assert.AreEqual(_testActivityLogs.Length, finalLogCheck.Length, "All activity log records should exist");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
            finally
            {
                _testDataSemaphore.Release();
            }
        }

        [TestMethod]
        [Priority(4)]
        public async Task Test04_ReadAndVerifyData_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");
            
            // 确保测试数据存在
            await EnsureTestDataExists();
            
            Assert.IsNotNull(_testEmployees, "Test employees should be available");
            Assert.IsNotNull(_testActivityLogs, "Test activity logs should be available");

            try
            {
                // 验证员工数据
                var empUuids = _testEmployees.Select(e => e.UUID).ToArray();
                var readEmployees = await _employeesRepo.ReadTypedRecordsAsync(empUuids);
                Assert.IsNotNull(readEmployees, "Employee records should be readable");
                Assert.AreEqual(_testEmployees.Length, readEmployees.Length, "Should read all employee records");

                // 验证活动日志数据
                var logUuids = _testActivityLogs.Select(e => e.UUID).ToArray();
                var readLogs = await _activityLogsRepo.ReadTypedRecordsAsync(logUuids);
                Assert.IsNotNull(readLogs, "Activity log records should be readable");
                Assert.AreEqual(_testActivityLogs.Length, readLogs.Length, "Should read all activity log records");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        [Priority(5)]
        public async Task Test05_SearchOperations_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");
            
            // 确保测试数据存在
            await EnsureTestDataExists();
            
            Assert.IsNotNull(_testEmployees, "Test employees should be available");
            Assert.IsNotNull(_testActivityLogs, "Test activity logs should be available");

            try
            {
                // 测试员工搜索
                var engineeringEmps = await _employeesRepo.SearchTypedRecordsAsync("Department", "Engineering");
                Assert.IsNotNull(engineeringEmps, "Search results should not be null");
                var expectedCount = _testEmployees.Count(e => e.Department == "Engineering");
                Assert.AreEqual(expectedCount, engineeringEmps.Length, "Should find correct number of engineering employees");

                // 测试活动日志搜索
                var sitActivities = await _activityLogsRepo.GetActivityLogsByTypeAsync("sit");
                Assert.IsNotNull(sitActivities, "Sit activities search should not be null");
                var expectedSitCount = _testActivityLogs.Count(e => e.ActivityType == "sit");
                Assert.AreEqual(expectedSitCount, sitActivities.Length, "Should find correct number of sit activities");

                // 测试按用户ID搜索 - 使用实际存在的用户ID
                var actualUserId = _testActivityLogs[0].UserId; // 使用实际的测试数据用户ID
                var userActivities = await _activityLogsRepo.GetActivityLogsByUserIdAsync(actualUserId);
                Assert.IsNotNull(userActivities, $"User activities search for {actualUserId} should not be null");
                var expectedUserCount = _testActivityLogs.Count(e => e.UserId == actualUserId);
                Assert.AreEqual(expectedUserCount, userActivities.Length, $"Should find correct number of activities for {actualUserId}");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        [Priority(6)]
        public async Task Test06_UpdateOperations_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            
            // 确保测试数据存在
            await EnsureTestDataExists();
            
            Assert.IsNotNull(_testEmployees, "Test employees should be available");

            try
            {
                // 更新第一个员工记录
                var empToUpdate = _testEmployees[0];
                var updatedEmp = empToUpdate with { Department = "Updated Department" };
                var updateResult = await _employeesRepo.UpdateTypedRecordAsync(empToUpdate.UUID, updatedEmp);
                Assert.IsTrue(updateResult, "Employee update should succeed");

                // 验证更新
                var verifyRecords = await _employeesRepo.ReadTypedRecordsAsync(new[] { empToUpdate.UUID });
                Assert.IsNotNull(verifyRecords, "Updated employee record should be readable");
                Assert.AreEqual(1, verifyRecords.Length, "Should find exactly one updated record");
                Assert.AreEqual("Updated Department", verifyRecords[0].Department, "Employee department should be updated");

                // 更新回原值以保持测试数据一致性
                var revertResult = await _employeesRepo.UpdateTypedRecordAsync(empToUpdate.UUID, empToUpdate);
                Assert.IsTrue(revertResult, "Employee revert should succeed");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        [Priority(7)]
        public async Task Test07_ConcurrentOperations_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");

            try
            {
                // 并发测试
                var tasks = new List<Task<bool>>();
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // 测试并发连接
                            using var empConnection = await _employeesRepo.TryConnectAsync();
                            using var logConnection = await _activityLogsRepo.TryConnectAsync();
                            
                            if (empConnection == null || logConnection == null) return false;

                            // 测试并发查询
                            var empResult = await _employeesRepo.ExecuteCommandAsync("SELECT 1");
                            var logResult = await _activityLogsRepo.ExecuteCommandAsync("SELECT 1");
							
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
        [Priority(8)]
        public async Task Test08_ErrorHandling_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");

            try
            {
                // 测试读取不存在的记录
                var nonExistentEmployees = await _employeesRepo.ReadTypedRecordsAsync(new[] { "non-existent-uuid" });
                Assert.IsTrue(nonExistentEmployees == null || nonExistentEmployees.Length == 0, 
                    "Reading non-existent employee records should return empty result");

                var nonExistentLogs = await _activityLogsRepo.ReadTypedRecordsAsync(new[] { "non-existent-uuid" });
                Assert.IsTrue(nonExistentLogs == null || nonExistentLogs.Length == 0, 
                    "Reading non-existent activity log records should return empty result");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        [Priority(9)]
        public void Test09_DataValidation_Test()
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
        [Priority(10)]
        public void Test10_RecordConversion_Test()
        {
            var repo = Test.TestProgram.CreateEmployeesRepository();
            var now = DateTime.Now;
            
            // 测试员工记录转换
            var testRecord = new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                UUID: "test-uuid",
                UserId: "test-user",
                Name: "Test Name",
                Department: "Test Dept",
                WorkstationId: "WS-001",
                Preference: """{"theme": "dark"}""",
                Online: false,
                CreatedAt: now,
                UpdatedAt: now
            );

            var dict = repo.RecordToDict(testRecord);
            Assert.IsNotNull(dict, "Record conversion should not return null");
            Assert.AreEqual("test-uuid", dict["UUID"], "UUID should be converted correctly");

            // 测试反向转换
            var convertedRecord = repo.DictToRecord(dict);
            Assert.IsNotNull(convertedRecord, "Dictionary to record conversion should not return null");
            Assert.AreEqual(testRecord.UUID, convertedRecord.UUID, "UUID should match after conversion");

            // 测试活动日志记录转换
            var logRepo = Test.TestProgram.CreateActivityLogsRepository();
            var testLogRecord = new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                UUID: "test-log-uuid",
                UserId: "TEST123",
                ActivityType: "sit",
                DetailInformation: """{"type": "office_chair"}""",
                StartTime: now.AddMinutes(-30),
                EndTime: now,
                Duration: 1800,
                CreatedAt: now
            );

            var logDict = logRepo.RecordToDict(testLogRecord);
            Assert.IsNotNull(logDict, "Activity log record conversion should not return null");
            Assert.AreEqual("test-log-uuid", logDict["UUID"], "UUID should be converted correctly");

            var convertedLogRecord = logRepo.DictToRecord(logDict);
            Assert.IsNotNull(convertedLogRecord, "Dictionary to activity log record conversion should not return null");
            Assert.AreEqual(testLogRecord.UUID, convertedLogRecord.UUID, "UUID should match after conversion");
        }

        /// <summary>
        /// 确保测试数据存在，如果不存在则创建并添加到数据库
        /// </summary>
        private static async Task EnsureTestDataExists()
        {
            await _testDataSemaphore.WaitAsync();
            try
            {
                // 如果静态变量为null，创建测试数据
                if (_testEmployees == null)
                {
                    _testEmployees = CreateTestEmployees();
                }
                if (_testActivityLogs == null)
                {
                    _testActivityLogs = CreateTestActivityLogs();
                }

                // 确保数据库中有这些数据
                if (_employeesRepo != null && _testEmployees != null)
                {
                    var empUuids = _testEmployees.Select(e => e.UUID).ToArray();
                    var existingEmps = await _employeesRepo.ReadTypedRecordsAsync(empUuids);
                    
                    // 检查是否需要添加数据 - 要求所有数据都存在
                    if (existingEmps == null || existingEmps.Length != _testEmployees.Length)
                    {
                        // 先清理所有相关数据，然后重新添加完整数据
                        try
                        {
                            await _employeesRepo.DeleteRecordsAsync(empUuids);
                        }
                        catch { /* 忽略删除错误 */ }
                        
                        // 等待一小段时间确保删除完成
                        await Task.Delay(50);
                        
                        await _employeesRepo.AddNewTypedRecordsAsync(_testEmployees);
                    }
                }

                if (_activityLogsRepo != null && _testActivityLogs != null)
                {
                    var logUuids = _testActivityLogs.Select(e => e.UUID).ToArray();
                    var existingLogs = await _activityLogsRepo.ReadTypedRecordsAsync(logUuids);
                    
                    // 检查是否需要添加数据 - 要求所有数据都存在
                    if (existingLogs == null || existingLogs.Length != _testActivityLogs.Length)
                    {
                        // 先清理所有相关数据，然后重新添加完整数据
                        try
                        {
                            await _activityLogsRepo.DeleteRecordsAsync(logUuids);
                        }
                        catch { /* 忽略删除错误 */ }
                        
                        // 等待一小段时间确保删除完成
                        await Task.Delay(50);
                        
                        await _activityLogsRepo.AddNewTypedRecordsAsync(_testActivityLogs);
                    }
                }
            }
            finally
            {
                _testDataSemaphore.Release();
            }
        }

        private static RepositoriesCore.EmployeesRepository.EmployeeRecord[] CreateTestEmployees()
        {
            // 使用固定时间戳避免时序问题
            var fixedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            return new[]
            {
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: "11111111-1111-1111-1111-111111111111", // 固定UUID
                    UserId: "INTEGRATION_TEST_001",
                    Name: "Integration Test Employee 1",
                    Department: "Engineering",
                    WorkstationId: "WS-INT-001",
                    Preference: """{"theme": "dark", "notifications": true}""",
                    Online: false,
                    CreatedAt: fixedTime,
                    UpdatedAt: fixedTime
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: "22222222-2222-2222-2222-222222222222", // 固定UUID
                    UserId: "INTEGRATION_TEST_002",
                    Name: "Integration Test Employee 2",
                    Department: "HR",
                    WorkstationId: "WS-INT-002",
                    Preference: null,
                    Online: false,
                    CreatedAt: fixedTime,
                    UpdatedAt: fixedTime
                )
            };
        }

        private static RepositoriesCore.ActivityLogsRepository.ActivityLogRecord[] CreateTestActivityLogs()
        {
            // 使用固定时间戳避免时序问题
            var fixedTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
            return new[]
            {
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: "33333333-3333-3333-3333-333333333333", // 固定UUID
                    UserId: "INTEGRATION_TEST_001",
                    ActivityType: "sit",
                    DetailInformation: """{"position": "integration_test_chair"}""",
                    StartTime: fixedTime.AddMinutes(-30),
                    EndTime: fixedTime.AddMinutes(-15),
                    Duration: 900,
                    CreatedAt: fixedTime
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: "44444444-4444-4444-4444-444444444444", // 固定UUID
                    UserId: "INTEGRATION_TEST_002",
                    ActivityType: "walk",
                    DetailInformation: """{"route": "integration_test_corridor"}""",
                    StartTime: fixedTime.AddMinutes(-45),
                    EndTime: fixedTime.AddMinutes(-40),
                    Duration: 300,
                    CreatedAt: fixedTime
                )
            };
        }
    }
}