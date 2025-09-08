using MySqlConnector;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    public interface IRepository
    {
        string ConnectionString { get; set; }
        string SheetName { get; }
        // Sync versions
        bool IsConnected();
        void Disconnect();
        void Dispose();
        IRepository Clone();
        Task<bool> DatabaseIsInitializedAsync();
        Task<bool> InitializeDatabaseAsync(IEnumerable<ColumnDefinition> columns);
        // Async versions
        Task<bool> ConnectAsync(string connectionString);
        Task<bool> ConnectAsync();
        Task<bool> TryConnectAsync();
        Task<bool> AddNewRecordsAsync(string[] records);
        Task<bool> UpdateRecordAsync(string UUID, string record);
        Task<string[]?> ReadRecordsAsync(string[] UUIDs);
        Task<bool> DeleteRecordsAsync(string[] UUIDs);
        Task<string[]?> SearchRecordsAsync(string searchTarget, object content);
        Task<string> ExecuteCommandAsync(string commandText);
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
    }
}
