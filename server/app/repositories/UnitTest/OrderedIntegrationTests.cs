using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RepositoriesCore;
using Test;

namespace UnitTest
{
    [TestClass]
    [TestCategory("OrderedTests")]
    public sealed class OrderedIntegrationTests
    {
        private static RepositoriesCore.EmployeesRepository? _employeesRepo;
        private static RepositoriesCore.ActivityLogsRepository? _activityLogsRepo;
        private static RepositoriesCore.EmployeesRepository.EmployeeRecord[]? _testEmployees;
        private static RepositoriesCore.ActivityLogsRepository.ActivityLogRecord[]? _testActivityLogs;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            try
            {
                // 初始化仓储实例
                _employeesRepo = Test.TestProgram.CreateEmployeesRepository();
                _activityLogsRepo = Test.TestProgram.CreateActivityLogsRepository();

                // 初始化数据库
                await _employeesRepo.InitializeDatabaseAsync(_employeesRepo.databaseDefinition);
                await _activityLogsRepo.InitializeDatabaseAsync(_activityLogsRepo.databaseDefinition);

                // 准备测试数据
                _testEmployees = CreateTestEmployees();
                _testActivityLogs = CreateTestActivityLogs();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize test class: {ex.Message}", ex);
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _employeesRepo?.Dispose();
            _activityLogsRepo?.Dispose();
        }

        [TestMethod]
        [Priority(1)]
        public async Task Step1_DatabaseInitialization_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be initialized");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be initialized");

            var empInitialized = await _employeesRepo.DatabaseIsInitializedAsync();
            var logInitialized = await _activityLogsRepo.DatabaseIsInitializedAsync();

            Assert.IsTrue(empInitialized, "Employees database should be initialized");
            Assert.IsTrue(logInitialized, "Activity logs database should be initialized");
        }

        [TestMethod]
        [Priority(2)]
        public async Task Step2_AddTestData_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");
            Assert.IsNotNull(_testEmployees, "Test employees should be prepared");
            Assert.IsNotNull(_testActivityLogs, "Test activity logs should be prepared");

            try
            {
                // 添加员工数据
                var empAddResult = await _employeesRepo.AddNewTypedRecordsAsync(_testEmployees);
                Assert.IsTrue(empAddResult, "Adding employee records should succeed");

                // 添加活动日志数据
                var logAddResult = await _activityLogsRepo.AddNewTypedRecordsAsync(_testActivityLogs);
                Assert.IsTrue(logAddResult, "Adding activity log records should succeed");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        [Priority(3)]
        public async Task Step3_ReadAndVerifyData_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");
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
        [Priority(4)]
        public async Task Step4_UpdateOperations_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
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
                Assert.AreEqual("Updated Department", verifyRecords[0].Department, "Employee department should be updated");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        [TestMethod]
        [Priority(5)]
        public async Task Step5_CleanupTestData_Test()
        {
            Assert.IsNotNull(_employeesRepo, "Employees repository should be available");
            Assert.IsNotNull(_activityLogsRepo, "Activity logs repository should be available");
            Assert.IsNotNull(_testEmployees, "Test employees should be available");
            Assert.IsNotNull(_testActivityLogs, "Test activity logs should be available");

            try
            {
                // 删除测试数据
                var empUuids = _testEmployees.Select(e => e.UUID).ToArray();
                var empDeleteResult = await _employeesRepo.DeleteRecordsAsync(empUuids);
                Assert.IsTrue(empDeleteResult, "Employee records deletion should succeed");

                var logUuids = _testActivityLogs.Select(e => e.UUID).ToArray();
                var logDeleteResult = await _activityLogsRepo.DeleteRecordsAsync(logUuids);
                Assert.IsTrue(logDeleteResult, "Activity log records deletion should succeed");

                // 验证删除
                var verifyEmps = await _employeesRepo.ReadTypedRecordsAsync(empUuids);
                Assert.IsTrue(verifyEmps == null || verifyEmps.Length == 0, "Employee records should be deleted");

                var verifyLogs = await _activityLogsRepo.ReadTypedRecordsAsync(logUuids);
                Assert.IsTrue(verifyLogs == null || verifyLogs.Length == 0, "Activity log records should be deleted");
            }
            catch (Exception ex) when (ex.Message.Contains("connection") || ex.Message.Contains("database"))
            {
                Assert.Inconclusive($"Database connection issue: {ex.Message}");
            }
        }

        private static RepositoriesCore.EmployeesRepository.EmployeeRecord[] CreateTestEmployees()
        {
            var now = DateTime.Now;
            return new[]
            {
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "ORDERED_TEST_001",
                    Name: "Ordered Test Employee 1",
                    Department: "Engineering",
                    WorkstationId: "WS-ORDERED-001",
                    Preference: """{"theme": "dark", "notifications": true}""",
                    Online: false,
                    CreatedAt: now,
                    UpdatedAt: now
                ),
                new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "ORDERED_TEST_002",
                    Name: "Ordered Test Employee 2",
                    Department: "HR",
                    WorkstationId: "WS-ORDERED-002",
                    Preference: null,
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
                    UserId: "ORDERED_TEST_001",
                    ActivityType: "sit",
                    DetailInformation: """{"position": "ordered_test_chair"}""",
                    StartTime: now.AddMinutes(-30),
                    EndTime: now.AddMinutes(-15),
                    Duration: 900,
                    CreatedAt: now
                ),
                new RepositoriesCore.ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: "ORDERED_TEST_002",
                    ActivityType: "walk",
                    DetailInformation: """{"route": "ordered_test_corridor"}""",
                    StartTime: now.AddMinutes(-45),
                    EndTime: now.AddMinutes(-40),
                    Duration: 300,
                    CreatedAt: now
                )
            };
        }
    }
}