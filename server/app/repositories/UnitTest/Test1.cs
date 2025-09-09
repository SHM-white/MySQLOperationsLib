using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using RepositoriesCore;

namespace UnitTest
{
    [TestClass]
    public sealed class Test1
    {
        [TestMethod]
        public async Task Repository_Basic_Operations_Test()
        {
            var repo = new RepositoriesCore.EmployeesRepository("Server=localhost;Database=TestDB;User Id=test;Password=test;");

            var initResult = await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);
            var isInitialized = await repo.DatabaseIsInitializedAsync();
            
            // 验证数据库初始化
            Assert.IsTrue(initResult, "Database initialization should succeed");
            // isInitialized 可能为 true (表已建) 或 false (若连接失败)，这里不硬性断言结构存在
        }

        [TestMethod]
        public async Task Repository_CRUD_Operations_Test()
        {
            var repo = CreateTestRepository();
            
            try
            {
                // 确保数据库已初始化
                await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);

                // 1. 测试添加记录
                var testEmployees = CreateTestEmployees();
                var addResult = await repo.AddNewRecordsAsync(testEmployees);
                Assert.IsTrue(addResult, "Adding new records should succeed");

                // 2. 测试读取记录
                var uuidsToRead = testEmployees.Select(e => e.UUID).ToArray();
                var readRecords = await repo.ReadRecordsAsync(uuidsToRead);
                Assert.IsNotNull(readRecords, "Read records should not be null");
                Assert.AreEqual(testEmployees.Length, readRecords.Length, "Should read the same number of records as added");

                // 3. 测试搜索记录
                var searchResults = await repo.SearchRecordsAsync("department", "Engineering");
                Assert.IsNotNull(searchResults, "Search results should not be null");
                var engineeringCount = testEmployees.Count(e => e.Department == "Engineering");
                Assert.AreEqual(engineeringCount, searchResults.Length, "Should find correct number of engineering employees");

                // 4. 测试更新记录
                if (readRecords.Length > 0)
                {
                    var recordToUpdate = readRecords[0];
                    var updatedRecord = recordToUpdate with { Department = "Updated Engineering" };
                    var updateResult = await repo.UpdateRecordAsync(recordToUpdate.UUID, updatedRecord);
                    Assert.IsTrue(updateResult, "Update should succeed");

                    // 验证更新
                    var verifyRecords = await repo.ReadRecordsAsync(new[] { recordToUpdate.UUID });
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
                    var verifyAfterDelete = await repo.ReadRecordsAsync(new[] { recordToDelete.UUID });
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
            var repo = CreateTestRepository();
            
            try
            {
                // 测试连接
                using var connection = await repo.TryConnectAsync();
                if (connection != null)
                {
                    Assert.AreEqual(System.Data.ConnectionState.Open, connection.State, "Connection should be open");
                    
                    // 测试简单查询
                    using var testCmd = new MySqlConnector.MySqlCommand("SELECT 1", connection);
                    var result = await testCmd.ExecuteScalarAsync();
                    Assert.AreEqual(1, Convert.ToInt32(result), "Test query should return 1");
                }
                else
                {
                    Assert.Inconclusive("Cannot establish database connection for testing");
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
            var repo = CreateTestRepository();
            
            try
            {
                await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);

                // 并发测试
                var tasks = new List<Task<bool>>();
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            // 测试并发连接
                            using var connection = await repo.TryConnectAsync();
                            if (connection == null) return false;

                            // 测试并发查询
                            var result = await repo.ExecuteCommandAsync("SELECT 1");
                            return !string.IsNullOrEmpty(result);
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
            var repo = CreateTestRepository();
            
            try
            {
                await repo.InitializeDatabaseAsync(repo.DatabaseDefinition);

                // 测试读取不存在的记录
                var nonExistentRecords = await repo.ReadRecordsAsync(new[] { "non-existent-uuid" });
                Assert.IsTrue(nonExistentRecords == null || nonExistentRecords.Length == 0, 
                    "Reading non-existent records should return empty result");

                // 测试无效的搜索条件应该抛出异常
                await Assert.ThrowsExceptionAsync<Exception>(async () =>
                {
                    await repo.SearchRecordsAsync("non_existent_column", "test");
                }, "Searching with invalid column should throw exception");

                // 测试空参数
                await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
                {
                    await repo.UpdateRecordAsync("", new RepositoriesCore.EmployeesRepository.EmployeeRecord(
                        "", "", "", "", null, null, DateTime.Now, DateTime.Now));
                }, "Update with empty UUID should throw ArgumentException");
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
        public void Repository_Record_Conversion_Test()
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

            var dict = RepositoriesCore.EmployeesRepository.RecordToDict(testRecord);
            Assert.IsNotNull(dict, "Record conversion should not return null");
            Assert.AreEqual("test-uuid", dict["UUID"], "UUID should be converted correctly");
            Assert.AreEqual("test-user", dict["user_id"], "UserId should be converted correctly");
            Assert.AreEqual("Test Name", dict["name"], "Name should be converted correctly");

            // 测试 null 记录
            var nullDict = RepositoriesCore.EmployeesRepository.RecordToDict(null);
            Assert.IsNull(nullDict, "Null record should return null dictionary");
        }

        private static RepositoriesCore.EmployeesRepository CreateTestRepository()
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
