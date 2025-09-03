using Microsoft.Identity.Client;
using MySqlConnector;
using RepositoriesCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace RepositoriesCore
{
    public abstract class RepositoryManagerBase : IRepository, IDisposable
    {
        protected RepositoryManagerBase(string? connectionString, string sheetName)
        {
            _connectionString = connectionString ?? string.Empty;
            _sheetName = sheetName;
            Connect();
        }
        ~RepositoryManagerBase()
        {
            Dispose(false);
        }
        private string _connectionString;
        public string ConnectionString {
            get => _connectionString;
            set {
                if (IRepository.IsValidConnectionString(value))
                {
                    _connectionString = value;
                }
                else throw new ArgumentException($"Connection string is illegal: {value}");
            }
        }
        protected MySqlConnection? _connection;
        protected MySqlConnection? Connection => _connection;
        protected string _sheetName;
        public string SheetName => _sheetName;
        protected Dictionary<string, string> _properties = new Dictionary<string, string>();
        public Dictionary<string, string> Properties { 
            get => _properties; 
            set => _properties = value; 
        }
        public virtual IRepository Clone()
        {
            return (IRepository)MemberwiseClone();
        }

        public virtual bool Connect(string ConnectionString)
        {
            this.ConnectionString = ConnectionString;
            return Connect();
        }
        public virtual bool Connect()
        {
            if (_connection is null && !string.IsNullOrEmpty(ConnectionString))
            {
                _connection = new MySqlConnection(ConnectionString);
                _connection.Open();
                return true;
            }
            else if (_connection is not null && _connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
                return true;
            }
            return false;
        }
        public virtual void Disconnect()
        {
            if (_connection is not null)
            {
                _connection.Close();
                _connection.Dispose();
                _connection = null;
            }
        }
        // Dispose pattern
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // managed
                Disconnect();
            }
            else
            {
                // finalizer path
                try { Disconnect(); } catch { }
            }
        }

        public virtual bool InitializeDatabase(Dictionary<string, string> properties)
        {

            return true;
        }

        public virtual bool IsConnected()
        {
            return _connection is not null && _connection.State == System.Data.ConnectionState.Open;
        }

        public abstract string[]? ReadRecords(string[] UUIDs);

        public abstract string[]? SearchRecordsByUserId(string userId);

        public abstract bool UpdateRecord(string UUID, string record);

        public abstract bool CreateRecords(string[] records);

        public abstract bool DeleteRecords(string[] UUIDs);

        public virtual bool DatabaseIsInitialized()
        {
            // 校验基础参数
            if (string.IsNullOrWhiteSpace(_sheetName))
                throw new InvalidOperationException("SheetName is not set.");

            // 若尚未连接则尝试连接（避免调用方忘记先 Connect）
            if (!IsConnected())
            {
                Connect();
            }
            if (!IsConnected())
                throw new InvalidOperationException("Not connected to the database.");

            // 使用 INFORMATION_SCHEMA 精确判断，避免 LIKE 误判及特殊字符问题
            const string sql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table LIMIT 1;";
            try
            {
                using var cmd = new MySqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@table", _sheetName);
                var result = cmd.ExecuteScalar();
                return result is not null; // 有行即存在
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException($"Failed to check table existence for '{_sheetName}'. MySQL error {ex.Number}: {ex.Message}", ex);
            }
        }

        public virtual string ExecuteCommand(string commandText)
        {
            if (!IsConnected())
            {
                throw new InvalidOperationException("Not connected to the database.");
            }
            using (var command = new MySqlCommand(commandText, _connection))
            {
                var result = command.ExecuteScalar();
                return result?.ToString() ?? string.Empty;
            }
        }
    }
}
