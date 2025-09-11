using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    public class RecommandationRepository : RepositoryManagerBase<RecommandationRepository.RecommandationRecord>
    {
        public RecommandationRepository(string? connectionString) : base(connectionString, "Recommandation")
        {
        }

        public override IEnumerable<ColumnDefinition> databaseDefinition => DatabaseDefinition;

        public override RecommandationRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            return dict.DictToRecord<RecommandationRecord>();
        }

        public override Dictionary<string, object?>? RecordToDict(RecommandationRecord? record)
        {
            return record.RecordToDict();
        }

        /// <summary>
        /// Search recommendations by user ID
        /// </summary>
        public async Task<RecommandationRecord[]?> GetRecommendationsByUserIdAsync(string userId)
        {
            return await SearchTypedRecordsAsync("UserId", userId);
        }

        /// <summary>
        /// Search recommendations by user UUID
        /// </summary>
        public async Task<RecommandationRecord[]?> GetRecommendationsByUserUUIDAsync(string userUUID)
        {
            return await SearchTypedRecordsAsync("UserUUID", userUUID);
        }

        /// <summary>
        /// Get unpushed recommendations
        /// </summary>
        public async Task<RecommandationRecord[]?> GetUnpushedRecommendationsAsync()
        {
            return await SearchTypedRecordsAsync("IsPushed", false);
        }

        /// <summary>
        /// Get pushed recommendations
        /// </summary>
        public async Task<RecommandationRecord[]?> GetPushedRecommendationsAsync()
        {
            return await SearchTypedRecordsAsync("IsPushed", true);
        }

        /// <summary>
        /// Get recommendations within a date range for a specific user
        /// </summary>
        public async Task<RecommandationRecord[]?> GetRecommendationsInDateRangeAsync(string userId, DateTime startDate, DateTime endDate)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            
            var sql = $@"SELECT * FROM `{SheetName}` 
                        WHERE `UserId` = @userId 
                        AND `CreateTime` >= @startDate 
                        AND `CreateTime` <= @endDate 
                        ORDER BY `CreateTime` DESC";

            using var cmd = new MySqlConnector.MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<RecommandationRecord>();

            while (await reader.ReadAsync())
            {
                var record = new Dictionary<string, object?>();
                foreach (var col in databaseDefinition)
                {
                    record[col.Name] = reader[col.Name] is DBNull ? null : reader[col.Name];
                }

                var recommendation = DictToRecord(record);
                if (recommendation != null)
                {
                    results.Add(recommendation);
                }
            }

            return results.ToArray();
        }

        public record RecommandationRecord(
            string UUID,
            string UserId,
            string UserUUID,
            DateTime CreateTime,
            bool IsPushed,
            string Content
        );
        
        public static readonly IEnumerable<ColumnDefinition> DatabaseDefinition = new ColumnDefinition[]
        {
            new ColumnDefinition(Name:"UUID", Type:DbColumnType.Guid, IsPrimaryKey:true, IsNullable:false, IsUnique:true, Comment:"UUID，主键"),
            new ColumnDefinition(Name:"UserId", Type:DbColumnType.String, Length:50, IsPrimaryKey:false, IsNullable:false, IsUnique:false, IsIndexed:true, Comment:"用户ID"),
            new ColumnDefinition(Name:"UserUUID", Type:DbColumnType.Guid, IsPrimaryKey:false, IsNullable:false, IsUnique:false, IsIndexed:true, Comment:"用户UUID"),
            new ColumnDefinition(Name:"CreateTime", Type:DbColumnType.DateTime, IsPrimaryKey:false, IsNullable:false, IsUnique:false, DefaultValue:"CURRENT_TIMESTAMP", IsIndexed:true, Comment:"创建时间"),
            new ColumnDefinition(Name:"IsPushed", Type:DbColumnType.Boolean, IsPrimaryKey:false, IsNullable:false, IsUnique:false, IsIndexed:true, DefaultValue:"0", Comment:"是否推送"),
            new ColumnDefinition(Name:"Content", Type:DbColumnType.Text, IsPrimaryKey:false, IsNullable:false, IsUnique:false, Comment:"推荐内容")
        };
    }
}
