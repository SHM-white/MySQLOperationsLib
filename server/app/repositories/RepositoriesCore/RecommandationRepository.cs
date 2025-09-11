using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    public class RecommandationRepository(string? connectionString) : RepositoryManagerBase<RecommandationRepository.RecommandationRecord>(connectionString, "Recommandation")
    {
        public override IEnumerable<ColumnDefinition> databaseDefinition => throw new NotImplementedException();

        public override RecommandationRecord? DictToRecord(Dictionary<string, object?>? dict)
        {
            return dict.DictToRecord<RecommandationRecord>();
        }

        public override Dictionary<string, object?>? RecordToDict(RecommandationRecord? record)
        {
            return record.RecordToDict();
        }

        public record RecommandationRecord(
            string UUID,
            string UserId,
            string UserUUID,
            DateTime CreateTime,
            bool IsPushed,
            string Content
        );
        public static readonly IEnumerable<ColumnDefinition> DatabaseDefinition =
        [
            new (Name:"UUID", Type:DbColumnType.Guid, IsPrimaryKey:true, IsNullable:false, IsUnique:true, Comment:"UUID，主键"),
            new (Name:"UserId", Type:DbColumnType.String, IsPrimaryKey:false, IsNullable:false, IsUnique:false, IsIndexed:true, Comment:"用户ID"),
            new (Name:"UserUUID", Type:DbColumnType.Guid, IsPrimaryKey:false, IsNullable:false, IsUnique:false, IsIndexed:true, Comment:"用户UUID"),
            new (Name:"CreateTime", Type:DbColumnType.DateTime, IsPrimaryKey:false, IsNullable:false, IsUnique:false, Comment:"创建时间"),
            new (Name:"IsPushed", Type:DbColumnType.Boolean, IsPrimaryKey:false, IsNullable:false, IsUnique:false, IsIndexed:true, Comment:"是否推送"),
            new (Name:"Content", Type:DbColumnType.String, IsPrimaryKey:false, IsNullable:false, IsUnique:false, Comment:"内容")
        ];
    }
}
