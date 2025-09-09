using System.Text.Json;
using static RepositoriesCore.EmployeesRepository;

namespace RepositoriesCore
{
    public class EmployeesRepository(string? connectionString) : RepositoryManagerBase<EmployeesRepository.EmployeeRecord>(connectionString, "Employees")
    {
        public override IEnumerable<ColumnDefinition> DatabaseDefinition => _databaseDefinition;

        // Implement the abstract methods for type conversion
        public override Dictionary<string, object?>? RecordToDict(EmployeeRecord? record)
        {
            return record.RecordToDict();
        }

        public override EmployeeRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            return dict.DictToEmployeeRecord();
        }

        // Employee record definition
        public record EmployeeRecord(
            string UUID,
            string UserId,
            string Name,
            string Department,
            string? WorkstationId,
            string? Preference,
            DateTime CreatedAt,
            DateTime UpdatedAt
        );
        
        // Database definition
        private readonly IEnumerable<ColumnDefinition> _databaseDefinition =
        [
            new (Name:"UUID", Type:DbColumnType.String, Length:36, IsPrimaryKey:true, IsNullable:false, Comment:"UUID，主键"),
            new (Name:"user_id", Type:DbColumnType.String, Length:36, IsNullable:false, Comment:"用户ID"),
            new (Name:"name", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"员工姓名"),
            new (Name:"department", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"所在部门"),
            new (Name:"workstation_id", Type:DbColumnType.String, Length:20, DefaultValue:null, Comment:"工位编号"),
            new (Name:"preference", Type:DbColumnType.Text, DefaultValue:null, Comment:"健康偏好设置(JSON格式)"),
            new (Name:"created_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP", Comment:"创建时间"),
            new (Name:"updated_at", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP", Comment:"更新时间")
        ];

    }
}
