using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// 窗口检测类
    /// 用于检测特定名称的窗口是否存在
    /// 支持检测的窗口名称：逃跑吧！少年、逃跑吧少年、dmmdzz
    /// </summary>
    public class CheckGameWindow_Others
    {
        /// <summary>
        /// 导入Windows API函数，用于查找窗口
        /// </summary>
        /// <param name="lpClassName">窗口类名</param>
        /// <param name="lpWindowName">窗口标题</param>
        /// <returns>找到的窗口句柄，如果未找到则返回IntPtr.Zero</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        /// <summary>
        /// 导入Windows API函数，用于枚举所有顶级窗口
        /// </summary>
        /// <param name="lpEnumFunc">回调函数，用于处理每个窗口</param>
        /// <param name="lParam">用户定义的参数，传递给回调函数</param>
        /// <returns>如果枚举成功则返回true，否则返回false</returns>
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        /// <summary>
        /// 定义窗口枚举回调函数的委托类型
        /// </summary>
        /// <param name="hWnd">当前枚举到的窗口句柄</param>
        /// <param name="lParam">用户定义的参数</param>
        /// <returns>如果继续枚举则返回true，否则返回false</returns>
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        /// <summary>
        /// 导入Windows API函数，用于获取窗口标题文本
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpString">存储窗口标题的缓冲区</param>
        /// <param name="nMaxCount">缓冲区大小</param>
        /// <returns>复制到缓冲区的字符数，如果函数失败则返回0</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// 导入Windows API函数，用于获取窗口类名
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="lpString">存储窗口类名的缓冲区</param>
        /// <param name="nMaxCount">缓冲区大小</param>
        /// <returns>复制到缓冲区的字符数，如果函数失败则返回0</returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        /// <summary>
        /// 用于持续监测的CancellationTokenSource
        /// </summary>
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 监测任务
        /// </summary>
        private Task _monitoringTask;

        /// <summary>
        /// 上次检测的窗口状态
        /// </summary>
        private bool _lastWindowState;

        /// <summary>
        /// 监测间隔（毫秒）
        /// </summary>
        private int _monitoringInterval = 1000; // 默认1秒，与实际使用一致

        /// <summary>
        /// 上次检测到的窗口标题列表
        /// 用于减少重复的窗口枚举操作
        /// </summary>
        private List<string> _lastDetectedWindows = new List<string>();

        /// <summary>
        /// 窗口状态变化事件
        /// </summary>
        public event EventHandler<WindowStateChangedEventArgs> WindowStateChanged;

        /// <summary>
        /// 获取或设置监测间隔（毫秒）
        /// </summary>
        public int MonitoringInterval
        {
            get { return _monitoringInterval; }
            set
            {
                if (value < 99) // 最小99毫秒
                {
                    _monitoringInterval = 99;
                }
                else
                {
                    _monitoringInterval = value;
                }
            }
        }

        /// <summary>
        /// 是否正在监测
        /// </summary>
        public bool IsMonitoring { get; private set; }

        /// <summary>
        /// 要检测的目标窗口名称列表
        /// </summary>
        private readonly List<string> _targetWindowNames = new List<string>
        {
            "逃跑吧！少年",
            "逃跑吧少年",
            "dmmdzz"
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        public CheckGameWindow_Others() { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="customWindowNames">自定义要检测的窗口名称列表</param>
        public CheckGameWindow_Others(List<string> customWindowNames)
        {
            if (customWindowNames != null && customWindowNames.Count > 0)
            {
                _targetWindowNames.Clear();
                _targetWindowNames.AddRange(customWindowNames);
            }
        }

        /// <summary>
        /// 检测目标窗口是否存在
        /// </summary>
        /// <returns>如果任一目标窗口存在则返回true，否则返回false</returns>
        public bool IsTargetWindowRunning()
        {
            // 方法1: 直接使用FindWindow查找特定窗口标题
            foreach (string windowName in _targetWindowNames)
            {
                IntPtr hWnd = FindWindow(null, windowName);
                if (hWnd != IntPtr.Zero)
                {
                    return true;
                }
            }

            // 方法2: 如果方法1没找到，使用EnumWindows枚举所有窗口进行更全面的查找
            return EnumAndCheckWindows();
        }

        /// <summary>
        /// 枚举所有窗口并检查是否有目标窗口
        /// 同时收集匹配的窗口标题
        /// </summary>
        /// <param name="collectWindowTitles">是否收集匹配的窗口标题</param>
        /// <param name="windowTitles">收集窗口标题的列表</param>
        /// <returns>如果找到目标窗口则返回true，否则返回false</returns>
        private bool EnumAndCheckWindows(bool collectWindowTitles = false, List<string> windowTitles = null)
        {
            bool found = false;

            // 预先分配StringBuilder以减少内存分配
            StringBuilder windowText = new StringBuilder(256);

            // 使用匿名委托作为回调函数
            EnumWindows((hWnd, lParam) =>
            {
                // 清空StringBuilder而不是创建新实例
                windowText.Clear();
                GetWindowText(hWnd, windowText, windowText.Capacity);
                string title = windowText.ToString();

                // 检查窗口标题是否包含目标窗口名称
                foreach (string targetName in _targetWindowNames)
                {
                    if (string.Equals(title, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                         
                        // 如果需要收集窗口标题
                        if (collectWindowTitles && windowTitles != null && !windowTitles.Contains(title))
                        {
                            windowTitles.Add(title);
                        }
                        
                        // 继续枚举以收集所有匹配的窗口
                        // 仅在不需要收集窗口标题时才停止枚举
                        if (!collectWindowTitles)
                        {
                            return false;
                        }
                    }
                }

                return true; // 继续枚举
            }, IntPtr.Zero);

            return found;
        }

        /// <summary>
        /// 获取所有正在运行的目标窗口标题列表
        /// </summary>
        /// <returns>包含所有找到的目标窗口标题的列表</returns>
        public List<string> GetRunningTargetWindows()
        {
            // 重用已有列表或创建新列表
            if (_lastDetectedWindows == null)
            {
                _lastDetectedWindows = new List<string>();
            }
            else
            {
                _lastDetectedWindows.Clear();
            }

            // 使用优化后的枚举方法，同时收集窗口标题
            EnumAndCheckWindows(true, _lastDetectedWindows);

            return new List<string>(_lastDetectedWindows);
        }

        /// <summary>
        /// 获取指定窗口是否可见
        /// </summary>
        /// <param name="windowName">窗口名称</param>
        /// <returns>如果窗口存在且可见则返回true，否则返回false</returns>
        public bool IsWindowVisible(string windowName)
        {
            IntPtr hWnd = FindWindow(null, windowName);
            if (hWnd == IntPtr.Zero)
            {
                return false;
            }

            return IsWindowVisible(hWnd);
        }

        /// <summary>
        /// 导入Windows API函数，用于检查窗口是否可见
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <returns>如果窗口可见则返回true，否则返回false</returns>
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// 添加新的目标窗口名称到检测列表
        /// </summary>
        /// <param name="windowName">要添加的窗口名称</param>
        public void AddTargetWindowName(string windowName)
        {
            if (!string.IsNullOrEmpty(windowName) && !_targetWindowNames.Contains(windowName))
            {
                _targetWindowNames.Add(windowName);
            }
        }

        /// <summary>
        /// 从检测列表中移除指定的目标窗口名称
        /// </summary>
        /// <param name="windowName">要移除的窗口名称</param>
        /// <returns>如果成功移除则返回true，否则返回false</returns>
        public bool RemoveTargetWindowName(string windowName)
        {
            return _targetWindowNames.Remove(windowName);
        }

        /// <summary>
        /// 获取当前的目标窗口名称列表
        /// </summary>
        /// <returns>目标窗口名称列表的副本</returns>
        public List<string> GetTargetWindowNames()
        {
            return new List<string>(_targetWindowNames);
        }

        /// <summary>
        /// 开始持续监测窗口状态
        /// </summary>
        public void StartMonitoring()
        {
            if (IsMonitoring)
            {
                return; // 已经在监测中
            }

            // 初始化取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            // 记录初始状态
            _lastWindowState = IsTargetWindowRunning();

            // 启动监测任务
            _monitoringTask = Task.Run(() => MonitorWindowsAsync(token), token);
            IsMonitoring = true;
        }

        /// <summary>
        /// 停止持续监测窗口状态
        /// </summary>
        public void StopMonitoring()
        {
            if (!IsMonitoring)
            {
                return; // 没有在监测中
            }

            // 取消任务
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // 等待任务完成
            if (_monitoringTask != null && !_monitoringTask.IsCompleted)
            {
                try
                {
                    Task.WaitAny(_monitoringTask, Task.Delay(1000));
                }
                catch { }
            }

            _monitoringTask = null;
            IsMonitoring = false;
        }

        /// <summary>
        /// 异步执行窗口监测
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        private async Task MonitorWindowsAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // 检查窗口状态
                    bool currentState = IsTargetWindowRunning();
                    
                    // 如果状态发生变化，触发事件
                    if (currentState != _lastWindowState)
                    {
                        // 如果窗口状态为运行中，重用_lastDetectedWindows缓存
                        List<string> runningWindows;
                        if (currentState)
                        {
                            // 直接使用缓存的窗口列表，避免重复枚举
                            if (_lastDetectedWindows != null && _lastDetectedWindows.Count > 0)
                            {
                                runningWindows = new List<string>(_lastDetectedWindows);
                            }
                            else
                            {
                                runningWindows = GetRunningTargetWindows();
                            }
                        }
                        else
                        {
                            runningWindows = new List<string>();
                            // 清空缓存
                            if (_lastDetectedWindows != null)
                            {
                                _lastDetectedWindows.Clear();
                            }
                        }
                        
                        OnWindowStateChanged(new WindowStateChangedEventArgs(currentState, runningWindows));
                        _lastWindowState = currentState;
                    }

                    // 等待指定的间隔时间，同时可以响应取消请求
                    await Task.Delay(_monitoringInterval, cancellationToken);
                }
            }
            catch (TaskCanceledException) 
            {
                // 任务取消是正常流程，不记录日志
            }
            catch (Exception)
            {
                // 实际应用中应该有适当的日志记录机制
                // 这里简化处理
            }
        }

        /// <summary>
        /// 触发窗口状态变化事件
        /// </summary>
        /// <param name="e">事件参数</param>
        protected virtual void OnWindowStateChanged(WindowStateChangedEventArgs e)
        {
            WindowStateChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// 窗口状态变化事件参数类
    /// </summary>
    public class WindowStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 当前窗口状态（是否存在）
        /// </summary>
        public bool IsRunning { get; }

        /// <summary>
        /// 当前正在运行的窗口标题列表
        /// </summary>
        public List<string> RunningWindowTitles { get; }

        /// <summary>
        /// 状态变化的时间戳
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isRunning">窗口是否正在运行</param>
        /// <param name="runningWindowTitles">运行中的窗口标题列表</param>
        public WindowStateChangedEventArgs(bool isRunning, List<string> runningWindowTitles)
        {
            IsRunning = isRunning;
            RunningWindowTitles = runningWindowTitles ?? new List<string>();
            Timestamp = DateTime.Now;
        }
    }
}