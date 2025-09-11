using System;
using System.Collections.Generic;

namespace RepositoriesCore
{
    /// <summary>
    /// 同步Repository接口，用于pythonnet兼容
    /// </summary>
    public interface IRepositorySync
    {
        string ConnectionString { get; set; }
        string SheetName { get; }
        
        /// <summary>
        /// 初始化数据库表结构
        /// </summary>
        bool InitializeDatabase();
        
        /// <summary>
        /// 检查数据库是否已初始化
        /// </summary>
        bool DatabaseIsInitialized();
        
        /// <summary>
        /// 添加新记录
        /// </summary>
        bool AddNewRecords(Dictionary<string, object?>[] records);
        
        /// <summary>
        /// 根据UUID读取记录
        /// </summary>
        Dictionary<string, object?>[]? ReadRecords(string[] uuids);
        
        /// <summary>
        /// 根据UUID删除记录
        /// </summary>
        bool DeleteRecords(string[] uuids);
        
        /// <summary>
        /// 更新记录
        /// </summary>
        bool UpdateRecord(string uuid, Dictionary<string, object?> record);
        
        /// <summary>
        /// 搜索记录
        /// </summary>
        Dictionary<string, object?>[]? SearchRecords(string searchTarget, object content);
        
        /// <summary>
        /// 执行自定义SQL命令
        /// </summary>
        string ExecuteCommand(string commandText);
    }
}