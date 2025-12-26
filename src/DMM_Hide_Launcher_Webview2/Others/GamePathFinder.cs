using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Collections.Concurrent;

// 忽略Windows特定API的平台兼容性警告
#pragma warning disable CA1416

namespace DMM_Hide_Launcher.Others
{
    public class GamePathFinder
    {
        // 游戏可执行文件名称
        private readonly string[] _gameExecutables = { "dmmdzz.exe", "DmmdzzLoader.exe", "hide_pc_launcher.exe" };
        
        // 预定义的字符串常量，减少字符串构建
        private const string dmmdzzExe = "dmmdzz.exe";
        private const string dmmdzz4399Game = "dmmdzz_4399\\game";
        private const string dmmdzz7k7kGame = "dmmdzz_7k7k\\game";
        private const int dmmdzz4399Length = 11; // "dmmdzz_4399".Length
        private const int dmmdzz7k7kLength = 11; // "dmmdzz_7k7k".Length

        // 常见游戏安装目录
        private readonly string[] _commonInstallPaths = {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dmmdzz_4399"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dmmdzz_4399"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dmmdzz_7k7k"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dmmdzz_7k7k"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dmmdzz_4399"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dmmdzz_7k7k")
        };

        // 注册表中可能包含游戏路径的位置
        private readonly string[] _registryPaths = {
            @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store",
            @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        // 要查找的路径片段
        private readonly string[] _targetFragments = new string[]
        {
            @"dmmdzz_4399\DmmdzzLoader.exe",
            @"dmmdzz_4399\hide_pc_launcher.exe",
            @"dmmdzz_7k7k\hide_pc_launcher.exe",
            @"dmmdzz_7k7k\DmmdzzLoader.exe"
        };

        public List<string> FindGamePaths()
        {
            App.Log("开始执行游戏路径查找操作");
            
            // 使用HashSet提高去重效率
            ConcurrentBag<string> allResults = new ConcurrentBag<string>();

            try
            {
                App.Log("初始化搜索任务列表");
                
                // 创建任务列表并行搜索
                List<Task> searchTasks = new List<Task>
                {
                    Task.Run(() => AddResultsToBag(SearchRegistry(), allResults)),
                    Task.Run(() => AddResultsToBag(SearchCommonPaths(), allResults)),
                    Task.Run(() => AddResultsToBag(SearchRunningProcesses(), allResults))
                };

                App.Log("等待所有搜索任务完成");
                // 等待所有搜索任务完成
                Task.WaitAll(searchTasks.ToArray());

                // 使用HashSet去重并验证路径
                HashSet<string> validatedUniqueResults = new HashSet<string>();
                // 优化：预先调整容量以减少重新哈希的开销
                validatedUniqueResults.EnsureCapacity(allResults.Count);
                
                foreach (string path in allResults)
                {
                    if (!validatedUniqueResults.Contains(path) && ValidateGamePath(path))
                    {
                        validatedUniqueResults.Add(path);
                    }
                }

                // 按路径的最后修改时间排序结果
                List<string> finalResults = validatedUniqueResults.OrderByDescending(path =>
                {
                    try
                    {
                        return Directory.GetLastWriteTime(path);
                    }
                    catch
                    {
                        return DateTime.MinValue;
                    }
                }).ToList();

                App.Log($"路径查找完成，共找到{finalResults.Count}个有效游戏路径");
                return finalResults;
            }
            catch (Exception ex)
            {
                App.LogError("自动查找游戏路径时发生错误", ex);
                return new List<string>();
            }
        }
        
        // 辅助方法：将结果添加到ConcurrentBag
        private void AddResultsToBag(List<string> results, ConcurrentBag<string> bag)
        {
            if (results != null)
            {
                foreach (string path in results)
                {
                    bag.Add(path);
                }
            }
        }

        // 从注册表搜索游戏路径
        private List<string> SearchRegistry()
        {
            App.Log("开始从注册表搜索游戏路径");
            
            // 使用HashSet提高去重效率
            HashSet<string> results = new HashSet<string>();

            try
            {
                foreach (string registryPath in _registryPaths)
                {
                    App.Log($"搜索注册表路径: {registryPath}");
                    
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                    {
                        if (key == null)
                        {
                            App.Log($"注册表键不存在: {registryPath}");
                            continue;
                        }

                        // 处理AppCompatFlags路径特殊情况
                        if (registryPath.Contains("AppCompatFlags"))
                        {
                            string[] valueNames = key.GetValueNames();
                            App.Log($"在{registryPath}下找到{valueNames.Length}个值");
                            
                            foreach (string name in valueNames)
                            {
                                foreach (string fragment in _targetFragments)
                                {
                                    if (name.Contains(fragment))
                                    {
                                        int index = name.LastIndexOf('\\');
                                        if (index > 0)
                                        {
                                            string resultPath = name.Substring(0, index);
                                            if (results.Add(resultPath))
                                            {
                                                App.Log($"从注册表找到游戏路径: {resultPath}");
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 处理卸载注册表项
                            App.Log("处理卸载注册表项");
                            
                            string[] subKeyNames = key.GetSubKeyNames();
                            App.Log($"在{registryPath}下找到{subKeyNames.Length}个子键");
                            
                            // 预先调整HashSet容量以减少重新哈希开销
                            results.EnsureCapacity(results.Count + subKeyNames.Length);
                            
                            foreach (string subKeyName in subKeyNames)
                            {
                                try
                                {
                                    using (RegistryKey subKey = key.OpenSubKey(subKeyName))
                                    {
                                        if (subKey != null)
                                        {
                                            string displayName = subKey.GetValue("DisplayName") as string;
                                            string installLocation = subKey.GetValue("InstallLocation") as string;

                                            // 如果安装位置有效且包含游戏名称
                                            if (!string.IsNullOrEmpty(installLocation) && 
                                                Directory.Exists(installLocation) && 
                                                (installLocation.Contains("dmmdzz_4399") || installLocation.Contains("dmmdzz_7k7k")))
                                            {
                                                if (results.Add(installLocation))
                                                {
                                                    App.Log($"从注册表找到游戏路径: {installLocation}");
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    App.LogError($"处理注册表子键{subKeyName}时出错", ex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("注册表搜索时发生错误", ex);
            }

            return results.ToList();
        }

        // 搜索常见路径
        private List<string> SearchCommonPaths()
        {
            App.Log("开始搜索常见游戏路径");
            
            HashSet<string> results = new HashSet<string>();

            try
            {
                foreach (string path in _commonInstallPaths)
                {
                    if (Directory.Exists(path))
                    {
                        App.Log($"检查常见路径: {path}");
                        
                        // 搜索游戏可执行文件
                        foreach (string exe in _gameExecutables)
                        {
                            string exePath = Path.Combine(path, exe);
                            if (File.Exists(exePath))
                            {
                                if (results.Add(path))
                                {
                                    App.Log($"在常见路径中找到游戏: {path}");
                                }
                                break;
                            }
                        }

                        // 检查子目录
                        string[] subDirs = Directory.GetDirectories(path);
                        foreach (string subDir in subDirs)
                        {
                            foreach (string exe in _gameExecutables)
                            {
                                string exePath = Path.Combine(subDir, exe);
                                if (File.Exists(exePath))
                                {
                                    string gamePath = Path.GetDirectoryName(exePath);
                                    if (results.Add(gamePath))
                                    {
                                        App.Log($"在子目录中找到游戏: {gamePath}");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("搜索常见路径时发生错误", ex);
            }

            return results.ToList();
        }

        // 搜索运行中的进程
        private List<string> SearchRunningProcesses()
        {
            App.Log("开始搜索运行中的游戏进程");
            
            HashSet<string> results = new HashSet<string>();

            try
            {
                foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcesses())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(process.MainModule?.FileName))
                        {
                            string processPath = process.MainModule.FileName;
                            foreach (string exe in _gameExecutables)
                            {
                                if (processPath.EndsWith(exe, StringComparison.OrdinalIgnoreCase))
                                {
                                    string gamePath = Path.GetDirectoryName(processPath);
                                    if (results.Add(gamePath))
                                    {
                                        App.Log($"从运行进程中找到游戏: {gamePath}");
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 忽略访问被拒绝的进程
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("搜索运行进程时发生错误", ex);
            }

            return results.ToList();
        }

        // 验证游戏路径是否有效
        public bool ValidateGamePath(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
            {
                return false;
            }

            // 检查是否存在游戏可执行文件
            foreach (string exe in _gameExecutables)
            {
                string exePath = Path.Combine(gamePath, exe);
                if (File.Exists(exePath))
                {
                    return true;
                }
            }

            return false;
        }
    }
}