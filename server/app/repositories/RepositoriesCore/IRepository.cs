using MySqlConnector;
using System.Text.Json;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    public partial interface IRepository
    {
        List<ColumnDefinition> DatabaseDefinition { get; }
        string ConnectionString { get; set; }
        string SheetName { get; }
        // Sync versions
        void Dispose();
        IRepository Clone();
        Task<bool> DatabaseIsInitializedAsync();
        Task<bool> InitializeDatabaseAsync(IEnumerable<ColumnDefinition> columns);
        // Async versions
        Task<MySqlConnection?> TryConnectAsync();
        Task<bool> AddNewRecordsAsync(string[] records);
        Task<bool> UpdateRecordAsync(string UUID, string record);
        Task<string[]?> ReadRecordsAsync(string[] UUIDs);
        Task<bool> DeleteRecordsAsync(string[] UUIDs);
        Task<string[]?> SearchRecordsAsync(string searchTarget, object content);
        Task<string> ExecuteCommandAsync(string commandText);
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
            await JsonSerializer.SerializeAsync(stream, record, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

    }
}
