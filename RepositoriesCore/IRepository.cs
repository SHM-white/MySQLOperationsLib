using MySqlConnector;
using System.Text.Json;

namespace RepositoriesCore
{
    public partial interface IRepository
    {
        IEnumerable<ColumnDefinition> databaseDefinition { get; } // 数据库表定义
        string ConnectionString { get; set; } // 数据库连接字符串
        string SheetName { get; } // 数据库表名称
        Task<bool> DatabaseIsInitializedAsync(); // 检查数据库表是否已初始化
        Task<bool> InitializeDatabaseAsync(IEnumerable<ColumnDefinition> columns); // 初始化数据库表
        Task<MySqlConnection?> TryConnectAsync(); // 尝试连接数据库，返回 MySqlConnection 或 null
        Task<bool> AddNewRecordsAsync(Dictionary<string, object?>[] records); // 添加新记录
        Task<bool> UpdateRecordAsync(string UUID, Dictionary<string, object?> records); // 更新记录
        Task<string[]?> ReadRecordsAsync(string[] UUIDs); // 读取记录，返回 JSON 字符串数组
        Task<bool> DeleteRecordsAsync(string[] UUIDs); // 删除记录
        Task<string[]?> SearchRecordsAsync(string searchTarget, object content); // 搜索记录，返回 JSON 字符串数组
        Task<string> ExecuteCommandAsync(string commandText); // 执行自定义 SQL 命令，返回结果
    }
    public partial interface IRepository
    {
        static bool IsValidConnectionString(string connectionString)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(connectionString);
                return !string.IsNullOrWhiteSpace(builder.Server) &&
                       !string.IsNullOrWhiteSpace(builder.Database) &&
                       !string.IsNullOrWhiteSpace(builder.UserID) &&
                       !string.IsNullOrWhiteSpace(builder.Password);
            }
            catch
            {
                return false;
            }
        }
        static string GenerateConnectionString(string server, string database, string userId, string password)
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = server,
                Database = database,
                UserID = userId,
                Password = password
            };
            return builder.ConnectionString;
        }
        protected static async Task<string> SerializeToJsonAsync(Dictionary<string, object?> record)
        {
            using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, record, CachedJsonSerializerOptions);

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
