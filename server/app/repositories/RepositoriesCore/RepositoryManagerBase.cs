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
        protected RepositoryManagerBase(string? connectionString, string sheetName)
        {
            _connectionString = connectionString ?? string.Empty;
            _sheetName = sheetName;
            Task.Run(async () => await ConnectAsync()).Wait();
        }
        ~RepositoryManagerBase()
        {
            Dispose(false);
        }

        // Properties and fields
        private string _connectionString;
        protected string _sheetName;
        protected MySqlConnection? _connection;
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
        protected MySqlConnection? Connection => _connection; // 连接实例不暴露给外部
        public string SheetName => _sheetName;

        // Methods implementation
        public virtual IRepository Clone()
        {
            return (IRepository)MemberwiseClone();
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
            
            try
            {
                // 如果连接已存在但状态异常，先清理
                if (_connection != null && _connection.State == System.Data.ConnectionState.Broken)
                {
                    _connection.Dispose();
                    _connection = null;
                }
                
                if (_connection is null)
                {
                    _connection = new MySqlConnection(ConnectionString);
                }
                
                if (_connection.State != System.Data.ConnectionState.Open)
                {
                    await _connection.OpenAsync();
                }
                
                return _connection.State == System.Data.ConnectionState.Open;
            }
            catch (MySqlException ex)
            {
                // 连接失败时清理资源
                if (_connection != null)
                {
                    _connection.Dispose();
                    _connection = null;
                }
                throw new InvalidOperationException($"Failed to connect to database. MySQL error {ex.Number}: {ex.Message}", ex);
            }
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
        public virtual async Task<bool> InitializeDatabaseAsync(IEnumerable<ColumnDefinition> columns)
        {
            if (columns is null) throw new ArgumentNullException(nameof(columns));
            var cols = columns.ToList();
            if (!cols.Any()) throw new ArgumentException("No column definitions provided.");
            if (!await TryConnectAsync()) throw new InvalidOperationException("Not connected to the database.");

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
                
                // AUTO_INCREMENT 只能用于数值类型的主键
                var autoInc = c.AutoIncrement && c.IsPrimaryKey && IsNumericType(c.Type) ? " AUTO_INCREMENT" : string.Empty;
                
                // 处理 DEFAULT 值
                var defaultExpr = string.Empty;
                if (!string.IsNullOrWhiteSpace(c.DefaultValue))
                {
                    if (c.Type == DbColumnType.DateTime)
                    {
                        // 处理 DateTime 类型的特殊默认值
                        if (c.DefaultValue.Contains("ON UPDATE"))
                        {
                            // 分离默认值和更新值
                            var parts = c.DefaultValue.Split(new[] { " ON UPDATE " }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2)
                            {
                                defaultExpr = $" DEFAULT {parts[0]} ON UPDATE {parts[1]}";
                            }
                            else
                            {
                                defaultExpr = $" DEFAULT {c.DefaultValue}";
                            }
                        }
                        else
                        {
                            defaultExpr = $" DEFAULT {c.DefaultValue}";
                        }
                    }
                    else
                    {
                        defaultExpr = $" DEFAULT {c.DefaultValue}";
                    }
                }
                
                var comment = !string.IsNullOrWhiteSpace(c.Comment) ? $" COMMENT '{c.Comment.Replace("'", "''")}'" : string.Empty;
                
                // 构建完整的列定义：类型 + NOT NULL/NULL + AUTO_INCREMENT + DEFAULT + COMMENT
                var columnDef = $"`{c.Name}` {type} {nullStr}{autoInc}{defaultExpr}{comment}".Trim();
                columnSqlParts.Add(columnDef);
                
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
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        // 辅助方法：检查是否为数值类型
        private static bool IsNumericType(DbColumnType type)
        {
            return type == DbColumnType.Int32 || type == DbColumnType.Int64 || type == DbColumnType.Decimal;
        }

        public virtual bool IsConnected()
        {
            try
            {
                return _connection is not null && 
                       _connection.State == System.Data.ConnectionState.Open;
            }
            catch
            {
                // 如果检查连接状态时出现异常，认为连接不可用
                return false;
            }
        }

        public virtual async Task<bool> DatabaseIsInitializedAsync()
        {
            // 校验基础参数
            if (string.IsNullOrWhiteSpace(_sheetName))
                throw new InvalidOperationException("SheetName is not set.");

            // 若尚未连接则尝试连接（避免调用方忘记先 Connect）
            if (!await TryConnectAsync()) throw new InvalidOperationException("Not connected to the database.");

            // 使用 INFORMATION_SCHEMA 精确判断，避免 LIKE 误判及特殊字符问题
            const string sql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table LIMIT 1;";
            try
            {
                using var cmd = new MySqlCommand(sql, _connection);
                cmd.Parameters.AddWithValue("@table", _sheetName);
                var result = await cmd.ExecuteScalarAsync();
                return result is not null; // 有行即存在
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException($"Failed to check table existence for '{_sheetName}'. MySQL error {ex.Number}: {ex.Message}", ex);
            }
        }

        public virtual async Task<string> ExecuteCommandAsync(string commandText)
        {
            // 确保连接可用
            if (!await EnsureConnectionAvailableAsync())
            {
                throw new InvalidOperationException("Cannot establish or verify database connection.");
            }
            
            try
            {
                using var command = new MySqlCommand(commandText, _connection);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? string.Empty;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in use"))
            {
                // 如果连接仍然被占用，等待并重试一次
                await Task.Delay(50);
                using var command = new MySqlCommand(commandText, _connection);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? string.Empty;
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException($"Failed to execute command. MySQL error {ex.Number}: {ex.Message}", ex);
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

        // 辅助方法：确保连接可用并且没有被其他操作占用
        protected virtual async Task<bool> EnsureConnectionAvailableAsync()
        {
            if (!IsConnected())
            {
                return await ConnectAsync();
            }

            // 检查连接是否被占用（通过尝试执行一个简单的查询）
            try
            {
                using var testCmd = new MySqlCommand("SELECT 1", _connection);
                await testCmd.ExecuteScalarAsync();
                return true;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in use"))
            {
                // 连接被占用，等待一小段时间后重试
                await Task.Delay(10);
                return false;
            }
            catch
            {
                // 其他错误，尝试重新连接
                return await ConnectAsync();
            }
        }

        // Abstract methods to be implemented by subclasses
        public abstract Task<bool> AddNewRecordsAsync(string[] records);
        public abstract Task<bool> UpdateRecordAsync(string UUID, string record);
        public abstract Task<string[]?> ReadRecordsAsync(string[] UUIDs);
        public abstract Task<bool> DeleteRecordsAsync(string[] UUIDs);
        public abstract Task<string[]?> SearchRecordsAsync(string searchTarget, object content);

        // Database structure definition
        public abstract List<ColumnDefinition> DatabaseDefinition { get; }

    }
}
