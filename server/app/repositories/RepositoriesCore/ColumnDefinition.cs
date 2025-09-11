using System.Text.RegularExpressions;

namespace RepositoriesCore
{
    public enum DbColumnType
    {
        String,
        Int32,
        Int64,
        Boolean,
        DateTime,
        Decimal,
        Guid,
        Text,
        Json
    }

    public record ColumnDefinition(
        string Name,                    // Column name
        DbColumnType Type,              // Data type
        int? Length = null,             // Length for string types
        bool IsPrimaryKey = false,      // Is primary key
        bool IsNullable = true,         // Is nullable
        bool AutoIncrement = false,     // Is auto-increment
        string? DefaultValue = null,    // Default value
        bool IsUnique = false,          // Is unique
        bool IsIndexed = false,         // Is indexed
        string? Comment = null          // Comment or description
    );

    internal static class ColumnDefinitionExtensions
    {
        internal static bool IsValidIdentifier(this string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$");
        }

        internal static string ToMySqlType(this ColumnDefinition col)
        {
            return col.Type switch
            {
                DbColumnType.String => $"VARCHAR({col.Length ?? 255})",
                DbColumnType.Int32 => "INT",
                DbColumnType.Int64 => "BIGINT",
                DbColumnType.Boolean => "TINYINT(1)",
                DbColumnType.DateTime => "DATETIME",
                DbColumnType.Decimal => "DECIMAL(18,6)",
                DbColumnType.Guid => "CHAR(36)",
                DbColumnType.Text => "TEXT",
                DbColumnType.Json => "JSON",
                _ => "TEXT"
            };
        }
    }
}
