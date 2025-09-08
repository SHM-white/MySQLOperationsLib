using MySqlConnector;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    public interface IRepository
    {
        string ConnectionString { get; set; }
        string SheetName { get; }
        bool IsConnected();
        bool Connect(string ConnectionString);
        bool Connect();
        Task<bool> ConnectAsync(string connectionString);
        Task<bool> ConnectAsync();
        Task<bool> TryConnectAsync();
        void Disconnect();
        void Dispose();
        IRepository Clone();
        bool CreateRecords(string[] records);
        bool UpdateRecord(string UUID, string record);
        string[]? ReadRecords(string[] UUIDs);
        bool DeleteRecords(string[] UUIDs);
        string[]? SearchRecordsByUserId(string userId);
        bool DatabaseIsInitialized();
        bool InitializeDatabase(IEnumerable<ColumnDefinition> columns);
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
