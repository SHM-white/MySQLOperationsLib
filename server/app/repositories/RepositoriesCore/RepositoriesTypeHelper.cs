using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>
        /// Convert EmployeeRecord to Dictionary for backward compatibility
        /// </summary>
        public static Dictionary<string, object?>? RecordToDict(this EmployeesRepository.EmployeeRecord? record)
        {
            if (record == null) return null;
            return new Dictionary<string, object?>
            {
                { "UUID", record.UUID },
                { "user_id", record.UserId },
                { "name", record.Name },
                { "department", record.Department },
                { "workstation_id", record.WorkstationId },
                { "preference", record.Preference },
                { "created_at", record.CreatedAt },
                { "updated_at", record.UpdatedAt }
            };
        }

        /// <summary>
        /// Convert Dictionary to EmployeeRecord for backward compatibility
        /// </summary>
        public static EmployeeRecord? DictToEmployeeRecord(this Dictionary<string, object?>? dict)
        {
            if (dict == null) return null;

            try
            {
                // 安全地获取值并转换类型
                var uuid = RepositoriesTypeHelper.GetStringValue(dict, "UUID") ?? "";
                var userId = RepositoriesTypeHelper.GetStringValue(dict, "user_id") ?? "";
                var name = RepositoriesTypeHelper.GetStringValue(dict, "name") ?? "";
                var department = RepositoriesTypeHelper.GetStringValue(dict, "department") ?? "";
                var workstationId = RepositoriesTypeHelper.GetStringValue(dict, "workstation_id");
                var preference = RepositoriesTypeHelper.GetStringValue(dict, "preference");
                var createdAt = RepositoriesTypeHelper.GetDateTimeValue(dict, "created_at") ?? DateTime.Now;
                var updatedAt = RepositoriesTypeHelper.GetDateTimeValue(dict, "updated_at") ?? DateTime.Now;

                return new EmployeeRecord(uuid, userId, name, department, workstationId, preference, createdAt, updatedAt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert dictionary to EmployeeRecord: {ex.Message}", ex);
            }
        }
    }
    public static class ActivityLogsRecordExtensions
    {
        public static Dictionary<string, object?>? RecordToDict(this ActivityLogsRepository.ActivityLogRecord? record)
        {
            if (record == null) return null;
            return new Dictionary<string, object?>
            {
                { "UUID", record.UUID },
                { "log_id", record.LogId },
                { "user_id", record.UserId },
                { "activity_type", record.ActivityType },
                { "start_time", record.StartTime },
                { "end_time", record.EndTime },
                { "duration", record.Duration },
                { "created_at", record.CreatedAt }
            };
        }

        public static ActivityLogsRepository.ActivityLogRecord? DictToActivityLogsRecord(this Dictionary<string, object?>? dict)
        {
            if (dict == null) return null;
            try
            {
                var uuid = RepositoriesTypeHelper.GetStringValue(dict, "UUID") ?? "";
                var logId = RepositoriesTypeHelper.GetStringValue(dict, "log_id") ?? "";
                var UserId = RepositoriesTypeHelper.GetStringValue(dict, "user_id") ?? "";
                var activityType = RepositoriesTypeHelper.GetStringValue(dict, "activity_type") ?? "";
                var startTime = RepositoriesTypeHelper.GetDateTimeValue(dict, "start_time") ?? DateTime.Now;
                var endTime = RepositoriesTypeHelper.GetDateTimeValue(dict, "end_time") ?? DateTime.Now;
                var duration = RepositoriesTypeHelper.GetIntValue(dict, "duration") ?? 0;
                var createdAt = RepositoriesTypeHelper.GetDateTimeValue(dict, "created_at") ?? DateTime.Now;

                return new ActivityLogRecord(uuid, logId, UserId, activityType, startTime, endTime, duration, createdAt);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to convert dictionary to ActivityLogRecord: {ex.Message}", ex);
            }
        }
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
