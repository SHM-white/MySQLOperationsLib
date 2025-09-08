using Microsoft.Identity.Client;
using MySqlConnector;
using RepositoriesCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace RepositoriesCore
{
    public abstract class RepositoryManagerBase : IRepository, IDisposable
    {
        public abstract List<ColumnDefinition> DatabaseDefinition { get; }
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
        protected MySqlConnection? Connection => _connection; // 连接实例不暴露给外部
        protected string _sheetName;
        public string SheetName => _sheetName;
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
            if (string.IsNullOrEmpty(ConnectionString))
            {
                return false;
            }
            if (_connection is null)
            {
                _connection = new MySqlConnection(ConnectionString);
            }
            if (_connection is not null && _connection.State != System.Data.ConnectionState.Open)
            {
                _connection.Open();
            }
            return _connection is not null && _connection.State == System.Data.ConnectionState.Open;
        }

        public virtual async Task<bool> ConnectAsync(string connectionString)
        {
            this.ConnectionString = connectionString;
            return await ConnectAsync();
        }

        public virtual async Task<bool> ConnectAsync()
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                return false;
            }
            if (_connection is null)
            {
                _connection = new MySqlConnection(ConnectionString);
            }
            if (_connection is not null && _connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
            return _connection is not null && _connection.State == System.Data.ConnectionState.Open;
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
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Disconnect();
            }
            else
            {
                try { Disconnect(); } catch { }
            }
        }

        // 新增：基于列定义动态建表
        public virtual bool InitializeDatabase(IEnumerable<ColumnDefinition> columns)
        {
            if (columns is null) throw new ArgumentNullException(nameof(columns));
            var cols = columns.ToList();
            if (!cols.Any()) throw new ArgumentException("No column definitions provided.");
            if (!IsConnected()) Connect();
            if (!IsConnected()) throw new InvalidOperationException("Not connected to the database.");

            if (string.IsNullOrEmpty(_sheetName))
                throw new ArgumentException($"Invalid table name: {_sheetName}");

            for (int i = 0; i < cols.Count; i++)
            {
                var c = cols[i];
                if (!c.Name.IsValidIdentifier())
                    throw new ArgumentException($"Invalid column name: {c.Name}");
                // 自动主键推断：如果没有主键且列名为 UUID
            }
            if (!cols.Any(c => c.IsPrimaryKey))
            {
                var uuid = cols.FirstOrDefault(c => string.Equals(c.Name, "UUID", StringComparison.OrdinalIgnoreCase));
                if (uuid is not null)
                {
                    cols[cols.IndexOf(uuid)] = uuid with { IsPrimaryKey = true, IsNullable = false };
                }
                else
                {
                    // 插入一个自增主键列
                    cols.Insert(0, new ColumnDefinition("Id", DbColumnType.Int64, IsPrimaryKey: true, IsNullable: false, AutoIncrement: true));
                }
            }

            // 生成列定义
            var columnSqlParts = new List<string>();
            var primaryKeys = cols.Where(c => c.IsPrimaryKey).ToList();
            var uniqueKeys = new List<string>();
            var indexes = new List<string>();

            foreach (var c in cols)
            {
                var type = c.ToMySqlType();
                var nullStr = c.IsNullable && !c.IsPrimaryKey ? "NULL" : "NOT NULL";
                var autoInc = c.AutoIncrement ? " AUTO_INCREMENT" : string.Empty;
                var defaultExpr = !string.IsNullOrWhiteSpace(c.DefaultValue) ? $" DEFAULT {c.DefaultValue}" : string.Empty;
                var comment = !string.IsNullOrWhiteSpace(c.Comment) ? $" COMMENT '{c.Comment.Replace("'", "''")}'" : string.Empty;
                columnSqlParts.Add($"`{c.Name}` {type} {nullStr}{autoInc}{defaultExpr}{comment}".Trim());
                if (c.IsUnique && !c.IsPrimaryKey)
                {
                    uniqueKeys.Add($"UNIQUE KEY `UK_{_sheetName}_{c.Name}` (`{c.Name}`)");
                }
                if (c.IsIndexed && !c.IsPrimaryKey && !c.IsUnique)
                {
                    indexes.Add($"KEY `IDX_{_sheetName}_{c.Name}` (`{c.Name}`)");
                }
            }

            string pkClause = primaryKeys.Count == 1
                ? $"PRIMARY KEY (`{primaryKeys[0].Name}`)"
                : $"PRIMARY KEY ({string.Join(",", primaryKeys.Select(p => $"`{p.Name}`"))})";

            var allConstraints = new List<string> { pkClause };
            allConstraints.AddRange(uniqueKeys);
            allConstraints.AddRange(indexes);

            string createTableSql = $@"CREATE TABLE IF NOT EXISTS `{_sheetName}` (
{string.Join(",\n", columnSqlParts)},
{string.Join(",\n", allConstraints)}
) DEFAULT CHARSET=utf8mb4;";

            using var cmd = new MySqlCommand(createTableSql, _connection);
            cmd.ExecuteNonQuery();
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

        public virtual async Task<bool> TryConnectAsync()
        {
            if (!IsConnected())
            {
                return await ConnectAsync();
            }
            return IsConnected();
        }
    }
}
