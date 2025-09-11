using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RepositoriesCore
{
    public static class RepositoriesFactory
    {
        // 原有的异步版本工厂方法
        public static EmployeesRepository CreateEmployeesRepository(string? connectionString)
        {
            return new EmployeesRepository(connectionString);
        }
        
        public static ActivityLogsRepository CreateActivityLogsRepository(string? connectionString)
        {
            return new ActivityLogsRepository(connectionString);
        }
        
        public static RecommandationRepository CreateRecommandationRepository(string? connectionString)
        {
            return new RecommandationRepository(connectionString);
        }

        // 新增的同步版本工厂方法，用于pythonnet兼容

        /// <summary>
        /// 创建员工Repository同步版本
        /// </summary>
        public static EmployeesSyncRepository CreateEmployeesSyncRepository(string? connectionString)
        {
            return new EmployeesSyncRepository(connectionString);
        }

        /// <summary>
        /// 创建活动日志Repository同步版本
        /// </summary>
        public static ActivityLogsSyncRepository CreateActivityLogsSyncRepository(string? connectionString)
        {
            return new ActivityLogsSyncRepository(connectionString);
        }

        /// <summary>
        /// 创建推荐Repository同步版本
        /// </summary>
        public static RecommendationsSyncRepository CreateRecommendationsSyncRepository(string? connectionString)
        {
            return new RecommendationsSyncRepository(connectionString);
        }

        /// <summary>
        /// 通用同步Repository创建方法
        /// </summary>
        public static IRepositorySync CreateSyncRepository(string repositoryType, string? connectionString)
        {
            return repositoryType.ToLowerInvariant() switch
            {
                "employees" => new EmployeesSyncRepository(connectionString),
                "activitylogs" => new ActivityLogsSyncRepository(connectionString),
                "recommendations" => new RecommendationsSyncRepository(connectionString),
                _ => throw new ArgumentException($"Unknown repository type: {repositoryType}")
            };
        }
    }
}
