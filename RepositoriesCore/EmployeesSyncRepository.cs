using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    /// <summary>
    /// 员工Repository同步版本，用于pythonnet兼容
    /// </summary>
    public class EmployeesSyncRepository : IRepositorySync
    {
        private readonly EmployeesRepository _repository;

        public EmployeesSyncRepository(string? connectionString)
        {
            _repository = new EmployeesRepository(connectionString);
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

        // 员工特有的同步方法

        /// <summary>
        /// 根据用户ID搜索员工
        /// </summary>
        public Dictionary<string, object?>[]? GetEmployeesByUserId(string userId)
        {
            return SearchRecords("UserId", userId);
        }

        /// <summary>
        /// 根据部门搜索员工
        /// </summary>
        public Dictionary<string, object?>[]? GetEmployeesByDepartment(string department)
        {
            return SearchRecords("Department", department);
        }

        /// <summary>
        /// 获取在线员工
        /// </summary>
        public Dictionary<string, object?>[]? GetOnlineEmployees()
        {
            return SearchRecords("Online", true);
        }

        /// <summary>
        /// 获取离线员工
        /// </summary>
        public Dictionary<string, object?>[]? GetOfflineEmployees()
        {
            return SearchRecords("Online", false);
        }
    }
}