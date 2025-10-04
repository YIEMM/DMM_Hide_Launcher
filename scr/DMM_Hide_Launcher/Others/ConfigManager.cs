using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// 配置管理器类
    /// 负责配置文件的读取、写入和管理操作
    /// </summary>
    public static class ConfigManager
    {
        /// <summary>
        /// 配置文件路径
        /// </summary>
        private const string ConfigFilePath = "config.json";
        
        /// <summary>
        /// JSON序列化选项
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true, // 格式化输出
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 支持中文等非ASCII字符
        };
        
        /// <summary>
        /// 加载配置文件
        /// </summary>
        /// <returns>配置对象</returns>
        public static Config LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    return JsonSerializer.Deserialize<Config>(json) ?? CreateDefaultConfig();
                }
                catch (Exception)
                {
                    // 读取失败时创建默认配置
                    return CreateDefaultConfig();
                }
            }
            else
            {
                // 文件不存在时创建默认配置
                return CreateDefaultConfig();
            }
        }
        
        /// <summary>
        /// 保存配置文件
        /// </summary>
        /// <param name="config">配置对象</param>
        public static void SaveConfig(Config config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, JsonOptions);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                App.LogError("保存配置文件时出错", ex);
                throw;
            }
        }
        
        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认配置对象</returns>
        public static Config CreateDefaultConfig()
        {
            Config config = new Config
            {
                GamePath = "",
                ID7K = ""
            };
            
            try
            {
                SaveConfig(config);
            }
            catch (Exception)
            {
                // 保存失败时不影响返回默认配置
            }
            
            return config;
        }
        
        /// <summary>
        /// 设置游戏路径
        /// </summary>
        /// <param name="path">游戏路径</param>
        public static void SetGamePath(string path)
        {
            Config config = LoadConfig();
            config.GamePath = path;
            SaveConfig(config);
        }
        
        /// <summary>
        /// 获取账号列表
        /// </summary>
        /// <returns>账号列表</returns>
        public static List<Account> GetAccounts()
        {
            Config config = LoadConfig();
            return config.Accounts ?? new List<Account>();
        }
        
        /// <summary>
        /// 添加或更新账号
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        public static void AddOrUpdateAccount(string username, string password)
        {
            Config config = LoadConfig();
            
            // 查找是否已存在该账号
            Account existingAccount = config.Accounts.Find(a => a.Username == username);
            
            if (existingAccount != null)
            {
                // 更新密码
                existingAccount.Password = password;
            }
            else
            {
                // 添加新账号
                config.Accounts.Add(new Account { Username = username, Password = password });
            }
            
            SaveConfig(config);
        }
        
        /// <summary>
        /// 删除账号
        /// </summary>
        /// <param name="username">用户名</param>
        public static void DeleteAccount(string username)
        {
            Config config = LoadConfig();
            config.Accounts.RemoveAll(a => a.Username == username);
            SaveConfig(config);
        }
    }
}