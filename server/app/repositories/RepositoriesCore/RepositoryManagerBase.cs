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
        }
        ~RepositoryManagerBase()
        {
        }

        // Properties and fields
        private string _connectionString;
        protected string _sheetName;
        
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

        public string SheetName => _sheetName;

        // Methods implementation
        public virtual IRepository Clone()
        {
            return (IRepository)MemberwiseClone();
        }

        // 创建并返回新的数据库连接，连接失败返回 null
        public virtual async Task<MySqlConnection?> TryConnectAsync()
        {
            if (string.IsNullOrEmpty(ConnectionString))
            {
                return null;
            }
            
            try
            {
                var connection = new MySqlConnection(ConnectionString);
                await connection.OpenAsync();
                
                // 验证连接是否真的打开了
                if (connection.State == System.Data.ConnectionState.Open)
                {
                    return connection;
                }
                else
                {
                    connection.Dispose();
                    return null;
                }
            }
            catch (MySqlException)
            {
                // 连接失败，返回 null
                return null;
            }
            catch (Exception)
            {
                // 其他异常，也返回 null
                return null;
            }
        }
        
        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
        
        public virtual async Task<bool> InitializeDatabaseAsync(IEnumerable<ColumnDefinition> columns)
        {
            if (columns is null) throw new ArgumentNullException(nameof(columns));
            var cols = columns.ToList();
            if (!cols.Any()) throw new ArgumentException("No column definitions provided.");

            using var connection = await TryConnectAsync();
            if (connection == null) throw new InvalidOperationException("Not connected to the database.");

            if (string.IsNullOrEmpty(SheetName))
                throw new ArgumentException($"Invalid table name: {SheetName}");

            // 先删除表
            using (var dropCmd = new MySqlCommand($"DROP TABLE IF EXISTS `{SheetName}`", connection))
            {
                await dropCmd.ExecuteNonQueryAsync();
            }

            for (int i = 0; i < cols.Count; i++)
            {
                var c = cols[i];
                if (!c.Name.IsValidIdentifier())
                    throw new ArgumentException($"Invalid column name: {c.Name}");
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
                    uniqueKeys.Add($"UNIQUE KEY `UK_{SheetName}_{c.Name}` (`{c.Name}`)");
                }
                if (c.IsIndexed && !c.IsPrimaryKey && !c.IsUnique)
                {
                    indexes.Add($"KEY `IDX_{SheetName}_{c.Name}` (`{c.Name}`)");
                }
            }

            string pkClause = primaryKeys.Count == 1
                ? $"PRIMARY KEY (`{primaryKeys[0].Name}`)"
                : $"PRIMARY KEY ({string.Join(",", primaryKeys.Select(p => $"`{p.Name}`"))})";

            var allConstraints = new List<string> { pkClause };
            allConstraints.AddRange(uniqueKeys);
            allConstraints.AddRange(indexes);

            string createTableSql = $@"CREATE TABLE IF NOT EXISTS `{SheetName}` (
{string.Join(",\n", columnSqlParts)},
{string.Join(",\n", allConstraints)}
) DEFAULT CHARSET=utf8mb4;";

            using var cmd = new MySqlCommand(createTableSql, connection);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        // 辅助方法：检查是否为数值类型
        private static bool IsNumericType(DbColumnType type)
        {
            return type == DbColumnType.Int32 || type == DbColumnType.Int64 || type == DbColumnType.Decimal;
        }

        public virtual async Task<bool> DatabaseIsInitializedAsync()
        {
            // 校验基础参数
            if (string.IsNullOrWhiteSpace(SheetName))
                throw new InvalidOperationException("SheetName is not set.");

            using var connection = await TryConnectAsync();
            if (connection is null) throw new InvalidOperationException("Not connected to the database.");

            // 使用 INFORMATION_SCHEMA 精确判断，避免 LIKE 误判及特殊字符问题
            const string sql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table LIMIT 1;";
            try
            {
                using var cmd = new MySqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@table", SheetName);
                var result = await cmd.ExecuteScalarAsync();
                return result is not null; // 有行即存在
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException($"Failed to check table existence for '{SheetName}'. MySQL error {ex.Number}: {ex.Message}", ex);
            }
        }

        public virtual async Task<string> ExecuteCommandAsync(string commandText)
        {
            using var connection = await TryConnectAsync();
            if (connection is null)
            {
                throw new InvalidOperationException("Cannot establish database connection.");
            }
            
            try
            {
                using var command = new MySqlCommand(commandText, connection);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString() ?? string.Empty;
            }
            catch (MySqlException ex)
            {
                throw new InvalidOperationException($"Failed to execute command. MySQL error {ex.Number}: {ex.Message}", ex);
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
