using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace RepositoriesCore
{
    // Simple in-memory implementation placeholder (empty implementations)
    public class EmployeesRepository : RepositoryManagerBase
    {
        private readonly List<ColumnDefinition> _databaseDefinition = new List<ColumnDefinition>
        {
            new (Name:"UUID", Type:DbColumnType.String, Length:36, IsPrimaryKey:true, IsNullable:false, Comment:"UUID，主键"),
            new (Name:"user_id", Type:DbColumnType.String, Length:36, IsNullable:false, Comment:"用户ID"),
            new (Name:"name", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"员工姓名"),
            new (Name:"department", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"所在部门"),
            new (Name:"workstation_id", Type:DbColumnType.String, Length:20, DefaultValue:null, Comment:"工位编号"),
            new (Name:"preference", Type:DbColumnType.Text, DefaultValue:null, Comment:"健康偏好设置(JSON格式)"),
            new (Name:"created_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP", Comment:"创建时间"),
            new (Name:"updated_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP", Comment:"更新时间")
        };
        public override List<ColumnDefinition> DatabaseDefinition => _databaseDefinition;
        public EmployeesRepository(string? connectionString) : base(connectionString, "Employees")
        {
        }

        public override async Task<string[]?> ReadRecordsAsync(string[] UUIDs)
        {
            if (!await EnsureConnectionAvailableAsync()) 
                throw new InvalidOperationException("Cannot establish or verify database connection.");
            
            var sqlCommand = $"SELECT * FROM `{SheetName}` WHERE `UUID` IN ({string.Join(",", UUIDs.Select(id => $"'{id}'"))});";
            
            try
            {
                // 正确使用 using 语句来管理资源
                using var cmd = new MySqlConnector.MySqlCommand(sqlCommand, _connection);
                using var reader = await cmd.ExecuteReaderAsync();
                var results = new List<string>();
                
                while (await reader.ReadAsync())
                {
                    var record = new Dictionary<string, object?>();
                    foreach (var col in DatabaseDefinition)
                    {
                        record[col.Name] = reader[col.Name] is DBNull ? null : reader[col.Name];
                    }

                    // 使用异步 JSON 序列化
                    var jsonString = await Task.Run(async () =>
                    {
                        using var stream = new MemoryStream();
                        await JsonSerializer.SerializeAsync(stream, record, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = false
                        });

                        stream.Position = 0;
                        using var reader = new StreamReader(stream);
                        return await reader.ReadToEndAsync();
                    });
                    results.Add(jsonString);
                }
                
                return results?.ToArray();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already in use"))
            {
                // 如果连接被占用，等待并重试
                await Task.Delay(50);
                using var cmd = new MySqlConnector.MySqlCommand(sqlCommand, _connection);
                using var reader = await cmd.ExecuteReaderAsync();
                var results = new List<string>();
                
                while (await reader.ReadAsync())
                {
                    var record = new Dictionary<string, object?>();
                    foreach (var col in DatabaseDefinition)
                    {
                        record[col.Name] = reader[col.Name] is DBNull ? null : reader[col.Name];
                    }

                    var jsonString = await Task.Run(async () =>
                    {
                        using var stream = new MemoryStream();
                        await JsonSerializer.SerializeAsync(stream, record, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = false
                        });

                        stream.Position = 0;
                        using var reader = new StreamReader(stream);
                        return await reader.ReadToEndAsync();
                    });
                    results.Add(jsonString);
                }
                
                return results?.ToArray();
            }
        }

        public override Task<bool> AddNewRecordsAsync(string[] records)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> UpdateRecordAsync(string UUID, string record)
        {
            throw new NotImplementedException();
        }
        public override Task<bool> DeleteRecordsAsync(string[] UUIDs)
        {
            throw new NotImplementedException();
        }

        public override Task<string[]?> SearchRecordsAsync(string searchTarget, object content)
        {
            throw new NotImplementedException();
        }
    }
}
