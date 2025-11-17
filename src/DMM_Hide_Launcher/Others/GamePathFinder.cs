using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using DMM_Hide_Launcher;
using System.Diagnostics;
using System.Windows;
using System.Collections.Concurrent;
using System;
using System.Threading;
using MessageBox = AdonisUI.Controls.MessageBox;

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

                                            if (displayName != null && 
                                                (displayName.IndexOf("dmmdzz", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                                 displayName.IndexOf("逃跑吧少年", StringComparison.OrdinalIgnoreCase) >= 0 || 
                                                 displayName.IndexOf("逃跑吧！少年", StringComparison.OrdinalIgnoreCase) >= 0) &&
                                                !string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
                                            {
                                                if (results.Add(installLocation))
                                                {
                                                    App.Log($"从卸载注册表找到游戏路径: {installLocation}");
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // 记录警告日志
                                    App.LogWarning($"访问注册表项{registryPath}\\{subKeyName}时出错: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("注册表搜索错误", ex);
            }

            App.Log($"注册表搜索完成，找到{results.Count}个游戏路径");
            return results.ToList();
        }

        // 搜索常见安装路径
        private List<string> SearchCommonPaths()
        {
            App.Log("开始搜索常见安装路径");
            
            // 使用HashSet提高去重效率
            HashSet<string> results = new HashSet<string>();
            // 预先调整容量以减少重新哈希的开销
            results.EnsureCapacity(_commonInstallPaths.Length * 2);

            try
            {
                // 检查常见安装路径
                App.Log("检查预定义的常见安装路径");
                foreach (string path in _commonInstallPaths)
                {
                    App.Log($"检查路径: {path}");
                    if (Directory.Exists(path))
                    {
                        if (results.Add(path))
                        {
                            App.Log($"找到有效的游戏路径: {path}");
                        }

                    }

                }

                // 搜索所有本地驱动器的Program Files目录
                App.Log("开始搜索所有本地驱动器的Program Files目录");
                DriveInfo[] drives = DriveInfo.GetDrives();
                // 预过滤以避免多次重复计算
                var fixedReadyDrives = drives.Where(d => d.IsReady && d.DriveType == DriveType.Fixed).ToList();
                int readyDrivesCount = fixedReadyDrives.Count;
                App.Log($"找到{readyDrivesCount}个可用的固定驱动器");
                // 优化：只在有有效驱动器时执行后续操作
                if (readyDrivesCount > 0)
                {
                    foreach (DriveInfo drive in fixedReadyDrives)
                    {
                        App.Log($"检查驱动器: {drive.RootDirectory.FullName}");
                        string driveRoot = drive.RootDirectory.FullName;
                        
                        // 避免对光盘驱动器进行长时间搜索
                        if (driveRoot.Equals("D:\\", StringComparison.OrdinalIgnoreCase) && drive.DriveType == DriveType.CDRom)
                            continue;
                        
                        // 使用预构建的路径组合以减少内存分配
                        CheckAndAddPath(Path.Combine(driveRoot, "Program Files", "dmmdzz_4399"), results);
                        CheckAndAddPath(Path.Combine(driveRoot, "Program Files (x86)", "dmmdzz_4399"), results);
                        CheckAndAddPath(Path.Combine(driveRoot, "Program Files", "dmmdzz_7k7k"), results);
                        CheckAndAddPath(Path.Combine(driveRoot, "Program Files (x86)", "dmmdzz_7k7k"), results);
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("常见路径搜索错误", ex);
            }

            App.Log($"常见路径搜索完成，找到{results.Count}个游戏路径");
            return results.ToList();
        }

        // 检查路径是否存在并添加到结果集合
        private void CheckAndAddPath(string path, HashSet<string> results)
        {
            App.Log($"检查并添加路径: {path}");
            try
            {
                if (Directory.Exists(path))
                {
                    if (results.Add(path))
                    {
                        App.Log($"添加有效的游戏路径: {path}");
                    }

                }

            }
            catch (Exception ex)
            {
                // 即使禁用详细日志，错误也需要记录
                App.LogError($"检查路径错误: {path}", ex);
            }
        }

        // 验证路径是否包含游戏可执行文件
        public bool ValidateGamePath(string path)
        {
            App.Log($"开始验证游戏路径: {path}");
            
            // 参数验证
            if (string.IsNullOrEmpty(path))
            {
                    App.Log("路径为空");
                    return false;
                }
            
            try
            {
                if (!Directory.Exists(path))
                {
                    App.Log($"路径不存在，验证失败: {path}");
                    return false;
                }

                App.Log($"路径存在，检查是否包含游戏可执行文件");
                // 检查是否包含游戏可执行文件
                App.Log($"检查可执行文件: {string.Join(", ", _gameExecutables)}");
                
                // 优化：先检查主目录
                foreach (string exeName in _gameExecutables)
                {
                    string exePath = Path.Combine(path, exeName);
                    if (File.Exists(exePath))
                    {
                        App.Log($"找到游戏可执行文件: {exePath}，验证通过");
                        return true;
                    }
                }

                App.Log($"直接目录中未找到可执行文件，检查子目录");
                // 检查子目录
                string[] subDirectories = Directory.GetDirectories(path);
                
                // 优化：提前返回条件判断，减少不必要的循环
                if (subDirectories.Length > 0)
                {
                    App.Log($"找到{subDirectories.Length}个子目录");
                    // 优化：使用并行检查来加速验证过程
                    bool found = false;
                    
                    // 对于少量子目录，顺序检查更高效
                    if (subDirectories.Length <= 5)
                    {
                        foreach (string dir in subDirectories)
                        {
                            foreach (string exeName in _gameExecutables)
                            {
                                if (File.Exists(Path.Combine(dir, exeName)))
                                {
                                    App.Log($"在子目录{dir}中找到游戏可执行文件{exeName}，验证通过");
                                    return true;
                                }
                            }
                        }
                    }
                    else
                    {
                        // 对于大量子目录，使用并行检查
                        Parallel.ForEach(subDirectories, (dir, state) =>
                        {
                            foreach (string exeName in _gameExecutables)
                            {
                                if (File.Exists(Path.Combine(dir, exeName)))
                                {
                                    found = true;
                                    state.Break();
                                }
                            }
                        });
                        
                        if (found)
                            return true;
                    }
                }

                App.Log($"在路径及子目录中未找到游戏可执行文件，验证失败: {path}");
                return false;
            }
            catch (Exception ex)
            {
                // 即使禁用详细日志，错误也需要记录
                App.LogError($"验证路径时发生异常: {path}", ex);
                return false;
            }
        }

        // 搜索正在运行的游戏进程
        /// <summary>
        /// 搜索正在运行的游戏进程
        /// 通过直接获取特定名称的进程来提高搜索效率，避免遍历所有进程
        /// 从找到的游戏进程中提取游戏安装路径
        /// </summary>
        /// <returns>找到的游戏安装路径列表</returns>
        private List<string> SearchRunningProcesses()
        {
            App.Log("开始搜索正在运行的游戏进程");
            
            // 使用HashSet提高去重效率
            HashSet<string> results = new HashSet<string>();
            int accessDeniedCount = 0;
            bool showMessageBox = false;

            try
            {
                foreach (string exeName in _gameExecutables)
                {
                    App.Log($"搜索进程: {exeName}");
                    string exeNameWithoutExtension = Path.GetFileNameWithoutExtension(exeName);
                    
                    // 优化：使用GetProcessesByName直接获取指定名称的进程，避免遍历所有进程
                    try
                    {
                        Process[] gameProcesses = Process.GetProcessesByName(exeNameWithoutExtension);
                        App.Log($"找到{gameProcesses.Length}个名称为{exeNameWithoutExtension}的进程");

                        foreach (Process process in gameProcesses)
                        {
                            try
                            {
                                // 安全获取进程的主模块文件路径，避免不必要的异常
                                string processPath = null;
                                try
                                {
                                    if (process.MainModule != null && !string.IsNullOrEmpty(process.MainModule.FileName))
                                    {
                                        processPath = process.MainModule.FileName;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // 只在第一次遇到权限问题时显示MessageBox
                                    if (ex.Message.Contains("拒绝访问") && !showMessageBox)
                                    {
                                        accessDeniedCount++;
                                        App.LogWarning($"访问进程{process.ProcessName}(ID: {process.Id})信息时出错: {ex.Message}。这可能是因为进程以管理员权限运行，而当前程序没有足够权限访问。");
                                        showMessageBox = true;
                                    }
                                    else
                                    {
                                        App.LogWarning($"访问进程{process.ProcessName}(ID: {process.Id})信息时出错: {ex.Message}");
                                    }
                                    continue;
                                }

                                if (!string.IsNullOrEmpty(processPath))
                                {
                                    string gamePath = Path.GetDirectoryName(processPath);
                                    if (!string.IsNullOrEmpty(gamePath))
                                    {
                                        // 特殊处理dmmdzz.exe进程的路径
                                        if (string.Equals(exeName, dmmdzzExe, StringComparison.OrdinalIgnoreCase))
                                        {
                                            string lowerGamePath = gamePath.ToLower();
                                            // 检查路径是否包含dmmdzz_4399\game*模式
                                            int index4399 = lowerGamePath.IndexOf(dmmdzz4399Game);
                                            if (index4399 >= 0)
                                            {
                                                // 只使用dmmdzz_4399目录（保留原始大小写，移除尾部反斜杠）
                                                gamePath = gamePath.Substring(0, index4399 + dmmdzz4399Length);
                                                App.Log($"对dmmdzz.exe进程应用特殊路径处理，修改为: {gamePath}");
                                            }
                                            // 检查路径是否包含dmmdzz_7k7k\game*模式
                                            else
                                            {
                                                int index7k7k = lowerGamePath.IndexOf(dmmdzz7k7kGame);
                                                if (index7k7k >= 0)
                                                {
                                                    // 只使用dmmdzz_7k7k目录（保留原始大小写，移除尾部反斜杠）
                                                    gamePath = gamePath.Substring(0, index7k7k + dmmdzz7k7kLength);
                                                    App.Log($"对dmmdzz.exe进程应用特殊路径处理，修改为: {gamePath}");
                                                }
                                            }
                                        }
                                          
                                        // 使用HashSet的Add方法自动去重
                                        if (results.Add(gamePath))
                                        {
                                            App.Log($"从运行进程找到游戏路径: {gamePath}");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                // 防止在处理一个进程时影响整个搜索过程
                                App.LogWarning($"处理进程{process?.ProcessName}时发生错误: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.LogWarning($"获取进程{exeNameWithoutExtension}时发生错误: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 即使禁用详细日志，错误也需要记录
                App.LogError("搜索运行进程时发生错误", ex);
            }

            // 如果有进程因为权限问题无法访问，在操作完成后统一显示一次提示
            if (showMessageBox)
            {
                // 使用Dispatcher将MessageBox调用切换到UI线程
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("无法通过进程访问程序路径，可以尝试通过管理员启动当前程序\n\n当然您可以继续通过其他方式获取程序路径", 
                        "警告", 
                        AdonisUI.Controls.MessageBoxButton.OK, 
                        AdonisUI.Controls.MessageBoxImage.Information);
                });
            }


              
            App.Log($"进程搜索完成，找到{results.Count}个游戏路径");
            return results.ToList();
        }
    }
}
