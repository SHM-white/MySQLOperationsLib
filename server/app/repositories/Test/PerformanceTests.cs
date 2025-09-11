using RepositoriesCore;
using System.Diagnostics;

namespace TestUtils
{
    /// <summary>
    /// 性能测试类，用于测试大量数据操作的性能
    /// </summary>
    public static class PerformanceTests
    {
        public static async Task RunPerformanceTests(EmployeesRepository repo)
        {
            Console.WriteLine("\n" + "=".PadRight(60, '='));
            Console.WriteLine("EMPLOYEES PERFORMANCE TESTS");
            Console.WriteLine("=".PadRight(60, '='));

            await TestBatchInsertPerformance(repo);
            await TestBatchReadPerformance(repo);
            await TestConcurrentReadPerformance(repo);
            await TestLargeDatasetOperations(repo);
            
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("EMPLOYEES PERFORMANCE TESTS COMPLETED");
            Console.WriteLine("=".PadRight(60, '='));
        }

        private static async Task TestBatchInsertPerformance(EmployeesRepository repo)
        {
            Console.WriteLine("\n1. Testing batch insert performance...");
            
            var sizes = new[] { 10, 50, 100, 500 };
            
            foreach (var size in sizes)
            {
                var employees = GenerateTestEmployees(size);
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    var result = await repo.AddNewTypedRecordsAsync(employees);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  Inserted {size} records in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({size / (stopwatch.ElapsedMilliseconds / 1000.0):F2} records/sec) - Result: {result}");
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Console.WriteLine($"  Failed to insert {size} records: {ex.Message}");
                }
            }
        }

        private static async Task TestBatchReadPerformance(EmployeesRepository repo)
        {
            Console.WriteLine("\n2. Testing batch read performance...");
            
            try
            {
                // 首先获取所有UUID
                var allUUIDs = await GetAllEmployeeUUIDs(repo);
                if (allUUIDs.Length == 0)
                {
                    Console.WriteLine("  No records to read for performance test");
                    return;
                }

                var sizes = new[] { 
                    Math.Min(10, allUUIDs.Length), 
                    Math.Min(50, allUUIDs.Length), 
                    Math.Min(100, allUUIDs.Length) 
                }.Where(s => s > 0).ToArray();

                foreach (var size in sizes)
                {
                    var uuidsToRead = allUUIDs.Take(size).ToArray();
                    var stopwatch = Stopwatch.StartNew();
                    
                    var records = await repo.ReadTypedRecordsAsync(uuidsToRead);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  Read {records?.Length ?? 0} records in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({(records?.Length ?? 0) / (stopwatch.ElapsedMilliseconds / 1000.0):F2} records/sec)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Batch read performance test failed: {ex.Message}");
            }
        }

        private static async Task TestConcurrentReadPerformance(EmployeesRepository repo)
        {
            Console.WriteLine("\n3. Testing concurrent read performance...");
            
            try
            {
                var allUUIDs = await GetAllEmployeeUUIDs(repo);
                if (allUUIDs.Length == 0)
                {
                    Console.WriteLine("  No records for concurrent read test");
                    return;
                }

                var concurrencyLevels = new[] { 5, 10, 20 };
                
                foreach (var concurrency in concurrencyLevels)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var tasks = new List<Task<EmployeesRepository.EmployeeRecord[]?>>();
                    
                    for (int i = 0; i < concurrency; i++)
                    {
                        var uuidsSubset = allUUIDs.Skip(i).Take(Math.Max(1, allUUIDs.Length / concurrency)).ToArray();
                        if (uuidsSubset.Length > 0)
                        {
                            tasks.Add(repo.ReadTypedRecordsAsync(uuidsSubset));
                        }
                    }
                    
                    var results = await Task.WhenAll(tasks);
                    stopwatch.Stop();
                    
                    var totalRecords = results.Sum(r => r?.Length ?? 0);
                    Console.WriteLine($"  {concurrency} concurrent reads: {totalRecords} records in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({totalRecords / (stopwatch.ElapsedMilliseconds / 1000.0):F2} records/sec)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Concurrent read performance test failed: {ex.Message}");
            }
        }

        private static async Task TestLargeDatasetOperations(EmployeesRepository repo)
        {
            Console.WriteLine("\n4. Testing large dataset operations...");
            
            try
            {
                // 测试大量搜索操作
                var departments = new[] { "Engineering", "HR", "Marketing", "Sales", "Finance" };
                
                foreach (var dept in departments)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var searchResults = await repo.SearchTypedRecordsAsync("department", dept);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  Search '{dept}': {searchResults?.Length ?? 0} records in {stopwatch.ElapsedMilliseconds}ms");
                }

                // 测试总记录数查询
                var countStopwatch = Stopwatch.StartNew();
                var totalCount = await repo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{repo.SheetName}`");
                countStopwatch.Stop();
                
                Console.WriteLine($"  Count query: {totalCount} records in {countStopwatch.ElapsedMilliseconds}ms");

                // 测试复杂查询
                var complexQueryStopwatch = Stopwatch.StartNew();
                var complexResult = await repo.ExecuteCommandAsync(
                    $"SELECT department, COUNT(*) as count FROM `{repo.SheetName}` GROUP BY department ORDER BY count DESC LIMIT 5");
                complexQueryStopwatch.Stop();
                
                Console.WriteLine($"  Complex group query in {complexQueryStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Large dataset operations test failed: {ex.Message}");
            }
        }

        private static EmployeesRepository.EmployeeRecord[] GenerateTestEmployees(int count)
        {
            var random = new Random();
            var departments = new[] { "Engineering", "HR", "Marketing", "Sales", "Finance" };
            var employees = new EmployeesRepository.EmployeeRecord[count];
            
            for (int i = 0; i < count; i++)
            {
                var now = DateTime.Now.AddMinutes(-random.Next(0, 10000)); // 随机时间
                employees[i] = new EmployeesRepository.EmployeeRecord(
                    UUID: Guid.NewGuid().ToString(),
                    UserId: $"PERF{i:D6}",
                    Name: $"Performance Test Employee {i}",
                    Department: departments[random.Next(departments.Length)],
                    WorkstationId: random.Next(2) == 0 ? $"WS-PERF-{i:D3}" : null,
                    Preference: random.Next(2) == 0 ? $$$"""{"theme": "{{{(random.Next(2) == 0 ? "dark" : "light")}}}", "notifications": {{{(random.Next(2) == 0 ? "true" : "false")}}}""" : null,
                    Online: false,
                    CreatedAt: now,
                    UpdatedAt: now
                );
            }
            
            return employees;
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

        public static async Task RunStressTest(EmployeesRepository repo, int iterations = 100)
        {
            Console.WriteLine($"\n" + "=".PadRight(60, '='));
            Console.WriteLine($"EMPLOYEES STRESS TEST ({iterations} iterations)");
            Console.WriteLine("=".PadRight(60, '='));

            var overallStopwatch = Stopwatch.StartNew();
            var successCount = 0;
            var failureCount = 0;

            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    // 随机选择操作
                    var random = new Random();
                    var operation = random.Next(4);

                    switch (operation)
                    {
                        case 0: // Add
                            var employees = GenerateTestEmployees(random.Next(1, 5));
                            await repo.AddNewTypedRecordsAsync(employees);
                            break;

                        case 1: // Read
                            var allUUIDs = await GetAllEmployeeUUIDs(repo);
                            if (allUUIDs.Length > 0)
                            {
                                var uuidsToRead = allUUIDs.Take(random.Next(1, Math.Min(10, allUUIDs.Length))).ToArray();
                                await repo.ReadTypedRecordsAsync(uuidsToRead);
                            }
                            break;

                        case 2: // Search
                            var departments = new[] { "Engineering", "HR", "Marketing", "Sales", "Finance" };
                            var dept = departments[random.Next(departments.Length)];
                            await repo.SearchTypedRecordsAsync("department", dept);
                            break;

                        case 3: // Count
                            await repo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{repo.SheetName}`");
                            break;
                    }

                    successCount++;
                    if ((i + 1) % 20 == 0)
                    {
                        Console.WriteLine($"  Completed {i + 1}/{iterations} operations...");
                    }
                }
                catch (Exception ex)
                {
                    failureCount++;
                    if (failureCount <= 5) // 只显示前5个错误
                    {
                        Console.WriteLine($"  Operation {i + 1} failed: {ex.Message}");
                    }
                }
            }

            overallStopwatch.Stop();
            Console.WriteLine($"\nEmployees stress test completed:");
            Console.WriteLine($"  Total time: {overallStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Successful operations: {successCount}/{iterations} ({100.0 * successCount / iterations:F1}%)");
            Console.WriteLine($"  Failed operations: {failureCount}");
            Console.WriteLine($"  Average time per operation: {overallStopwatch.ElapsedMilliseconds / iterations:F2}ms");
        }
    }
}