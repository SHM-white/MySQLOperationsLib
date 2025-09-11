using MySqlConnector;
using System.Text;
using System.Text.Json;

namespace RepositoriesCore
{
    // Non-generic base class for backward compatibility
    public abstract partial class RepositoryManagerBase : IRepository, IDisposable
    {
        // Database structure definition
        public abstract IEnumerable<ColumnDefinition> databaseDefinition { get; }

        // Properties and fields
        private string _connectionString = connectionString ?? string.Empty;
        protected string _sheetName = sheetName;
    }

    public abstract partial class RepositoryManagerBase(string? connectionString, string sheetName) : IRepository, IDisposable
    {
        public string ConnectionString
        {
            get => _connectionString;
            set
            {
                if (IRepository.IsValidConnectionString(value))
                {
                    _connectionString = value;
                }
                else throw new ArgumentException($"Connection string is illegal: {value}");
            }
        }

        public string SheetName => _sheetName;

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
            ArgumentNullException.ThrowIfNull(columns);
            var cols = columns.ToList();
            if (cols.Count == 0) throw new ArgumentException("No column definitions provided.");

            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Not connected to the database.");
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
                    if (c.Type == DbColumnType.DateTime && c.DefaultValue.Contains("ON UPDATE"))
                    {
                        // 分离默认值和更新值
                        var parts = c.DefaultValue.Split([" ON UPDATE "], StringSplitOptions.RemoveEmptyEntries);
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

            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Not connected to the database.");

            // 使用 INFORMATION_SCHEMA 精确判断，避免 LIKE 误判及特殊字符问题
            const string sql = @"SELECT 1 FROM INFORMATION_SCHEMA.TABLES 
                WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table LIMIT 1;";
            using var cmd = new MySqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@table", SheetName);
            var result = await cmd.ExecuteScalarAsync();
            return result is not null; // 有行即存在
        }

        public virtual async Task<string> ExecuteCommandAsync(string commandText)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            using var command = new MySqlCommand(commandText, connection);
            var result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        public virtual async Task<string[]?> ReadRecordsAsync(string[] UUIDs)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            var sqlCommand = $"SELECT * FROM `{SheetName}` WHERE `UUID` IN ({string.Join(",", UUIDs.Select(id => $"'{id}'"))});";

            using var cmd = new MySqlConnector.MySqlCommand(sqlCommand, connection);
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<string>();

            while (await reader.ReadAsync())
            {
                var record = new Dictionary<string, object?>();
                foreach (var col in databaseDefinition)
                {
                    record[col.Name] = reader[col.Name] is DBNull ? null : reader[col.Name];
                }

                var jsonString = await IRepository.SerializeToJsonAsync(record);
                results.Add(jsonString);
            }

            return results?.ToArray();
        }

        public virtual async Task<bool> AddNewRecordsAsync(Dictionary<string, object?>[] records)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            if (records == null || records.Length == 0)
                return false;
            var sqlCommand = new StringBuilder();
            foreach (var record in records)
            {
                sqlCommand.AppendLine($"INSERT INTO `{SheetName}` (");
                var columnNames = databaseDefinition.Where(c => !c.AutoIncrement).Select(c => $"`{c.Name}`");
                sqlCommand.AppendLine(string.Join(", ", columnNames));
                sqlCommand.AppendLine(") VALUES (");
                var values = new List<string>();
                foreach (var col in databaseDefinition)
                {
                    if (col.AutoIncrement) continue; // 跳过自增列
                    if (record.TryGetValue(col.Name, out var value) && value != null)
                    {
                        if (value is string strVal)
                        {
                            // 对字符串进行转义
                            strVal = strVal.Replace("'", "''");
                            values.Add($"'{strVal}'");
                        }
                        else if (value is bool boolVal)
                        {
                            values.Add(boolVal ? "1" : "0");
                        }
                        else if (value is DateTime dateTimeVal)
                        {
                            values.Add($"'{dateTimeVal:yyyy-MM-dd HH:mm:ss}'");
                        }
                        else
                        {
                            values.Add(value.ToString() ?? "NULL");
                        }
                    }
                    else
                    {
                        values.Add("NULL");
                    }
                }
                sqlCommand.AppendLine(string.Join(", ", values));
                sqlCommand.AppendLine(");");
            }
            using var cmd = new MySqlCommand(sqlCommand.ToString(), connection);
            var affectedRows = await cmd.ExecuteNonQueryAsync();
            return affectedRows == records.Length;
        }

        public virtual async Task<bool> DeleteRecordsAsync(string[] UUIDs)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            if (UUIDs == null || UUIDs.Length == 0)
                return false;
            var sqlCommand = $"DELETE FROM `{SheetName}` WHERE `UUID` IN ({string.Join(",", UUIDs.Select(id => $"'{id}'"))});";
            using var cmd = new MySqlCommand(sqlCommand, connection);
            var affectedRows = await cmd.ExecuteNonQueryAsync();
            return affectedRows == UUIDs.Length;
        }

        public virtual async Task<bool> UpdateRecordAsync(string UUID, Dictionary<string, object?> record)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            if (string.IsNullOrWhiteSpace(UUID) || record == null || record.Count == 0)
                throw new ArgumentException($"Unexpected parameters: {UUID}, {record}");
                
            var command = new StringBuilder();
            command.AppendLine($"UPDATE `{SheetName}` SET ");
            var setClauses = new List<string>();
            
            // 只更新记录中实际包含的列，而不是所有列
            foreach (var kvp in record)
            {
                var columnName = kvp.Key;
                var value = kvp.Value;
                
                // 跳过主键列
                var columnDef = databaseDefinition.FirstOrDefault(c => c.Name == columnName);
                if (columnDef?.IsPrimaryKey == true) continue;
                
                if (value is string strVal)
                {
                    // 对字符串进行转义
                    strVal = strVal.Replace("'", "''");
                    setClauses.Add($"`{columnName}` = '{strVal}'");
                }
                else if (value is bool boolVal)
                {
                    setClauses.Add($"`{columnName}` = {(boolVal ? "1" : "0")}");
                }
                else if (value is DateTime dateTimeVal)
                {
                    setClauses.Add($"`{columnName}` = '{dateTimeVal:yyyy-MM-dd HH:mm:ss}'");
                }
                else if (value != null)
                {
                    setClauses.Add($"`{columnName}` = {value}");
                }
                else
                {
                    // 只有当列允许为 NULL 时才设置为 NULL
                    if (columnDef?.IsNullable == true)
                    {
                        setClauses.Add($"`{columnName}` = NULL");
                    }
                }
            }
            
            if (setClauses.Count == 0)
            {
                return false; // 没有要更新的列
            }
            
            command.AppendLine(string.Join(", ", setClauses));
            command.AppendLine($" WHERE `UUID` = '{UUID}';");
            
            using var cmd = new MySqlCommand(command.ToString(), connection);
            var affectedRows = await cmd.ExecuteNonQueryAsync();
            return affectedRows == 1;
        }

        public virtual async Task<string[]?> SearchRecordsAsync(string searchTarget, object content)
        {
            using var connection = await TryConnectAsync() ?? throw new InvalidOperationException("Cannot establish database connection.");
            if (string.IsNullOrWhiteSpace(searchTarget) || content == null)
                throw new ArgumentException($"Unexpected parameters: {searchTarget}, {content}");
                
            var commandString = new StringBuilder();
            commandString.AppendLine($"SELECT * FROM `{SheetName}` WHERE `{searchTarget}` = ");
            if (content is string strVal)
            {
                // 对字符串进行转义
                strVal = strVal.Replace("'", "''");
                commandString.AppendLine($"'{strVal}';");
            }
            else if (content is bool boolVal)
            {
                commandString.AppendLine(boolVal ? "1;" : "0;");
            }
            else if (content is DateTime dateTimeVal)
            {
                commandString.AppendLine($"'{dateTimeVal:yyyy-MM-dd HH:mm:ss}';");
            }
            else
            {
                commandString.AppendLine($"{content};");
            }
            
            using var cmd = new MySqlCommand(commandString.ToString(), connection);
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<string>();
            
            while (await reader.ReadAsync())
            {
                var record = new Dictionary<string, object?>();
                foreach (var col in databaseDefinition)
                {
                    record[col.Name] = reader[col.Name] is DBNull ? null : reader[col.Name];
                }
                // 使用异步 JSON 序列化
                var jsonString = await IRepository.SerializeToJsonAsync(record);
                results.Add(jsonString);
            }
            
            return results?.ToArray();
        }
    }

    public abstract partial class RepositoryManagerBase<T>(string? connectionString, string sheetName) : RepositoryManagerBase(connectionString, sheetName) where T : class
    {
        // Abstract methods for type conversion - must be implemented by derived classes
        public abstract Dictionary<string, object?>? RecordToDict(T? record);
        public abstract T? DictToRecord(Dictionary<string, object?>? dict);

        // Strongly-typed methods that use the base class JSON methods

        /// <summary>
        /// Read records and return strongly-typed objects
        /// </summary>
        public virtual async Task<T[]?> ReadTypedRecordsAsync(string[] UUIDs)
        {
            var jsonResults = await ReadRecordsAsync(UUIDs);
            if (jsonResults == null || jsonResults.Length == 0)
                return null;

            var typedRecords = new List<T>();
            foreach (var jsonItem in jsonResults)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonItem);
                    if (dict != null)
                    {
                        var typedRecord = DictToRecord(dict);
                        if (typedRecord != null)
                        {
                            typedRecords.Add(typedRecord);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new JsonException($"Failed to deserialize record: {jsonItem}", ex);
                }
            }
            return [.. typedRecords];
        }

        /// <summary>
        /// Add new records using strongly-typed objects
        /// </summary>
        public virtual async Task<bool> AddNewTypedRecordsAsync(T[] records)
        {
            var recordsDictionary = records.Select(
                r => RecordToDict(r) ?? throw new ArgumentNullException(nameof(r), "Record cannot be null.")
                ).ToArray() ?? [];
            return await AddNewRecordsAsync(recordsDictionary);
        }

        /// <summary>
        /// Update a record using strongly-typed object
        /// </summary>
        public virtual async Task<bool> UpdateTypedRecordAsync(string UUID, T record)
        {
            var recordDictionary = RecordToDict(record);
            if (recordDictionary == null)
                return false;
            return await UpdateRecordAsync(UUID, recordDictionary);
        }

        /// <summary>
        /// Search records and return strongly-typed objects
        /// </summary>
        public virtual async Task<T[]?> SearchTypedRecordsAsync(string searchTarget, object content)
        {
            var jsonResults = await SearchRecordsAsync(searchTarget, content);
            if (jsonResults == null || jsonResults.Length == 0)
                return null;

            var typedRecords = new List<T>();
            foreach (var jsonItem in jsonResults)
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonItem);
                    if (dict != null)
                    {
                        var typedRecord = DictToRecord(dict);
                        if (typedRecord != null)
                        {
                            typedRecords.Add(typedRecord);
                        }
                    }
                }
                catch (JsonException ex)
                {
                    throw new JsonException($"Failed to deserialize record: {jsonItem}", ex);
                }
            }
            return [.. typedRecords];
        }

    }
}
