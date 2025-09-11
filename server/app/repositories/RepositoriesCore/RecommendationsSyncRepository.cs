using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    /// <summary>
    /// 推荐Repository同步版本，用于pythonnet兼容
    /// </summary>
    public class RecommendationsSyncRepository : IRepositorySync
    {
        private readonly RecommandationRepository _repository;

        public RecommendationsSyncRepository(string? connectionString)
        {
            _repository = new RecommandationRepository(connectionString);
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

        // 推荐特有的同步方法

        /// <summary>
        /// 根据用户ID搜索推荐
        /// </summary>
        public Dictionary<string, object?>[]? GetRecommendationsByUserId(string userId)
        {
            return SearchRecords("UserId", userId);
        }

        /// <summary>
        /// 根据用户UUID搜索推荐
        /// </summary>
        public Dictionary<string, object?>[]? GetRecommendationsByUserUUID(string userUUID)
        {
            return SearchRecords("UserUUID", userUUID);
        }

        /// <summary>
        /// 获取未推送的推荐
        /// </summary>
        public Dictionary<string, object?>[]? GetUnpushedRecommendations()
        {
            return SearchRecords("IsPushed", false);
        }

        /// <summary>
        /// 获取已推送的推荐
        /// </summary>
        public Dictionary<string, object?>[]? GetPushedRecommendations()
        {
            return SearchRecords("IsPushed", true);
        }

        /// <summary>
        /// 获取指定用户在日期范围内的推荐
        /// </summary>
        public Dictionary<string, object?>[]? GetRecommendationsInDateRange(string userId, DateTime startDate, DateTime endDate)
        {
            var typedResults = _repository.GetRecommendationsInDateRangeAsync(userId, startDate, endDate).GetAwaiter().GetResult();
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
        /// 标记推荐为已推送
        /// </summary>
        public bool MarkRecommendationAsPushed(string uuid)
        {
            var updateDict = new Dictionary<string, object?>
            {
                ["IsPushed"] = true
            };
            return UpdateRecord(uuid, updateDict);
        }

        /// <summary>
        /// 批量标记推荐为已推送
        /// </summary>
        public bool MarkRecommendationsAsPushed(string[] uuids)
        {
            bool allSuccess = true;
            foreach (var uuid in uuids)
            {
                if (!MarkRecommendationAsPushed(uuid))
                {
                    allSuccess = false;
                }
            }
            return allSuccess;
        }
    }
}