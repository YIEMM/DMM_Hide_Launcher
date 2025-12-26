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
using Serilog.Events;
using Serilog.Sinks.Async;

namespace DMM_Hide_Launcher
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// 系统主题变化时触发的事件
        /// </summary>
        public event EventHandler<bool> SystemThemeChanged;
        
        /// <summary>
        /// 触发系统主题变化事件
        /// </summary>
        /// <param name="isDarkMode">是否为暗色主题</param>
        protected void OnSystemThemeChanged(bool isDarkMode)
        {
            SystemThemeChanged?.Invoke(this, isDarkMode);
        }
    
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
    /// 日志模式标志
    /// </summary>
    public static bool IsLogMode { get; private set; } = false;

    /// <summary>
    /// 日志实例
    /// </summary>
    private static ILogger logger;

    /// <summary>
    /// 导入Windows API函数，用于分配控制台
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    /// <summary>
    /// 初始化日志系统
    /// </summary>
    private static void InitializeLogging()
    {
        // 只有在日志模式下才初始化Serilog
        if (IsLogMode)
        {
            // 分配一个独立的控制台窗口
            AllocConsole();

            // 创建日志目录
            string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            Directory.CreateDirectory(logDirectory);

            // 配置Serilog
            var logConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .WriteTo.Async(wt => wt.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss.ffff} {Level:u3}] [{ClassName}.{MethodName}] {Message:lj}{NewLine}{Exception}",
                    theme: Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme.Code))
                .WriteTo.Async(wt => wt.File(
                    Path.Combine(logDirectory, "HDL_" + DateTime.Now.ToString("yyMMdd_HHmmss") + ".log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.ffff}] [{Level:u3}] [{ClassName}.{MethodName}]\n{Message:lj}{NewLine}{Exception}"));

            // 创建日志实例
            logger = logConfig.CreateLogger();
            Serilog.Log.Logger = logger;
        }
    }

    /// <summary>
    /// 记录信息日志
    /// </summary>
    /// <param name="message">日志消息</param>
    public static void Log(string message)
    {
        // 只有在日志模式下才记录日志
        if (IsLogMode)
        {
            // 获取调用者信息
            var callerInfo = GetCallerInfo();
            logger?.ForContext("ClassName", callerInfo.ClassName)
                  ?.ForContext("MethodName", callerInfo.MethodName)
                  ?.Information("{Message}", message);
        }
    }
    
    /// <summary>
    /// 获取调用者的类名和方法名
    /// </summary>
    /// <returns>包含类名和方法名的元组</returns>
    private static (string ClassName, string MethodName) GetCallerInfo()
    {
        try
        {
            // 获取堆栈帧，跳过当前方法和Log方法
            var stackFrame = new StackFrame(2, false);
            var method = stackFrame.GetMethod();
            
            if (method != null)
            {
                string className = method.DeclaringType?.Name ?? "UnknownClass";
                string methodName = method.Name;
                return (className, methodName);
            }
        }
        catch (Exception)
        {
            // 忽略异常，返回默认值
        }
        
        return ("UnknownClass", "UnknownMethod");
    }

    /// <summary>
    /// 记录错误日志
    /// </summary>
    /// <param name="message">日志消息</param>
    /// <param name="ex">异常对象</param>
    public static void LogError(string message, Exception ex = null)
    {
        // 只有在日志模式下才记录日志
        if (IsLogMode)
        {
            // 获取调用者信息
            var callerInfo = GetCallerInfo();
            var contextualLogger = logger?.ForContext("ClassName", callerInfo.ClassName)
                                         ?.ForContext("MethodName", callerInfo.MethodName);
            
            if (ex != null)
            {
                contextualLogger?.Error(ex, "{Message}", message);
            }
            else
            {
                contextualLogger?.Error("{Message}", message);
            }
        }
    }

    /// <summary>
    /// 记录警告日志
    /// </summary>
    /// <param name="message">日志消息</param>
    public static void LogWarning(string message)
    {
        // 只有在日志模式下才记录日志
        if (IsLogMode)
        {
            // 获取调用者信息
            var callerInfo = GetCallerInfo();
            logger?.ForContext("ClassName", callerInfo.ClassName)
                  ?.ForContext("MethodName", callerInfo.MethodName)
                  ?.Warning("{Message}", message);
        }
    }

        /// <summary>
        /// 应用程序启动事件处理程序
        /// 初始化系统主题、处理命令行参数并启动主窗口
        /// </summary>
        /// <param name="e">启动事件参数</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 初始化系统主题
            InitializeSystemTheme();
            
            // 注册系统主题变化事件监听
            SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            
            // 处理命令行参数
            ParseCommandLineArgs(e.Args);
            
            // 初始化日志系统
            InitializeLogging();
            
            // 设置异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
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
                SetAppTheme(isDarkMode);
            }
            catch (Exception)
            {
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
            catch (Exception)
            {
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
            }
            catch (Exception)
            {
                // 忽略主题设置错误
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
                SetAppTheme(isDarkMode);
                
                // 触发系统主题变化事件，通知所有订阅者
                OnSystemThemeChanged(isDarkMode);
            }
        }
        

        
        /// <summary>
        /// 解析命令行参数
        /// </summary>
        private void ParseCommandLineArgs(string[] args)
        {
            // 处理命令行参数
            foreach (string arg in args)
            {
                if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase))
                {
                    IsDebugMode = true;
                }
                else if (arg.Equals("--log", StringComparison.OrdinalIgnoreCase))
                {
                    IsLogMode = true;
                }
            }
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // 异常处理逻辑
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
