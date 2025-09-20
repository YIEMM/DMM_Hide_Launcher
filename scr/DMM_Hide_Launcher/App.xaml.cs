using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace DMM_Hide_Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
    /// 导入Windows API函数，用于附加控制台
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    /// <summary>
    /// 导入Windows API函数，用于释放控制台
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    /// <summary>
    /// 导入Windows API函数，用于获取控制台窗口句柄
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();
    
    /// <summary>
    /// 导入Windows API函数，用于设置控制台输出代码页
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);
    
    /// <summary>
    /// 导入Windows API函数，用于设置控制台输入代码页
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCP(uint wCodePageID);
    
    /// <summary>
    /// 导入Windows API函数，用于获取系统参数
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);
    
    /// <summary>
    /// 导入Windows API函数，用于获取窗口属性
    /// </summary>
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE dwAttribute, out int pvAttribute, int cbAttribute);
    
    /// <summary>
    /// DWM窗口属性枚举
    /// </summary>
    private enum DWMWINDOWATTRIBUTE : uint
    {
        DWMWA_USE_IMMERSIVE_DARK_MODE = 20
    }
    
    /// <summary>
    /// 系统参数信息常量
    /// </summary>
    private const uint SPI_GETUSEDRAGIMAGE = 0x0037;
    private const uint SPI_GETDROPSHADOW = 0x009E;
    private const uint SPI_GETDESKTOPWALLPAPER = 0x0073;
    private const uint SPI_GETICONTITLELOGFONT = 0x001D;
    private const uint SPI_GETNONCLIENTMETRICS = 0x0029;

    /// <summary>
    /// 调试模式标志
    /// </summary>
    public static bool IsDebugMode { get; private set; } = false;
    
    /// <summary>
    /// 日志启用标志
    /// </summary>
    public static bool IsLogEnabled { get; private set; } = false;
    
    /// <summary>
    /// 日志文件路径
    /// </summary>
    private static string LogFilePath { get; set; }
    
    /// <summary>
    /// 用于日志写入的锁对象，确保多线程环境下的文件访问安全
    /// </summary>
    private static readonly object logLock = new object();

        /// <summary>
        /// 应用程序启动事件处理程序
        /// 初始化日志系统、处理命令行参数并启动主窗口
        /// </summary>
        /// <param name="e">启动事件参数</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 初始化系统主题
            InitializeSystemTheme();
            
            // 注册系统主题变化事件监听
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            
            // 添加直接的控制台调试输出，不依赖任何标志
            Console.WriteLine("[DEBUG] 应用程序启动，正在初始化日志系统...");
            
            // 初始化日志文件路径
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Console.WriteLine($"[DEBUG] 日志目录: {logDirectory}");
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            LogFilePath = Path.Combine(logDirectory, $"DMM_Hide_Launcher_{timestamp}.log");
            Console.WriteLine($"[DEBUG] 日志文件路径: {LogFilePath}");
            
            // 先处理命令行参数，设置日志模式标志
            Console.WriteLine("[DEBUG] 正在处理命令行参数...");
            ParseCommandLineArgs(e.Args);
            Console.WriteLine($"[DEBUG] 命令行参数处理完成 - 调试模式: {IsDebugMode}, 日志模式: {IsLogEnabled}");
            
            // 根据日志模式决定是否附加控制台
            if (IsLogEnabled)
            {
                Console.WriteLine("[DEBUG] 日志模式已启用，准备附加控制台...");
                // 强制附加控制台以确保日志可见
                ForceAttachConsole();
                
                // 输出启动信息到控制台
                Console.WriteLine("DMM_Hide_Launcher 启动中...");
                Console.WriteLine($"当前模式: {(IsDebugMode ? "调试模式" : "日志模式")}");
            }
            
            // 现在日志模式已设置，可以开始记录日志
            LogStartupInfo(e.Args);
            
            // 设置异常处理
            App.Log("配置全局异常处理机制");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            App.Log("应用程序启动完成");
            base.OnStartup(e);
        }
        
        /// <summary>
        /// 初始化系统主题
        /// 根据当前系统主题设置应用程序主题
        /// </summary>
        private void InitializeSystemTheme()
        {
            try
            {
                bool isDarkMode = IsSystemDarkMode();
                App.Log($"检测到系统主题: {(isDarkMode ? "暗色" : "亮色")}");
                SetAppTheme(isDarkMode);
            }
            catch (Exception ex)
            {
                App.LogError("初始化系统主题时出错", ex);
                // 默认使用亮色主题
                SetAppTheme(false);
            }
        }
        
        /// <summary>
        /// 检测系统是否处于暗色模式
        /// </summary>
        /// <returns>如果系统是暗色模式则返回true，否则返回false</returns>
        public bool IsSystemDarkMode()
        {
            try
            {
                // 尝试通过DWM API检测Windows 10/11的暗色模式
                IntPtr hwnd = Process.GetCurrentProcess().MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    int useImmersiveDarkMode = 0;
                    int result = DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_USE_IMMERSIVE_DARK_MODE, out useImmersiveDarkMode, sizeof(int));
                    if (result == 0 && useImmersiveDarkMode == 1)
                    {
                        return true;
                    }
                }
                
                // 尝试通过注册表检测暗色模式
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int intValue)
                        {
                            return intValue == 0; // 0表示暗色模式，1表示亮色模式
                        }
                    }
                }
                
                // 默认返回亮色模式
                return false;
            }
            catch (Exception ex)
            {
                App.LogError("检测系统主题时出错", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 设置应用程序主题
        /// </summary>
        /// <param name="useDarkTheme">是否使用暗色主题</param>
        public void SetAppTheme(bool useDarkTheme)
        {
            try
            {
                App.Log(useDarkTheme ? "准备切换到暗色主题" : "准备切换到亮色主题");
                
                // 清除当前的资源字典
                System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
                
                if (useDarkTheme)
                {
                    // 添加暗色主题资源
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDark.xaml")
                    });
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml")
                    });
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/AdonisUI;component/ColorSchemes/Dark.xaml")
                    });
                }
                else
                {
                    // 添加亮色主题资源
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/HandyControl;component/Themes/SkinDefault.xaml")
                    });
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/HandyControl;component/Themes/Theme.xaml")
                    });
                    System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/AdonisUI;component/ColorSchemes/Light.xaml")
                    });
                }
                
                // 添加通用的ClassicTheme资源
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml")
                });
                
                // 添加全局字体设置
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    { "MiSansFont", new FontFamily("pack://application:,,,/DMM_Hide_Launcher;component/public/Fonts/MiSans-Medium.ttf#MiSans") }
                });
                
                App.Log("主题更新完成");
            }
            catch (Exception ex)
            {
                App.LogError("更新主题时出错", ex);
            }
        }
        
        /// <summary>
        /// 系统主题变化事件处理程序
        /// </summary>
        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General || e.Category == UserPreferenceCategory.VisualStyle)
            {
                // 当系统主题发生变化时，更新应用程序主题
                bool isDarkMode = IsSystemDarkMode();
                App.Log($"检测到系统主题变化，当前主题: {(isDarkMode ? "暗色" : "亮色")}");
                SetAppTheme(isDarkMode);
            }
        }
        
        /// <summary>
        /// 强制附加控制台
        /// </summary>
        private void ForceAttachConsole()
        {
            try
            {
                App.Log("开始附加控制台");
                
                // 尝试获取标准输出流，检查是否已连接到控制台
                var stdout = Console.Out;
                bool isConsoleAttached = true;
                
                // 测试写入是否会引发异常
                try
                {
                    Console.WriteLine("[DEBUG] 测试控制台写入能力...");
                }
                catch
                {
                    isConsoleAttached = false;
                }
                
                // 如果没有控制台或者测试失败，尝试分配一个新的控制台
                if (!isConsoleAttached || GetConsoleWindow() == IntPtr.Zero)
                {
                    App.Log("当前无控制台或控制台不可用，尝试附加新控制台");
                    Console.WriteLine("[DEBUG] 当前无控制台或控制台不可用，尝试附加新控制台...");
                    bool allocResult = AllocConsole();
                    App.Log($"AllocConsole 结果: {allocResult}");
                    Console.WriteLine($"[DEBUG] AllocConsole 结果: {allocResult}");
                }
                
                // 设置控制台代码页为UTF-8以支持中文显示
                const uint CP_UTF8 = 65001;
                try
                {
                    bool outputCPResult = SetConsoleOutputCP(CP_UTF8);
                    bool inputCPResult = SetConsoleCP(CP_UTF8);
                    App.Log($"设置控制台UTF-8编码 - 输出: {outputCPResult}, 输入: {inputCPResult}");
                    Console.WriteLine($"[DEBUG] 设置控制台UTF-8编码 - 输出: {outputCPResult}, 输入: {inputCPResult}");
                }
                catch (Exception ex)
                {
                    App.Log($"设置控制台编码失败: {ex.Message}");
                    Console.WriteLine($"[DEBUG] 设置控制台编码失败: {ex.Message}");
                }
                
                // 确保标准输出流被重新初始化
                try
                {
                    // 尝试重新打开标准输出流，显式设置UTF8编码以支持中文
                    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });
                    App.Log("已成功重新初始化标准输出流并设置UTF8编码");
                    Console.WriteLine("[DEBUG] 已成功重新初始化标准输出流并设置UTF8编码");
                }
                catch (Exception ex)
                {
                    App.Log($"重新初始化标准输出流失败: {ex.Message}");
                    Console.WriteLine($"[DEBUG] 重新初始化标准输出流失败: {ex.Message}");
                }
                
                // 测试中文显示
                string chineseTest = "中文测试显示 - Console.WriteLine";
                Console.WriteLine(chineseTest);
                App.Log("中文测试显示 - App.Log");
                
                // 在应用程序退出时释放控制台
                this.Exit += (sender, args) => 
                {
                    try
                    {
                        FreeConsole();
                    }
                    catch { }
                };
                
                App.Log("控制台附加完成");
                Console.WriteLine("[DEBUG] 控制台附加完成");
            }
            catch (Exception ex)
            {
                App.Log($"控制台附加过程中出现异常: {ex.Message}");
                Console.WriteLine($"[DEBUG] 控制台附加过程中出现异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 解析命令行参数，只设置标志，不记录日志
        /// </summary>
        private void ParseCommandLineArgs(string[] args)
        {
            bool showSecurityWarning = false;
            string mode = "";

            // 处理命令行参数
            foreach (string arg in args)
            {
                if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase))
                {
                    IsDebugMode = true;
                    IsLogEnabled = true;
                    mode = "调试模式";
                    showSecurityWarning = true;
                }
                else if (arg.Equals("--log", StringComparison.OrdinalIgnoreCase))
                {
                    IsLogEnabled = true;
                    mode = "日志模式";
                    showSecurityWarning = true;
                }
            }

            // 显示安全提示
            if (showSecurityWarning)
            {
                // 在日志目录创建之前，可能需要先显示消息框
                System.Windows.MessageBox.Show(
                    $"{mode}已启用\n\n警告：此模式可能会在日志文件中记录敏感信息（如账号、密码、配置等）。\n请确保在使用后妥善保管日志文件，避免泄露个人信息。",
                    "安全提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }
        
        /// <summary>
        /// 记录启动信息，此时IsLogEnabled已经设置
        /// </summary>
        private void LogStartupInfo(string[] args)
        {
            if (!IsLogEnabled)
                return;
                
            // 确保日志目录存在
            string logDirectory = Path.GetDirectoryName(LogFilePath);
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            
            // 记录命令行参数信息
            App.Log("开始处理命令行参数");
            App.Log($"应用程序基目录: {AppDomain.CurrentDomain.BaseDirectory}");
            App.Log($"日志文件路径: {LogFilePath}");
            if (args.Length > 0)
            {
                App.Log($"接收到 {args.Length} 个命令行参数: {string.Join(", ", args)}");
            }
            else
            {
                App.Log("未接收到命令行参数");
            }
            
            // 如果是调试模式，记录额外信息
            if (IsDebugMode)
            {
                App.Log("调试模式已启用");
            }
            else if (IsLogEnabled)
            {
                App.Log("日志模式已启用");
            }
            
            // 记录最终配置状态
            App.Log($"命令行参数处理完成 - 调试模式: {IsDebugMode}, 日志模式: {IsLogEnabled}");
        }

        private void ProcessCommandLineArgs(string[] args)
        {
            // 保留旧方法以保持兼容性，但实际上不再使用
            ParseCommandLineArgs(args);
            LogStartupInfo(args);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogError("UI线程异常: " + e.Exception.Message, e.Exception);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception exception = e.ExceptionObject as Exception;
            LogError("非UI线程异常: " + (exception?.Message ?? "未知异常"), exception);
        }

        /// <summary>
        /// 记录详细日志消息
        /// </summary>
        public static void Log(string message)
        {
            try
            {
                // 非日志模式下直接返回，避免不必要的处理
                if (!IsLogEnabled && !IsDebugMode)
                    return;

                // 仅在调试模式下获取调用方信息（性能开销较大）
                string callerInfo = IsDebugMode ? "Unknown Caller" : "App";
                if (IsDebugMode)
                {
                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(1, false);
                    if (stackTrace.FrameCount > 0)
                    {
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                        System.Reflection.MethodBase method = frame.GetMethod();
                        callerInfo = $"{method.DeclaringType?.FullName}.{method.Name}";
                    }
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO [进程ID: {System.Diagnostics.Process.GetCurrentProcess().Id}] [线程ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}] [{callerInfo}]: {message}\n";
                
                // 仅在日志模式或调试模式下输出到控制台
                if (IsLogEnabled || IsDebugMode)
                {
                    try
                    {
                        Console.WriteLine(logEntry.TrimEnd());
                    }
                    catch (Exception consoleEx)
                    {
                        // 如果控制台输出失败，记录到内部变量但不影响程序运行
                        string errorMsg = "控制台输出失败: " + consoleEx.Message;
                    }
                }
                
                if (IsLogEnabled)
                {
                    // 使用锁确保多线程环境下的文件访问安全
                    lock (logLock)
                    {
                        // 确保日志目录存在
                        string logDirectory = Path.GetDirectoryName(LogFilePath);
                        if (!Directory.Exists(logDirectory))
                        {
                            Directory.CreateDirectory(logDirectory);
                        }
                        
                        File.AppendAllText(LogFilePath, logEntry, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果写入日志失败，至少尝试输出到控制台
                Console.WriteLine("写入日志失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 记录详细错误日志
        /// </summary>
        public static void LogError(string message, Exception exception = null)
        {
            if (!IsLogEnabled && !IsDebugMode)
                return;

            try
            {
                // 仅在调试模式下获取调用方信息（性能开销较大）
                string callerInfo = IsDebugMode ? "Unknown Caller" : "App";
                if (IsDebugMode)
                {
                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(1, false);
                    if (stackTrace.FrameCount > 0)
                    {
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                        System.Reflection.MethodBase method = frame.GetMethod();
                        callerInfo = $"{method.DeclaringType?.FullName}.{method.Name}";
                    }
                }

                StringBuilder logEntry = new StringBuilder();
                logEntry.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR [进程ID: {System.Diagnostics.Process.GetCurrentProcess().Id}] [线程ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}] [{callerInfo}]: {message}");

                if (exception != null)
                {
                    logEntry.AppendLine($"异常类型: {exception.GetType().FullName}");
                    logEntry.AppendLine($"异常消息: {exception.Message}");
                    logEntry.AppendLine($"堆栈跟踪: {exception.StackTrace}");

                    if (exception.InnerException != null)
                    {
                        logEntry.AppendLine($"内部异常: {exception.InnerException.Message}");
                        logEntry.AppendLine($"内部堆栈: {exception.InnerException.StackTrace}");
                    }
                }

                // 仅在日志模式或调试模式下输出到控制台
                if (IsLogEnabled || IsDebugMode)
                {
                    try
                    {
                        Console.WriteLine(logEntry.ToString().TrimEnd());
                    }
                    catch (Exception consoleEx)
                    {
                        // 如果控制台输出失败，记录到内部变量但不影响程序运行
                        string errorMsg = "控制台输出失败: " + consoleEx.Message;
                    }
                }

                if (IsLogEnabled)
                {
                    // 使用锁确保多线程环境下的文件访问安全
                    lock (logLock)
                    {
                        // 确保日志目录存在
                        string logDirectory = Path.GetDirectoryName(LogFilePath);
                        if (!Directory.Exists(logDirectory))
                        {
                            Directory.CreateDirectory(logDirectory);
                        }
                        
                        File.AppendAllText(LogFilePath, logEntry.ToString(), Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果写入错误日志失败，至少尝试输出到控制台
                Console.WriteLine("写入错误日志失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void LogWarning(string message)
        {
            if (!IsLogEnabled && !IsDebugMode)
                return;

            try
            {
                // 仅在调试模式下获取调用方信息（性能开销较大）
                string callerInfo = IsDebugMode ? "Unknown Caller" : "App";
                if (IsDebugMode)
                {
                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(1, false);
                    if (stackTrace.FrameCount > 0)
                    {
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                        System.Reflection.MethodBase method = frame.GetMethod();
                        callerInfo = $"{method.DeclaringType?.FullName}.{method.Name}";
                    }
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] WARNING [进程ID: {System.Diagnostics.Process.GetCurrentProcess().Id}] [线程ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}] [{callerInfo}]: {message}\n";
                
                // 仅在日志模式或调试模式下输出到控制台
                if (IsLogEnabled || IsDebugMode)
                {
                    try
                    {
                        Console.WriteLine(logEntry.TrimEnd());
                    }
                    catch (Exception consoleEx)
                    {
                        // 如果控制台输出失败，记录到内部变量但不影响程序运行
                        string errorMsg = "控制台输出失败: " + consoleEx.Message;
                    }
                }
                
                if (IsLogEnabled)
                {
                    // 使用锁确保多线程环境下的文件访问安全
                    lock (logLock)
                    {
                        // 确保日志目录存在
                        string logDirectory = Path.GetDirectoryName(LogFilePath);
                        if (!Directory.Exists(logDirectory))
                        {
                            Directory.CreateDirectory(logDirectory);
                        }
                        
                        File.AppendAllText(LogFilePath, logEntry, Encoding.UTF8);
                    }
                }
            }
            catch (Exception ex)
            {
                // 如果写入日志失败，至少尝试输出到控制台
                Console.WriteLine("写入警告日志失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 应用程序退出事件处理程序
        /// 清理资源和事件监听
        /// </summary>
        /// <param name="e">退出事件参数</param>
        protected override void OnExit(ExitEventArgs e)
        {
            // 取消注册系统主题变化事件
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            
            base.OnExit(e);
        }
    }
}
