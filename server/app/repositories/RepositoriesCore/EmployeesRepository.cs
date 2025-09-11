using System.Text.Json;
using static RepositoriesCore.EmployeesRepository;

namespace RepositoriesCore
{
    public class EmployeesRepository(string? connectionString) : RepositoryManagerBase<EmployeesRepository.EmployeeRecord>(connectionString, "Employees")
    {
        public override IEnumerable<ColumnDefinition> databaseDefinition => EmployeesRepository.DatabaseDefinition;

        // Implement the abstract methods for type conversion
        public override Dictionary<string, object?>? RecordToDict(EmployeeRecord? record)
        {
            return record.RecordToDict();
        }

        public override EmployeeRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            return dict.DictToRecord<EmployeeRecord>();
        }

        // Employee record definition
        public record EmployeeRecord(
            string UUID,
            string UserId,
            string Name,
            string Department,
            string? WorkstationId,
            string? Preference,
            bool Online,
            DateTime CreatedAt,
            DateTime UpdatedAt
        );
        
        // Database definition
        private static readonly IEnumerable<ColumnDefinition> DatabaseDefinition =
        [
            new (Name:"UUID", Type:DbColumnType.Guid, IsPrimaryKey:true, IsNullable:false, IsUnique:true, Comment:"UUID，主键"),
            new (Name:"UserId", Type:DbColumnType.String, Length:36, IsNullable:false, IsIndexed:true, Comment:"用户ID"),
            new (Name:"Name", Type:DbColumnType.String, Length:50, IsNullable:false, IsIndexed:true, Comment:"员工姓名"),
            new (Name:"Department", Type:DbColumnType.String, Length:50, IsNullable:false, Comment:"所在部门，职位"),
            new (Name:"WorkstationId", Type:DbColumnType.String, Length:20, DefaultValue:null, Comment:"工位编号"),
            new (Name:"Preference", Type:DbColumnType.Json, DefaultValue:"{}", Comment:"健康偏好设置(JSON格式)"),
            new (Name:"Online", Type:DbColumnType.Boolean, IsNullable:false, DefaultValue:"0", IsIndexed:true, Comment:"是否在线"),
            new (Name:"CreatedAt", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP", IsIndexed:true, Comment:"创建时间"),
            new (Name:"UpdatedAt", Type:DbColumnType.DateTime, IsNullable:false, DefaultValue:"CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP", IsIndexed:true, Comment:"更新时间")
        ];

    }
}
