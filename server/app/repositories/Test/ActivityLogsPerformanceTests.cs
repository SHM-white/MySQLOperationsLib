using RepositoriesCore;
using System.Diagnostics;

namespace TestUtils
{
    /// <summary>
    /// ActivityLogs仓储性能测试类
    /// </summary>
    public static class ActivityLogsPerformanceTests
    {
        public static async Task RunPerformanceTests(ActivityLogsRepository repo)
        {
            Console.WriteLine("\n" + "=".PadRight(60, '='));
            Console.WriteLine("ACTIVITY LOGS PERFORMANCE TESTS");
            Console.WriteLine("=".PadRight(60, '='));

            await TestBatchInsertPerformance(repo);
            await TestBatchReadPerformance(repo);
            await TestSearchPerformance(repo);
            await TestConcurrentOperations(repo);
            
            Console.WriteLine("=".PadRight(60, '='));
            Console.WriteLine("ACTIVITY LOGS PERFORMANCE TESTS COMPLETED");
            Console.WriteLine("=".PadRight(60, '='));
        }

        private static async Task TestBatchInsertPerformance(ActivityLogsRepository repo)
        {
            Console.WriteLine("\n1. Testing activity logs batch insert performance...");
            
            var sizes = new[] { 10, 50, 100, 200 };
            
            foreach (var size in sizes)
            {
                var activityLogs = GenerateTestActivityLogs(size);
                var stopwatch = Stopwatch.StartNew();
                
                try
                {
                    var result = await repo.AddNewTypedRecordsAsync(activityLogs);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  Inserted {size} activity logs in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({size / (stopwatch.ElapsedMilliseconds / 1000.0):F2} records/sec) - Result: {result}");
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    Console.WriteLine($"  Failed to insert {size} activity logs: {ex.Message}");
                }
            }
        }

        private static async Task TestBatchReadPerformance(ActivityLogsRepository repo)
        {
            Console.WriteLine("\n2. Testing activity logs batch read performance...");
            
            try
            {
                // 首先获取所有UUID
                var allUUIDs = await GetAllActivityLogUUIDs(repo);
                if (allUUIDs.Length == 0)
                {
                    Console.WriteLine("  No activity logs to read for performance test");
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
                    
                    Console.WriteLine($"  Read {records?.Length ?? 0} activity logs in {stopwatch.ElapsedMilliseconds}ms " +
                                    $"({(records?.Length ?? 0) / (stopwatch.ElapsedMilliseconds / 1000.0):F2} records/sec)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Batch read performance test failed: {ex.Message}");
            }
        }

        private static async Task TestSearchPerformance(ActivityLogsRepository repo)
        {
            Console.WriteLine("\n3. Testing activity logs search performance...");
            
            try
            {
                var activityTypes = new[] { "sit", "stand", "walk", "meeting" };
                
                foreach (var activityType in activityTypes)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var searchResults = await repo.GetActivityLogsByTypeAsync(activityType);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  Search '{activityType}': {searchResults?.Length ?? 0} records in {stopwatch.ElapsedMilliseconds}ms");
                }

                // 测试按员工ID搜索
                var employeeIds = new[] { 1001, 1002, 1003, 1004, 1005 };
                foreach (var employeeId in employeeIds)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var employeeResults = await repo.GetActivityLogsByEmployeeIdAsync(employeeId);
                    stopwatch.Stop();
                    
                    Console.WriteLine($"  Search Employee {employeeId}: {employeeResults?.Length ?? 0} records in {stopwatch.ElapsedMilliseconds}ms");
                }

                // 测试日期范围搜索
                var now = DateTime.Now;
                var dateRangeStopwatch = Stopwatch.StartNew();
                var dateRangeResults = await repo.GetActivityLogsInDateRangeAsync(1001, now.AddDays(-1), now);
                dateRangeStopwatch.Stop();
                
                Console.WriteLine($"  Date range search: {dateRangeResults?.Length ?? 0} records in {dateRangeStopwatch.ElapsedMilliseconds}ms");

                // 测试总记录数查询
                var countStopwatch = Stopwatch.StartNew();
                var totalCount = await repo.ExecuteCommandAsync($"SELECT COUNT(*) FROM `{repo.SheetName}`");
                countStopwatch.Stop();
                
                Console.WriteLine($"  Count query: {totalCount} records in {countStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Search performance test failed: {ex.Message}");
            }
        }

        private static async Task TestConcurrentOperations(ActivityLogsRepository repo)
        {
            Console.WriteLine("\n4. Testing activity logs concurrent operations...");
            
            try
            {
                var concurrencyLevels = new[] { 5, 10, 15 };
                
                foreach (var concurrency in concurrencyLevels)
                {
                    var stopwatch = Stopwatch.StartNew();
                    var tasks = new List<Task<ActivityLogsRepository.ActivityLogRecord[]?>>();
                    
                    for (int i = 0; i < concurrency; i++)
                    {
                        var employeeId = 1001 + (i % 5); // Rotate through employee IDs
                        tasks.Add(repo.GetActivityLogsByEmployeeIdAsync(employeeId));
                    }
                    
                    var results = await Task.WhenAll(tasks);
                    stopwatch.Stop();
                    
                    var totalRecords = results.Sum(r => r?.Length ?? 0);
                    Console.WriteLine($"  {concurrency} concurrent searches: {totalRecords} records in {stopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Concurrent operations test failed: {ex.Message}");
            }
        }

        private static ActivityLogsRepository.ActivityLogRecord[] GenerateTestActivityLogs(int count)
        {
            var random = new Random();
            var activityTypes = new[] { "sit", "stand", "walk", "meeting" };
            var logs = new ActivityLogsRepository.ActivityLogRecord[count];
            
            for (int i = 0; i < count; i++)
            {
                var startTime = DateTime.Now.AddMinutes(-random.Next(0, 1440)); // Random time within last 24 hours
                var duration = random.Next(60, 3600); // 1 minute to 1 hour
                var endTime = startTime.AddSeconds(duration);
                
                logs[i] = new ActivityLogsRepository.ActivityLogRecord(
                    UUID: Guid.NewGuid().ToString(),
                    LogId: 0, // Will be auto-generated
                    EmployeeId: 1000 + random.Next(1, 100), // Employee IDs from 1001-1100
                    ActivityType: activityTypes[random.Next(activityTypes.Length)],
                    StartTime: startTime,
                    EndTime: endTime,
                    Duration: duration,
                    CreatedAt: DateTime.Now
                );
            }
            
            return logs;
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

        public static async Task RunStressTest(ActivityLogsRepository repo, int iterations = 100)
        {
            Console.WriteLine($"\n" + "=".PadRight(60, '='));
            Console.WriteLine($"ACTIVITY LOGS STRESS TEST ({iterations} iterations)");
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
                    var operation = random.Next(5);

                    switch (operation)
                    {
                        case 0: // Add
                            var logs = GenerateTestActivityLogs(random.Next(1, 5));
                            await repo.AddNewTypedRecordsAsync(logs);
                            break;

                        case 1: // Read
                            var allUUIDs = await GetAllActivityLogUUIDs(repo);
                            if (allUUIDs.Length > 0)
                            {
                                var uuidsToRead = allUUIDs.Take(random.Next(1, Math.Min(10, allUUIDs.Length))).ToArray();
                                await repo.ReadTypedRecordsAsync(uuidsToRead);
                            }
                            break;

                        case 2: // Search by type
                            var activityTypes = new[] { "sit", "stand", "walk", "meeting" };
                            var activityType = activityTypes[random.Next(activityTypes.Length)];
                            await repo.GetActivityLogsByTypeAsync(activityType);
                            break;

                        case 3: // Search by employee
                            var employeeId = 1000 + random.Next(1, 100);
                            await repo.GetActivityLogsByEmployeeIdAsync(employeeId);
                            break;

                        case 4: // Count
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
            Console.WriteLine($"\nActivity logs stress test completed:");
            Console.WriteLine($"  Total time: {overallStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Successful operations: {successCount}/{iterations} ({100.0 * successCount / iterations:F1}%)");
            Console.WriteLine($"  Failed operations: {failureCount}");
            Console.WriteLine($"  Average time per operation: {overallStopwatch.ElapsedMilliseconds / iterations:F2}ms");
        }
    }
}