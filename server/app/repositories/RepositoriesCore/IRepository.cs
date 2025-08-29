using Microsoft.Data.SqlClient;

namespace RepositoriesCore
{
    public interface IRepository
    {
        string ConnectionString
        {
            get; set;
        }
        public abstract bool IsConnected();
        public abstract bool Connect(string ConnectionString);
        public abstract void Disconnect();
        public abstract void Dispose();
        public abstract IRepository Clone();
        public abstract bool CreateRecords(string[] records);
        public abstract bool UpdateRecord(string UUID, string record);
        public abstract string[]? ReadRecords(string[] UUIDs);
        public abstract bool DeleteRecords(string[] UUIDs);
        public abstract string[]? SearchRecordsByUserId(string userId);
        public abstract bool DatabaseIsInitialized((string type, string name)[] properties);
        public abstract bool InitializeDatabase();
        public static string GenerateConnectionString(string server, string database, string userId, string password)
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
