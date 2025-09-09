using System.Text.Json;
using static RepositoriesCore.ActivityLogsRepository;

namespace RepositoriesCore
{
    public class ActivityLogsRepository(string? connectionString) : RepositoryManagerBase<ActivityLogsRepository.ActivityLogRecord>(connectionString, "activity_logs")
    {
        public override IEnumerable<ColumnDefinition> DatabaseDefinition => _databaseDefinition;

        // Implement the abstract methods for type conversion
        public override Dictionary<string, object?>? RecordToDict(ActivityLogRecord? record)
        {
            return record.RecordToDict();
        }

        public override ActivityLogRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            return dict.DictToActivityLogsRecord();
        }

        // Additional convenience methods specific to activity logs
        
        /// <summary>
        /// Search activity logs by employee ID
        /// </summary>
        public async Task<ActivityLogRecord[]?> GetActivityLogsByEmployeeIdAsync(int employeeId)
        {
            return await SearchTypedRecordsAsync("employee_id", employeeId);
        }

        /// <summary>
        /// Search activity logs by activity type
        /// </summary>
        public async Task<ActivityLogRecord[]?> GetActivityLogsByTypeAsync(string activityType)
        {
            return await SearchTypedRecordsAsync("activity_type", activityType);
        }

        /// <summary>
        /// Get activity logs within a date range for a specific employee
        /// </summary>
        public async Task<ActivityLogRecord[]?> GetActivityLogsInDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            
            var sql = $@"SELECT * FROM `{SheetName}` 
                        WHERE `employee_id` = @employeeId 
                        AND `start_time` >= @startDate 
                        AND `end_time` <= @endDate 
                        ORDER BY `start_time`";

            using var cmd = new MySqlConnector.MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@employeeId", employeeId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<ActivityLogRecord>();

            while (await reader.ReadAsync())
            {
                var record = new Dictionary<string, object?>();
                foreach (var col in DatabaseDefinition)
                {
                    record[col.Name] = reader[col.Name] is DBNull ? null : reader[col.Name];
                }

                var activityLog = DictToRecord(record);
                if (activityLog != null)
                {
                    results.Add(activityLog);
                }
            }

            return [.. results];
        }

        // Activity log record definition
        public record ActivityLogRecord(
            string UUID,
            int LogId,
            int EmployeeId,
            string ActivityType,
            DateTime StartTime,
            DateTime EndTime,
            int Duration,
            DateTime CreatedAt
        );
        
        // Database definition based on the provided table structure
        private readonly IEnumerable<ColumnDefinition> _databaseDefinition =
        [
            new (Name:"UUID", Type:DbColumnType.String, Length:255, IsPrimaryKey:true, IsNullable:false, Comment:"记录uuid，主键"),
            new (Name:"log_id", Type:DbColumnType.Int32, IsNullable:false, AutoIncrement:true, Comment:"记录ID"),
            new (Name:"employee_id", Type:DbColumnType.Int32, IsNullable:false, IsIndexed:true, Comment:"员工ID，外键关联employees表"),
            new (Name:"activity_type", Type:DbColumnType.String, Length:20, IsNullable:false, IsIndexed:true, Comment:"活动类型(sit/stand/walk/meeting)"),
            new (Name:"start_time", Type:DbColumnType.DateTime, IsNullable:false, IsIndexed:true, Comment:"活动开始时间"),
            new (Name:"end_time", Type:DbColumnType.DateTime, IsNullable:false, Comment:"活动结束时间"),
            new (Name:"duration", Type:DbColumnType.Int32, IsNullable:false, Comment:"活动持续时间(秒)"),
            new (Name:"created_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP", Comment:"创建时间")
        ];
    }
}