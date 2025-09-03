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
        protected RepositoryManagerBase(string? connectionString)
        {
            _connectionString = connectionString ?? string.Empty;
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

        public abstract bool InitializeDatabase(Dictionary<string, string> properties);

        public virtual bool IsConnected()
        {
            return _connection is not null && _connection.State == System.Data.ConnectionState.Open;
        }

        public abstract string[]? ReadRecords(string[] UUIDs);

        public abstract string[]? SearchRecordsByUserId(string userId);

        public abstract bool UpdateRecord(string UUID, string record);

        public abstract bool CreateRecords(string[] records);

        public abstract bool DeleteRecords(string[] UUIDs);

        public abstract bool DatabaseIsInitialized();
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
