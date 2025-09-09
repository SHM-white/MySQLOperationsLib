using System.Text.Json;

namespace RepositoriesCore
{
    public partial class EmployeesRepository : RepositoryManagerBase
    {
        public EmployeesRepository(string? connectionString) : base(connectionString, "Employees")
        {
        }
        public override List<ColumnDefinition> DatabaseDefinition => _databaseDefinition;

        public new async Task<EmployeeRecord[]?> ReadRecordsAsync(string[] UUIDs)
        {
            var result = await base.ReadRecordsAsync(UUIDs);
            if (result == null || result.Length == 0)
                return null;
                
            var employeeRecords = new List<EmployeeRecord>();
            foreach (var item in result)
            {
                try
                {
                    // 先反序列化为字典
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(item);
                    if (dict != null)
                    {
                        // 手动映射到 EmployeeRecord
                        var employee = DictToRecord(dict);
                        if (employee != null)
                        {
                            employeeRecords.Add(employee);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new JsonException($"Failed to deserialize record: {item}", ex);
                }
            }
            return employeeRecords.ToArray();
        }
        
        public async Task<bool> AddNewRecordsAsync(EmployeeRecord[] records)
        {
            var recordsDictionary = records.Select(
                r => RecordToDict(r) ?? throw new ArgumentNullException(nameof(r), "Record cannot be null.")
                ).ToArray() ?? Array.Empty<Dictionary<string, object?>>();
            return await AddNewRecordsAsync(recordsDictionary);
        }

        public async Task<bool> UpdateRecordAsync(string UUID, EmployeeRecord record)
        {
            var recordDictionary = RecordToDict(record);
            if (recordDictionary == null)
                return false;
            return await UpdateRecordAsync(UUID, recordDictionary);
        }

        public new async Task<EmployeeRecord[]?> SearchRecordsAsync(string searchTarget, object content)
        {
            var result = await base.SearchRecordsAsync(searchTarget, content);
            if (result != null && result.Length > 0)
            {
                var employeeRecords = new List<EmployeeRecord>();
                foreach (var item in result)
                {
                    try
                    {
                        // 先反序列化为字典
                        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(item);
                        if (dict != null)
                        {
                            // 手动映射到 EmployeeRecord
                            var employee = DictToRecord(dict);
                            if (employee != null)
                            {
                                employeeRecords.Add(employee);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        throw new JsonException($"Failed to deserialize record: {item}", ex);
                    }
                }
                return employeeRecords.ToArray();
            }
            return null;
        }
    }
    
    public partial class EmployeesRepository : RepositoryManagerBase
    {
        public record EmployeeRecord(
            string UUID,
            string UserId,
            string Name,
            string Department,
            string? WorkstationId,
            string? Preference,
            DateTime CreatedAt,
            DateTime UpdatedAt
        );
        private readonly List<ColumnDefinition> _databaseDefinition = new List<ColumnDefinition>
        {
            new (Name:"UUID", Type:DbColumnType.String, Length:36, IsPrimaryKey:true, IsNullable:false, Comment:"UUID，主键"),
            new (Name:"user_id", Type:DbColumnType.String, Length:36, IsNullable:false, Comment:"用户ID"),
            new (Name:"name", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"员工姓名"),
            new (Name:"department", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"所在部门"),
            new (Name:"workstation_id", Type:DbColumnType.String, Length:20, DefaultValue:null, Comment:"工位编号"),
            new (Name:"preference", Type:DbColumnType.Text, DefaultValue:null, Comment:"健康偏好设置(JSON格式)"),
            new (Name:"created_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP", Comment:"创建时间"),
            new (Name:"updated_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP", Comment:"更新时间")
        };
        public static Dictionary<string, object?>? RecordToDict(EmployeeRecord? record)
        {
            if (record == null) return null;
            return new Dictionary<string, object?>
            {
                { "UUID", record.UUID },
                { "user_id", record.UserId },
                { "name", record.Name },
                { "department", record.Department },
                { "workstation_id", record.WorkstationId },
                { "preference", record.Preference },
                { "created_at", record.CreatedAt },
                { "updated_at", record.UpdatedAt }
            };
        }

        public static EmployeeRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            if (dict == null) return null;
            
            try
            {
                // 安全地获取值并转换类型
                var uuid = GetStringValue(dict, "UUID") ?? "";
                var userId = GetStringValue(dict, "user_id") ?? "";
                var name = GetStringValue(dict, "name") ?? "";
                var department = GetStringValue(dict, "department") ?? "";
                var workstationId = GetStringValue(dict, "workstation_id");
                var preference = GetStringValue(dict, "preference");
                var createdAt = GetDateTimeValue(dict, "created_at") ?? DateTime.Now;
                var updatedAt = GetDateTimeValue(dict, "updated_at") ?? DateTime.Now;
                
                return new EmployeeRecord(uuid, userId, name, department, workstationId, preference, createdAt, updatedAt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert dictionary to EmployeeRecord: {ex.Message}", ex);
            }
        }

        private static string? GetStringValue(Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }
            return null;
        }

        private static DateTime? GetDateTimeValue(Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                if (value is DateTime dateTime)
                {
                    return dateTime;
                }
                if (DateTime.TryParse(value.ToString(), out var parsedDateTime))
                {
                    return parsedDateTime;
                }
            }
            return null;
        }
    }
}
