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
    public static class RecordExtensions
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
                    args[i] = ConvertToType(value, param.ParameterType);
                }
                else
                {
                    args[i] = param.HasDefaultValue ? param.DefaultValue : GetDefault(param.ParameterType);
                }
            }
            return (T?)ctor.Invoke(args);
        }

        private static object? ConvertToType(object value, Type targetType)
        {
            if (value == null) return null;
            
            // Handle nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                targetType = underlyingType;
            }
            
            // If already correct type, return as-is
            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }
            
            var stringValue = value.ToString();
            if (string.IsNullOrEmpty(stringValue))
            {
                return GetDefault(targetType);
            }
            
            // Handle specific types
            if (targetType == typeof(string))
                return stringValue;
            
            if (targetType == typeof(int))
                return int.TryParse(stringValue, out var i) ? i : GetDefault(targetType);
            
            if (targetType == typeof(long))
                return long.TryParse(stringValue, out var l) ? l : GetDefault(targetType);
            
            if (targetType == typeof(bool))
            {
                // Handle both "true/false" and "1/0" formats
                if (bool.TryParse(stringValue, out var b))
                    return b;
                if (stringValue == "1")
                    return true;
                if (stringValue == "0")
                    return false;
                return GetDefault(targetType);
            }
            
            if (targetType == typeof(DateTime))
                return DateTime.TryParse(stringValue, out var d) ? d : GetDefault(targetType);
            
            if (targetType == typeof(decimal))
                return decimal.TryParse(stringValue, out var dec) ? dec : GetDefault(targetType);
            
            if (targetType == typeof(double))
                return double.TryParse(stringValue, out var dbl) ? dbl : GetDefault(targetType);
            
            if (targetType == typeof(float))
                return float.TryParse(stringValue, out var f) ? f : GetDefault(targetType);
            
            if (targetType.IsEnum)
                return Enum.TryParse(targetType, stringValue, true, out var e) ? e : GetDefault(targetType);
            
            // Fallback to Convert.ChangeType
            try
            {
                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return GetDefault(targetType);
            }
        }

        private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
