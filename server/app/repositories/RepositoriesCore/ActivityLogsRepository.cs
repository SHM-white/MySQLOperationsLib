using System.Text.Json;
using static RepositoriesCore.ActivityLogsRepository;

namespace RepositoriesCore
{
    public class ActivityLogsRepository(string? connectionString) : RepositoryManagerBase<ActivityLogsRepository.ActivityLogRecord>(connectionString, "ActivityLogs")
    {
        public override IEnumerable<ColumnDefinition> databaseDefinition => DatabaseDefinition;

        // Implement the abstract methods for type conversion
        public override Dictionary<string, object?>? RecordToDict(ActivityLogRecord? record)
        {
            return record.RecordToDict();
        }

        public override ActivityLogRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            return dict.DictToRecord<ActivityLogRecord>();
        }

        // Additional convenience methods specific to activity logs

        /// <summary>
        /// Search activity logs by user ID
        /// </summary>
        public async Task<ActivityLogRecord[]?> GetActivityLogsByUserIdAsync(string userId)
        {
            return await SearchTypedRecordsAsync("UserId", userId);
        }

        /// <summary>
        /// Search activity logs by activity type
        /// </summary>
        public async Task<ActivityLogRecord[]?> GetActivityLogsByTypeAsync(string activityType)
        {
            return await SearchTypedRecordsAsync("ActivityType", activityType);
        }

        /// <summary>
        /// Get activity logs within a date range for a specific user
        /// </summary>
        public async Task<ActivityLogRecord[]?> GetActivityLogsInDateRangeAsync(string userId, DateTime startDate, DateTime endDate)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            
            var sql = $@"SELECT * FROM `{SheetName}` 
                        WHERE `UserId` = @userId 
                        AND `StartTime` >= @startDate 
                        AND `EndTime` <= @endDate 
                        ORDER BY `StartTime`";

            using var cmd = new MySqlConnector.MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<ActivityLogRecord>();

            while (await reader.ReadAsync())
            {
                var record = new Dictionary<string, object?>();
                foreach (var col in databaseDefinition)
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
            string UserId,
            string UserUUID,
            string ActivityType,    
            string DetailInformation,
            DateTime StartTime,
            DateTime EndTime,
            int Duration,
            DateTime CreatedAt
        );
        
        // Database definition
        public static readonly IEnumerable<ColumnDefinition> DatabaseDefinition =
        [
            new (Name:"UUID", Type:DbColumnType.Guid, IsPrimaryKey:true, IsNullable:false, IsUnique:true, Comment:"记录uuid，主键"),
            new (Name:"UserId", Type:DbColumnType.String, Length:50, IsNullable:false, IsIndexed:true, Comment:"员工ID，外键关联employees表"),
            new (Name:"UserUUID", Type:DbColumnType.Guid, IsNullable:false, IsIndexed:true, Comment:"员工UUID，外键关联employees表"),
            new (Name:"ActivityType", Type:DbColumnType.String, Length:20, IsNullable:false, IsIndexed:true, Comment:"活动类型"),
            new (Name:"DetailInformation", Type:DbColumnType.Json, DefaultValue:null, Comment:"活动详情信息(JSON格式)"),
            new (Name:"StartTime", Type:DbColumnType.DateTime, IsNullable:false, IsIndexed:true, Comment:"活动开始时间"),
            new (Name:"EndTime", Type:DbColumnType.DateTime, IsNullable:false, Comment:"活动结束时间"),
            new (Name:"Duration", Type:DbColumnType.Int32, IsNullable:false, DefaultValue:"0", Comment:"活动持续时间(秒)"),
            new (Name:"CreatedAt", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP", IsIndexed:true, Comment:"创建时间")
        ];
    }
}