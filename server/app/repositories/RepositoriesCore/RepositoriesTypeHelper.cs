using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static RepositoriesCore.ActivityLogsRepository;
using static RepositoriesCore.EmployeesRepository;

namespace RepositoriesCore
{
    /// <summary>
    /// Extension methods for EmployeeRecord conversion to maintain backward compatibility
    /// </summary>
    public static class EmployeeRecordExtensions
    {
        // Record -> Dictionary
        public static Dictionary<string, object?>? RecordToDict<T>(this T? record) where T : class
        {
            if (record == null) return null;
            var dict = new Dictionary<string, object?>();
            foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                dict[prop.Name] = prop.GetValue(record);
            }
            return dict;
        }

        // Dictionary -> Record
        public static T? DictToRecord<T>(this Dictionary<string, object?>? dict) where T : class
        {
            if (dict == null) return null;
            var ctor = typeof(T).GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor == null) throw new InvalidOperationException($"No constructor found for type {typeof(T).Name}");

            var parameters = ctor.GetParameters();
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (dict.TryGetValue(param.Name ?? "", out var value) && value != null)
                {
                    args[i] = Convert.ChangeType(value, param.ParameterType);
                }
                else
                {
                    args[i] = param.HasDefaultValue ? param.DefaultValue : GetDefault(param.ParameterType);
                }
            }
            return (T?)ctor.Invoke(args);
        }

        private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    internal static class RepositoriesTypeHelper
    {
        public static string? GetStringValue(this Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                return value.ToString();
            }
            return null;
        }

        public static DateTime? GetDateTimeValue(this Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                if (value is DateTime dateTime)
                {
                    return dateTime;
                }
                if (DateTime.TryParse(value.ToString(), out var parsedDateTime))
                {
                    return parsedDateTime;
                }
            }
            return null;
        }
        public static int? GetIntValue(this Dictionary<string, object?> dict, string key)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                if (value is int intValue)
                {
                    return intValue;
                }
                if (value is long longValue)
                {
                    return (int)longValue;
                }
                if (int.TryParse(value.ToString(), out var parsedInt))
                {
                    return parsedInt;
                }
            }
            return null;
        }
        
    }
}
