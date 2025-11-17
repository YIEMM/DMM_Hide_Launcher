using System;
using System.Collections.Generic;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// 配置类
    /// 用于存储应用程序的配置数据，支持JSON序列化和反序列化
    /// </summary>
    [Serializable]
    public class Config
    {
        /// <summary>
        /// 游戏路径
        /// </summary>
        public string GamePath { get; set; } = "";
        
        /// <summary>
        /// 7K7K账号ID
        /// </summary>
        public string ID7K { get; set; } = "";
        

        
        /// <summary>
        /// 账号列表
        /// </summary>
        public List<Account> Accounts { get; set; } = new List<Account>();
    }
    
    /// <summary>
    /// 账号类
    /// 用于存储用户名和密码信息
    /// </summary>
    [Serializable]
    public class Account
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; }
    }
}