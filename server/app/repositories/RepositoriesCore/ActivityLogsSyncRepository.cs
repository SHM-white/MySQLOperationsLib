using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    /// <summary>
    /// 活动日志Repository同步版本，用于pythonnet兼容
    /// </summary>
    public class ActivityLogsSyncRepository : IRepositorySync
    {
        private readonly ActivityLogsRepository _repository;

        public ActivityLogsSyncRepository(string? connectionString)
        {
            _repository = new ActivityLogsRepository(connectionString);
        }

        public string ConnectionString 
        { 
            get => _repository.ConnectionString; 
            set => _repository.ConnectionString = value; 
        }

        public string SheetName => _repository.SheetName;

        public bool InitializeDatabase()
        {
            return _repository.InitializeDatabaseAsync(_repository.databaseDefinition).GetAwaiter().GetResult();
        }

        public bool DatabaseIsInitialized()
        {
            return _repository.DatabaseIsInitializedAsync().GetAwaiter().GetResult();
        }

        public bool AddNewRecords(Dictionary<string, object?>[] records)
        {
            return _repository.AddNewRecordsAsync(records).GetAwaiter().GetResult();
        }

        public Dictionary<string, object?>[]? ReadRecords(string[] uuids)
        {
            var jsonResults = _repository.ReadRecordsAsync(uuids).GetAwaiter().GetResult();
            if (jsonResults == null || jsonResults.Length == 0)
                return null;

            var results = new List<Dictionary<string, object?>>();
            foreach (var jsonItem in jsonResults)
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonItem);
                    if (dict != null)
                    {
                        results.Add(dict);
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // 忽略无效的JSON记录
                    continue;
                }
            }
            return results.ToArray();
        }

        public bool DeleteRecords(string[] uuids)
        {
            return _repository.DeleteRecordsAsync(uuids).GetAwaiter().GetResult();
        }

        public bool UpdateRecord(string uuid, Dictionary<string, object?> record)
        {
            return _repository.UpdateRecordAsync(uuid, record).GetAwaiter().GetResult();
        }

        public Dictionary<string, object?>[]? SearchRecords(string searchTarget, object content)
        {
            var jsonResults = _repository.SearchRecordsAsync(searchTarget, content).GetAwaiter().GetResult();
            if (jsonResults == null || jsonResults.Length == 0)
                return null;

            var results = new List<Dictionary<string, object?>>();
            foreach (var jsonItem in jsonResults)
            {
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonItem);
                    if (dict != null)
                    {
                        results.Add(dict);
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // 忽略无效的JSON记录
                    continue;
                }
            }
            return results.ToArray();
        }

        public string ExecuteCommand(string commandText)
        {
            return _repository.ExecuteCommandAsync(commandText).GetAwaiter().GetResult();
        }

        // 活动日志特有的同步方法

        /// <summary>
        /// 根据用户ID搜索活动日志
        /// </summary>
        public Dictionary<string, object?>[]? GetActivityLogsByUserId(string userId)
        {
            return SearchRecords("UserId", userId);
        }

        /// <summary>
        /// 根据活动类型搜索活动日志
        /// </summary>
        public Dictionary<string, object?>[]? GetActivityLogsByType(string activityType)
        {
            return SearchRecords("ActivityType", activityType);
        }

        /// <summary>
        /// 获取指定用户在日期范围内的活动日志
        /// </summary>
        public Dictionary<string, object?>[]? GetActivityLogsInDateRange(string userId, DateTime startDate, DateTime endDate)
        {
            var typedResults = _repository.GetActivityLogsInDateRangeAsync(userId, startDate, endDate).GetAwaiter().GetResult();
            if (typedResults == null || typedResults.Length == 0)
                return null;

            var results = new List<Dictionary<string, object?>>();
            foreach (var record in typedResults)
            {
                var dict = _repository.RecordToDict(record);
                if (dict != null)
                {
                    results.Add(dict);
                }
            }
            return results.ToArray();
        }

        /// <summary>
        /// 获取指定时间范围内的所有活动日志
        /// </summary>
        public Dictionary<string, object?>[]? GetActivityLogsByTimeRange(DateTime startTime, DateTime endTime)
        {
            // 使用自定义SQL查询
            var sql = $@"SELECT * FROM `{SheetName}` 
                        WHERE `StartTime` >= '{startTime:yyyy-MM-dd HH:mm:ss}' 
                        AND `EndTime` <= '{endTime:yyyy-MM-dd HH:mm:ss}' 
                        ORDER BY `StartTime`";
            
            try
            {
                // 这里需要直接调用底层数据库查询方法
                // 为简化，我们使用SearchRecords的组合来模拟
                // 在实际使用中，可能需要扩展ExecuteCommand来支持返回多行数据
                return null; // 暂时返回null，需要根据实际需求实现
            }
            catch
            {
                return null;
            }
        }
    }
}