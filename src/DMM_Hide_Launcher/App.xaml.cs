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
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Json;

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
    /// 导入Windows API函数，用于设置控制台文本颜色
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleTextAttribute(IntPtr hConsoleOutput, ushort wAttributes);
    
    /// <summary>
    /// 导入Windows API函数，用于获取控制台文本颜色
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleTextAttribute(IntPtr hConsoleOutput, out ushort wAttributes);
    
    /// <summary>
    /// 导入Windows API函数，用于获取标准输出句柄
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int nStdHandle);
    
    /// <summary>
    /// 导入Windows API函数，用于设置控制台标题
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleTitle(string lpConsoleTitle);
    
    // 控制台标准句柄常量
    private const int STD_OUTPUT_HANDLE = -11;
    
    // 控制台颜色常量
    private const ushort FOREGROUND_BLUE = 0x0001;
    private const ushort FOREGROUND_GREEN = 0x0002;
    private const ushort FOREGROUND_RED = 0x0004;
    private const ushort FOREGROUND_INTENSITY = 0x0008;
    private const ushort BACKGROUND_BLUE = 0x0010;
    private const ushort BACKGROUND_GREEN = 0x0020;
    private const ushort BACKGROUND_RED = 0x0040;
    private const ushort BACKGROUND_INTENSITY = 0x0080;
    
    // 预设的控制台颜色方案
    private const ushort COLOR_NORMAL = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;
    private const ushort COLOR_INFO = FOREGROUND_BLUE | FOREGROUND_INTENSITY;
    private const ushort COLOR_WARNING = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY;
    private const ushort COLOR_ERROR = FOREGROUND_RED | FOREGROUND_INTENSITY;
    private const ushort COLOR_DEBUG = FOREGROUND_GREEN | FOREGROUND_INTENSITY;
    private const ushort COLOR_SUCCESS = FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY;
    
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
    /// Serilog日志记录器实例
    /// </summary>
    private static ILogger logger;
    
    /// <summary>
    /// 获取Serilog日志记录器实例
    /// </summary>
    public static ILogger Logger
    {
        get { return logger; }
    }

        /// <summary>
        /// 应用程序启动事件处理程序
        /// 初始化日志系统、处理命令行参数并启动主窗口
        /// </summary>
        /// <param name="e">启动事件参数</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 在应用程序启动的最早阶段尝试附加到父进程控制台，无论是否需要日志模式
            // 这确保了所有的输出都能正确地显示在父进程控制台中
            bool attachResult = AttachConsole(-1);
            
            // 初始化系统主题
            InitializeSystemTheme();
            
            // 注册系统主题变化事件监听
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            
            // 先处理命令行参数，设置日志模式标志
            Console.WriteLine("[DEBUG] 正在处理命令行参数...");
            ParseCommandLineArgs(e.Args);
            Console.WriteLine($"[DEBUG] 命令行参数处理完成 - 调试模式: {IsDebugMode}, 日志模式: {IsLogEnabled}");
            
            // 初始化日志文件路径
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Console.WriteLine($"[DEBUG] 日志目录: {logDirectory}");
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            LogFilePath = Path.Combine(logDirectory, $"DMM_Hide_Launcher_{timestamp}.log");
            Console.WriteLine($"[DEBUG] 日志文件路径: {LogFilePath}");
            
            // 根据日志模式和控制台附加状态决定是否创建新控制台
            if (IsLogEnabled && GetConsoleWindow() == IntPtr.Zero)
            {
                Console.WriteLine("[DEBUG] 日志模式已启用，但未附加到控制台，准备创建新控制台...");
                // 强制附加控制台以确保日志可见
                ForceAttachConsole();
            }
            
            // 初始化Serilog日志记录器
            InitializeLogger();
            
            // 输出启动信息到控制台
            if (IsLogEnabled || IsDebugMode)
            {
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
        /// 设置控制台文本颜色
        /// </summary>
        /// <param name="colorCode">颜色代码</param>
        private static void SetConsoleColor(ushort colorCode)
        {
            try
            {
                IntPtr consoleHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (consoleHandle != IntPtr.Zero && consoleHandle.ToInt32() != -1)
                {
                    SetConsoleTextAttribute(consoleHandle, colorCode);
                }
            }
            catch (Exception ex)
            {
                // 忽略颜色设置失败的异常，不影响主要功能
                Console.WriteLine("设置控制台颜色失败: " + ex.Message);
            }
        }
        
        /// <summary>
        /// 恢复控制台默认颜色
        /// </summary>
        private static void ResetConsoleColor()
        {
            SetConsoleColor(COLOR_NORMAL);
        }
        
        /// <summary>
        /// 强制附加控制台
        /// </summary>
        private void ForceAttachConsole()
        {
            try
            {
                App.Log("开始附加控制台");
                
                // 在OnStartup中已经尝试过AttachConsole(-1)，这里直接检查是否需要分配新控制台
                if (GetConsoleWindow() == IntPtr.Zero)
                {
                    App.Log("当前无控制台可用，尝试分配新控制台");
                    Console.WriteLine("[DEBUG] 当前无控制台可用，尝试分配新控制台...");
                    
                    bool allocResult = AllocConsole();
                    App.Log($"AllocConsole 结果: {allocResult}");
                    Console.WriteLine($"[DEBUG] AllocConsole 结果: {allocResult}");
                    Console.Out.Flush();
                }
                
                // 设置控制台标题
                string consoleTitle = "DMM_Hide_Launcher - 控制台输出";
                SetConsoleTitle(consoleTitle);
                App.Log($"控制台标题已设置为: {consoleTitle}");
                
                // 尝试获取标准输出流，检查控制台连接状态
                var stdout = Console.Out;
                try
                {
                    Console.WriteLine("[DEBUG] 测试控制台写入能力...");
                    Console.Out.Flush();
                }
                catch (Exception ex)
                {
                    App.Log("控制台写入测试失败: " + ex.Message);
                    Console.WriteLine("[DEBUG] 控制台写入测试失败: " + ex.Message);
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
                
                // 根据系统主题设置控制台颜色
                bool isDarkMode = IsSystemDarkMode();
                try
                {
                    IntPtr consoleHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                    if (consoleHandle != IntPtr.Zero && consoleHandle.ToInt32() != -1)
                    {
                        if (isDarkMode)
                        {
                            // 暗色主题：深色背景，浅色文字
                            ushort darkModeAttributes = (ushort)(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
                            SetConsoleTextAttribute(consoleHandle, darkModeAttributes);
                            App.Log("控制台已设置为深色主题样式");
                        }
                        else
                        {
                            // 亮色主题：浅色背景，深色文字
                            ushort lightModeAttributes = (ushort)(FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
                            SetConsoleTextAttribute(consoleHandle, lightModeAttributes);
                            App.Log("控制台已设置为亮色主题样式");
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Log($"设置控制台主题样式失败: {ex.Message}");
                    Console.WriteLine($"[DEBUG] 设置控制台主题样式失败: {ex.Message}");
                }
                
                // 确保标准输出流被重新初始化
                try
                {
                    // 尝试重新打开标准输出流，显式设置UTF8编码以支持中文
                    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true });
                    
                    // 强制刷新以确保所有未写入的数据都被处理
                    Console.Out.Flush();
                    
                    // 禁止用户向控制台输入
                    Console.SetIn(TextReader.Null);
                    
                    // 验证控制台输入是否被成功禁止
                    try
                    {
                        Console.WriteLine("[DEBUG] 尝试从控制台读取输入，验证是否被禁止...");
                        Console.Out.Flush();
                        
                        // 尝试读取一行输入
                        string input = Console.ReadLine();
                        
                        // 如果输入为null，说明输入被成功禁止
                        if (input == null)
                        {
                            App.Log("控制台输入验证成功：无法读取到任何输入，输入已被成功禁止");
                            Console.WriteLine("[DEBUG] 控制台输入验证成功：无法读取到任何输入，输入已被成功禁止");
                        }
                        else
                        {
                            App.Log("警告：控制台输入验证失败，成功读取到了输入内容");
                            Console.WriteLine("[DEBUG] 警告：控制台输入验证失败，成功读取到了输入内容");
                        }
                        Console.Out.Flush();
                    }
                    catch (Exception readEx)
                    {
                        App.Log("控制台输入验证结果：尝试读取输入时发生异常（预期行为）：" + readEx.Message);
                        Console.WriteLine("[DEBUG] 控制台输入验证结果：尝试读取输入时发生异常（预期行为）：" + readEx.Message);
                        Console.Out.Flush();
                    }
                    
                    App.Log("已成功重新初始化标准输出流并设置UTF8编码，同时禁止了控制台输入");
                    Console.WriteLine("[DEBUG] 已成功重新初始化标准输出流并设置UTF8编码，同时禁止了控制台输入");
                    Console.Out.Flush();
                }
                catch (Exception ex)
                {
                    App.Log($"重新初始化标准输出流失败: {ex.Message}");
                    Console.WriteLine($"[DEBUG] 重新初始化标准输出流失败: {ex.Message}");
                }
                
                // 测试中文显示
                string chineseTest = "中文测试显示 - Console.WriteLine";
                Console.WriteLine(chineseTest);
                Console.Out.Flush(); // 确保中文测试输出立即显示
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
                Console.Out.Flush(); // 最后一次强制刷新确保所有输出都被处理
            }
            catch (Exception ex)
            {
                App.Log($"控制台附加过程中出现异常: {ex.Message}");
                Console.WriteLine($"[DEBUG] 控制台附加过程中出现异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 导入Windows API函数，用于附加到父进程控制台
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AttachConsole(int dwProcessId);
        
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
        /// 初始化Serilog日志记录器
        /// </summary>
        private void InitializeLogger()
        {
            try
            {
                // 配置Serilog
                var loggerConfiguration = new LoggerConfiguration()
                    .MinimumLevel.Information() // 默认最低日志级别为Information
                    .Enrich.FromLogContext()
                    .Enrich.WithProcessId()
                    .Enrich.WithThreadId()
                    .Enrich.WithEnvironmentUserName();

                // 根据是否为调试模式设置日志级别
                if (IsDebugMode)
                {
                    loggerConfiguration.MinimumLevel.Debug();
                }

                // 配置控制台输出
                if (IsLogEnabled || IsDebugMode)
                {
                    loggerConfiguration.WriteTo.Console(
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [进程ID:{ProcessId}] [线程ID:{ThreadId}] [{SourceContext}]: {Message:lj}{NewLine}{Exception}",
                        theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code);
                }

                // 配置文件输出
                if (IsLogEnabled)
                {
                    // 确保日志目录存在
                    string logDirectory = Path.GetDirectoryName(LogFilePath);
                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }

                    loggerConfiguration.WriteTo.File(
                        path: LogFilePath,
                        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [进程ID:{ProcessId}] [线程ID:{ThreadId}] [{SourceContext}]: {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Infinite,
                        encoding: Encoding.UTF8,
                        retainedFileCountLimit: 10,
                        fileSizeLimitBytes: 10 * 1024 * 1024); // 10MB限制
                }

                // 创建日志记录器实例
                logger = loggerConfiguration.CreateLogger();
                
                // 记录初始化信息
                if (logger != null)
                {
                    logger.Information("Serilog日志记录器初始化完成");
                }
            }
            catch (Exception ex)
            {
                // 如果Serilog初始化失败，尝试通过控制台输出错误信息
                try
                {
                    Console.WriteLine($"Serilog初始化失败: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
                catch { }
            }
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

                // 获取调用方信息
                string callerInfo = "App";
                if (IsDebugMode || IsLogEnabled)
                {
                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(1, false);
                    if (stackTrace.FrameCount > 0)
                    {
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                        System.Reflection.MethodBase method = frame.GetMethod();
                        callerInfo = $"{method.DeclaringType?.FullName}.{method.Name}";
                    }
                }

                // 使用Serilog记录日志
                if (logger != null)
                {
                    using (LogContext.PushProperty("SourceContext", callerInfo))
                    {
                        logger.Information(message);
                    }
                }
                // 降级方案：如果Serilog不可用，尝试使用控制台输出
                else if (IsLogEnabled || IsDebugMode)
                {
                    try
                    {
                        SetConsoleColor(COLOR_INFO);
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] INFO [{callerInfo}]: {message}");
                        ResetConsoleColor();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // 日志记录失败的降级处理
                try
                {
                    SetConsoleColor(COLOR_ERROR);
                    Console.WriteLine("写入日志失败: " + ex.Message);
                    ResetConsoleColor();
                }
                catch { }
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
                // 获取调用方信息
                string callerInfo = "App";
                if (IsDebugMode || IsLogEnabled)
                {
                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(1, false);
                    if (stackTrace.FrameCount > 0)
                    {
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                        System.Reflection.MethodBase method = frame.GetMethod();
                        callerInfo = $"{method.DeclaringType?.FullName}.{method.Name}";
                    }
                }

                // 使用Serilog记录错误日志
                if (logger != null)
                {
                    using (LogContext.PushProperty("SourceContext", callerInfo))
                    {
                        if (exception != null)
                        {
                            logger.Error(exception, message);
                        }
                        else
                        {
                            logger.Error(message);
                        }
                    }
                }
                // 降级方案：如果Serilog不可用，尝试使用控制台输出
                else if (IsLogEnabled || IsDebugMode)
                {
                    try
                    {
                        StringBuilder errorMessage = new StringBuilder();
                        errorMessage.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR [{callerInfo}]: {message}");
                        
                        if (exception != null)
                        {
                            errorMessage.AppendLine($"异常类型: {exception.GetType().FullName}");
                            errorMessage.AppendLine($"异常消息: {exception.Message}");
                            errorMessage.AppendLine($"堆栈跟踪: {exception.StackTrace}");
                            
                            if (exception.InnerException != null)
                            {
                                errorMessage.AppendLine($"内部异常: {exception.InnerException.Message}");
                                errorMessage.AppendLine($"内部堆栈: {exception.InnerException.StackTrace}");
                            }
                        }
                        
                        SetConsoleColor(COLOR_ERROR);
                        Console.WriteLine(errorMessage.ToString().TrimEnd());
                        ResetConsoleColor();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // 错误日志记录失败的降级处理
                try
                {
                    SetConsoleColor(COLOR_ERROR);
                    Console.WriteLine("写入错误日志失败: " + ex.Message);
                    ResetConsoleColor();
                }
                catch { }
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
                // 获取调用方信息
                string callerInfo = "App";
                if (IsDebugMode || IsLogEnabled)
                {
                    System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace(1, false);
                    if (stackTrace.FrameCount > 0)
                    {
                        System.Diagnostics.StackFrame frame = stackTrace.GetFrame(0);
                        System.Reflection.MethodBase method = frame.GetMethod();
                        callerInfo = $"{method.DeclaringType?.FullName}.{method.Name}";
                    }
                }

                // 使用Serilog记录警告日志
                if (logger != null)
                {
                    using (LogContext.PushProperty("SourceContext", callerInfo))
                    {
                        logger.Warning(message);
                    }
                }
                // 降级方案：如果Serilog不可用，尝试使用控制台输出
                else if (IsLogEnabled || IsDebugMode)
                {
                    try
                    {
                        SetConsoleColor(COLOR_WARNING);
                        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] WARNING [{callerInfo}]: {message}");
                        ResetConsoleColor();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                // 警告日志记录失败的降级处理
                try
                {
                    SetConsoleColor(COLOR_ERROR);
                    Console.WriteLine("写入警告日志失败: " + ex.Message);
                    ResetConsoleColor();
                }
                catch { }
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
            
            // 关闭并刷新Serilog日志记录器
            if (logger != null)
            {
                logger.Information("应用程序正在关闭，日志记录器正在刷新...");
                Serilog.Log.CloseAndFlush();
            }
            
            base.OnExit(e);
        }
    }
}
