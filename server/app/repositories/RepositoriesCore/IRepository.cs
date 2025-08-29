using Microsoft.Data.SqlClient;

namespace RepositoriesCore
{
    public interface IRepository
    {
        string ConnectionString { get; set; }
        bool IsConnected();
        bool Connect(string ConnectionString);
        bool Connect();
        void Disconnect();
        void Dispose();
        IRepository Clone();
        bool CreateRecords(string[] records);
        bool UpdateRecord(string UUID, string record);
        string[]? ReadRecords(string[] UUIDs);
        bool DeleteRecords(string[] UUIDs);
        string[]? SearchRecordsByUserId(string userId);
        bool DatabaseIsInitialized((string type, string name)[] properties);
        bool InitializeDatabase();
        static bool IsValidConnectionString(string connectionString)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(connectionString);
                return !string.IsNullOrWhiteSpace(builder.DataSource) &&
                       !string.IsNullOrWhiteSpace(builder.InitialCatalog) &&
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
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = server,
                InitialCatalog = database,
                UserID = userId,
                Password = password,
                Encrypt = true,
                TrustServerCertificate = true,
                ConnectTimeout = 30,
                MultipleActiveResultSets = true
            };
            return builder.ConnectionString;
        }
    }
}
