using AdonisUI.Controls;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using WinForms = System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Interop;
using HandyControl.Controls;

namespace DMM_Hide_Launcher.Others.Tools
{
    /// <summary>
    /// 调整游戏窗口的交互逻辑
    /// </summary>
    public partial class GameWindowResizer : AdonisWindow
    {
        // 窗口信息类
        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; }
            public string ProcessName { get; set; }
            public int MatchScore { get; set; } = 0; // 匹配分数，用于排序

            public override string ToString()
            {
                return Title;
            }
        }
        
        /// <summary>
        /// 手动调整位置按钮点击事件
        /// </summary>
        private void ManualAdjustButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WindowComboBox.SelectedItem is not WindowInfo selectedWindow)
                {
                    HandyControl.Controls.MessageBox.Info("请先选择一个游戏窗口", "提示");
                    return;
                }

                WindowMover mover = new WindowMover(selectedWindow);
                mover.ShowDialog();
            }
            catch (Exception ex)
            {
                App.LogError("手动调整位置失败: " + ex.Message);
                HandyControl.Controls.MessageBox.Error("手动调整位置失败: " + ex.Message, "错误");
            }
        }
        


        #region 窗口移动功能相关代码
        /// <summary>
        /// 全屏半透明移动窗口类
        /// </summary>
        private class WindowMover : System.Windows.Window
        {
            private readonly WindowInfo _targetWindow;
            private DispatcherTimer _debounceTimer;
            private System.Windows.Point _lastMousePos;
            private bool _debounceActive = false;
            
            private int _windowWidth = 0;
            private int _windowHeight = 0;
            
            private const int DEBOUNCE_INTERVAL_MS = 50;

            public WindowMover(WindowInfo targetWindow)
            {
                _targetWindow = targetWindow;
                
                _debounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(DEBOUNCE_INTERVAL_MS)
                };
                _debounceTimer.Tick += OnDebounceTimerTick;
                
                if (GetWindowRect(_targetWindow.Handle, out RECT rect))
                {
                    _windowWidth = rect.Right - rect.Left;
                    _windowHeight = rect.Bottom - rect.Top;
                }
                
                WindowStyle = WindowStyle.None;
                WindowStartupLocation = WindowStartupLocation.Manual;
                AllowsTransparency = true;
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(10, 179, 255, 92));
                
                Width = SystemParameters.PrimaryScreenWidth;
                Height = SystemParameters.PrimaryScreenHeight;
                Left = 0;
                Top = 0;
                
                Title = "窗口移动助手 - 按任意键退出";
                
                var textBlock = new TextBlock
                {
                    Text = "移动鼠标以定位窗口，点击任意键退出",
                    Foreground = System.Windows.Media.Brushes.White,
                    FontSize = 20,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 50)
                };
                var mainGrid = new Grid();
                mainGrid.Children.Add(textBlock);
                Content = mainGrid;
                
                MouseMove += OnMouseMove;
                KeyDown += OnKeyDown;
                MouseDown += OnMouseDown;
                
                Loaded += (s, e) => { 
                    var hwnd = new WindowInteropHelper(this).Handle;
                    SetWindowPos(hwnd, (IntPtr)(-1), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
                    Mouse.Capture(this);
                };
                
                Closing += (s, e) => { 
                    Mouse.Capture(null);
                };
            }
            
            private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
            {
                _lastMousePos = PointToScreen(e.GetPosition(this));
                
                if (!_debounceActive)
                {
                    MoveWindowToPosition(_lastMousePos);
                }
                
                _debounceTimer.Stop();
                _debounceTimer.Start();
                _debounceActive = true;
            }
            
            private void OnDebounceTimerTick(object sender, EventArgs e)
            {
                _debounceTimer.Stop();
                _debounceActive = false;
                MoveWindowToPosition(_lastMousePos);
            }
            
            private void MoveWindowToPosition(System.Windows.Point mousePos)
            {
                int newX = (int)(mousePos.X - _windowWidth / 2);
                int newY = (int)(mousePos.Y - _windowHeight / 2);
                
                WinForms.Screen currentScreen = WinForms.Screen.FromPoint(new System.Drawing.Point((int)mousePos.X, (int)mousePos.Y));
                
                int screenLeft = currentScreen.Bounds.Left;
                int screenTop = currentScreen.Bounds.Top;
                int screenRight = currentScreen.Bounds.Right - _windowWidth;
                int screenBottom = currentScreen.Bounds.Bottom - _windowHeight;
                
                newX = Math.Max(screenLeft, Math.Min(newX, screenRight));
                newY = Math.Max(screenTop, Math.Min(newY, screenBottom));
                
                ShowWindow(_targetWindow.Handle, SW_RESTORE);
                SetWindowPos(_targetWindow.Handle, IntPtr.Zero, newX, newY, _windowWidth, _windowHeight, SWP_NOZORDER | SWP_SHOWWINDOW);
            }
            
            private void OnMouseDown(object sender, MouseButtonEventArgs e)
            {
                Close();
            }
            
            private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
            {
                Close();
            }
        }
        #endregion
        


        // Windows API 相关声明
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, System.Text.StringBuilder lpExeName, ref uint lpdwSize);
        
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        // 窗口枚举回调委托
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // 窗口位置和大小结构体
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        // SetWindowPos 常量
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020; // 强制重新应用窗口样式

        // ShowWindow 常量
        private const int SW_MAXIMIZE = 3;
        private const int SW_RESTORE = 9;

        // Get/SetWindowLong 常量
        private const int GWL_STYLE = -16;
        private const int WS_BORDER = 0x00800000;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_OVERLAPPED = 0x00000000;
        private const int WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME;

        // 长按移动窗口相关字段
        // 移除长按移动窗口功能的相关变量

        // DllImport for Get/SetWindowLong
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public GameWindowResizer()
        {
            try
            {
                InitializeComponent();
                
                // 窗口加载完成后再执行操作，确保UI元素完全初始化
                this.Loaded += (sender, e) =>
                {
                    // 延迟执行，确保窗口完全加载
                    System.Windows.Threading.DispatcherTimer timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    timer.Tick += (s, args) =>
                    {
                        timer.Stop();
                        try
                        {
                            RefreshWindowList();
                            AutoDetectGameWindow();
                            App.Log("窗口初始化完成，已刷新窗口列表并自动检测游戏窗口");
                            Growl.Success("窗口初始化完成");
                        }
                        catch (Exception ex)
                        {
                            App.LogError("加载窗口时初始化操作失败：" + ex.Message, ex);

                        }
                    };
                    timer.Start();
                };
            }
            catch (Exception ex)
            {
                App.LogError($"GameWindowResizer构造函数异常: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 刷新窗口列表
        /// </summary>
        private void RefreshWindowList()
        {
            try
            {
                List<WindowInfo> gameWindows = new List<WindowInfo>();

                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        System.Text.StringBuilder titleBuilder = new System.Text.StringBuilder(256);
                        GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                        string title = titleBuilder.ToString();

                        if (!string.IsNullOrEmpty(title))
                        {
                            WindowInfo window = new WindowInfo { Handle = hWnd, Title = title };
                            window.ProcessName = GetProcessNameFromWindow(hWnd);
                            window.MatchScore = GetWindowMatchScore(window);
                            
                            if (window.MatchScore > 0)
                            {
                                gameWindows.Add(window);
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                // 按匹配分数排序
                gameWindows.Sort((a, b) => 
                {
                    int scoreComparison = b.MatchScore.CompareTo(a.MatchScore);
                    return scoreComparison != 0 ? scoreComparison : a.Title.CompareTo(b.Title);
                });

                WindowComboBox.Items.Clear();
                
                foreach (var window in gameWindows)
                {
                    WindowComboBox.Items.Add(window);
                }
        
                UpdateComboBoxVisibility();

                if (gameWindows.Count > 0)
                {
                    WindowComboBox.SelectedItem = gameWindows[0];
                }
                else
                {

                }
                
                App.Log($"窗口列表刷新完成，共找到 {gameWindows.Count} 个游戏窗口");
                Growl.Info($"窗口列表刷新完成，共找到 {gameWindows.Count} 个游戏窗口");
            }
            catch (Exception ex)
            {
                App.LogError("刷新窗口列表时出错：" + ex.Message, ex);

                WindowComboBox.Visibility = Visibility.Visible;
            }
        }
        
        /// <summary>
        /// 更新ComboBox的可见性
        /// </summary>
        private void UpdateComboBoxVisibility()
        {
            if (WindowComboBox != null)
            {
                WindowComboBox.Visibility = WindowComboBox.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 获取窗口的进程名
        /// </summary>
        private string GetProcessNameFromWindow(IntPtr hWnd)
        {
            try
            {
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);
                
                IntPtr processHandle = OpenProcess(0x0400 | 0x0010, false, processId);
                if (processHandle == IntPtr.Zero)
                    return string.Empty;
                
                try
                {
                    uint capacity = 256;
                    System.Text.StringBuilder path = new System.Text.StringBuilder((int)capacity);
                    
                    if (QueryFullProcessImageName(processHandle, 0, path, ref capacity))
                    {
                        return System.IO.Path.GetFileNameWithoutExtension(path.ToString());
                    }
                }
                finally
                {
                    CloseHandle(processHandle);
                }
            }
            catch (Exception ex)
            {
                App.LogError("获取进程名时出错：" + ex.Message, ex);
            }
            
            return string.Empty;
        }
        
        /// <summary>
        /// 获取窗口匹配分数
        /// </summary>
        private int GetWindowMatchScore(WindowInfo window)
        {
            int score = 0;
            string title = window.Title.ToLower();
            string processName = window.ProcessName?.ToLower() ?? string.Empty;
            
            if (window.Title == "逃跑吧！少年")
                score += 100;
            if (processName == "dmmdzz")
                score += 80;
            if (title.Contains("逃跑吧！少年"))
                score += 50;
            if (title.Contains("dmmdzz"))
                score += 50;
            if (processName.Contains("dmmdzz"))
                score += 30;
            
            return score;
        }
        
        /// <summary>
        /// 自动检测并选择游戏窗口
        /// </summary>
        public void AutoDetectGameWindow()
        {
            try
            {
                if (WindowComboBox.Items.Count > 0)
                {
                    WindowInfo selectedWindow = WindowComboBox.Items[0] as WindowInfo;
                    WindowComboBox.SelectedItem = selectedWindow;
                    
                    GetCurrentWindowInfo(false);
                    UpdateComboBoxVisibility();
                    App.Log("已自动选择并获取游戏窗口信息");
                    Growl.Success("已自动选择游戏窗口");

                }
                else
                {
                    UpdateComboBoxVisibility();
                }
            }
            catch (Exception ex)
            {
                App.LogError("自动检测游戏窗口时出错：" + ex.Message, ex);

                WindowComboBox.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 获取当前选中窗口的信息
        /// </summary>
        /// <param name="showNotification">是否显示通知（默认为true）</param>
        private void GetCurrentWindowInfo(bool showNotification = true)
        {
            try
            {
                if (WindowComboBox.SelectedItem is WindowInfo selectedWindow)
                {
                    if (GetWindowRect(selectedWindow.Handle, out RECT rect))
                    {
                        // 获取窗口样式
                        int windowStyle = GetWindowLong(selectedWindow.Handle, GWL_STYLE);
                        
                        // 获取窗口激活状态
                        bool isActive = GetForegroundWindow() == selectedWindow.Handle;
                        
                        // 获取窗口状态
                        bool isMaximized = IsZoomed(selectedWindow.Handle);
                        bool isMinimized = IsIconic(selectedWindow.Handle);
                        
                        // 构建状态信息
                        string statusInfo = $"窗口信息：位置({rect.Left},{rect.Top})，尺寸({rect.Width}x{rect.Height})，";
                        statusInfo += isActive ? "激活，" : "未激活，";
                        statusInfo += isMaximized ? "最大化，" : isMinimized ? "最小化，" : "正常，";
                        statusInfo += WindowStyleManager.IsWindowResizable(selectedWindow.Handle) ? "可调整大小" : "不可调整大小";
                        
                        // 显示通知
                        if (showNotification)
                        {

                        }
                        
                        // 更新UI元素状态
                        SyncWindowStatusToUI(selectedWindow.Handle, rect, windowStyle, isActive);
                        App.Log($"获取窗口信息完成：位置({rect.Left},{rect.Top})，尺寸({rect.Width}x{rect.Height})");
                        Growl.Info($"已获取窗口信息：{rect.Width}x{rect.Height}");
                    }
                    else
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        App.LogError($"无法获取窗口信息，Win32错误码：{errorCode}");

                    }
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                App.LogError("获取窗口信息时出错：" + ex.Message, ex);

            }
        }
        
        /// <summary>
        /// 将窗口状态同步到UI元素
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="rect">窗口矩形信息</param>
        /// <param name="windowStyle">窗口样式</param>
        /// <param name="isActive">是否激活</param>
        private void SyncWindowStatusToUI(IntPtr hWnd, RECT rect, int windowStyle, bool isActive)
        {
            // 检查窗口是否有边框样式
            bool hasBorder = (windowStyle & (WS_BORDER | WS_CAPTION | WS_SYSMENU)) != 0;
            bool hasThickFrame = (windowStyle & WS_THICKFRAME) != 0;
            
            // 确定窗口模式并更新ComboBox
            string windowMode = "正常窗口";
            if (!hasBorder && !hasThickFrame)
            {
                // 检查是否为全屏尺寸
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
                
                if (rect.Width == screenWidth && rect.Height == screenHeight && rect.Left == 0 && rect.Top == 0)
                {
                    windowMode = "无边框全屏";
                }
                else
                {
                    windowMode = "无边框窗口";
                }
            }
            
            // 更新窗口模式ComboBox
            foreach (ComboBoxItem item in WindowModeComboBox.Items)
            {
                if (item.Content.ToString() == windowMode)
                {
                    WindowModeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            // 尝试匹配当前分辨率到预设ComboBox
            string currentResolution = $"{rect.Width}x{rect.Height}";
            foreach (ComboBoxItem item in PresetComboBox.Items)
            {
                if (item.Content.ToString() == currentResolution)
                {
                    PresetComboBox.SelectedItem = item;
                    break;
                }
            }
        }
        
        // 添加所需的Windows API声明
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);



        /// <summary>
        /// 快速定位窗口
        /// </summary>
        private void PositionWindow(string position)
        {
            try
            {
                if (WindowComboBox.SelectedItem is WindowInfo selectedWindow)
                {
                    GetWindowRect(selectedWindow.Handle, out RECT rect);
                    int width = rect.Width;
                    int height = rect.Height;
                    
                    (width, height) = GetSelectedPresetResolution(width, height);
                    
                    int x = 0, y = 0;
                    
                    int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                    int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
                    
                    switch (position)
                    {
                        case "Center":
                            x = (screenWidth - width) / 2;
                            y = (screenHeight - height) / 2;
                            break;
                        case "TopLeft":
                            x = 0;
                            y = 0;
                            break;
                        case "TopRight":
                            x = screenWidth - width;
                            y = 0;
                            break;
                        case "BottomLeft":
                            x = 0;
                            y = screenHeight - height;
                            break;
                        case "BottomRight":
                            x = screenWidth - width;
                            y = screenHeight - height;
                            break;
                    }
                    
                    ShowWindow(selectedWindow.Handle, SW_RESTORE);
                    SetWindowPos(selectedWindow.Handle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_SHOWWINDOW);
                    App.Log($"窗口定位完成：{position}");
                    Growl.Success($"已定位窗口到{position}");
                    

                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                App.LogError("定位窗口时出错：" + ex.Message, ex);

            }
        }

        /// <summary>
        /// 应用预设分辨率
        /// </summary>
        private void ApplyPresetResolution()
        {
            try
            {
                if (WindowComboBox.SelectedItem is WindowInfo selectedWindow && PresetComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    string preset = selectedItem.Content.ToString();
                    
                    string[] parts = preset.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int width) && int.TryParse(parts[1], out int height))
                    {
                        GetWindowRect(selectedWindow.Handle, out RECT rect);
                        
                        ShowWindow(selectedWindow.Handle, SW_RESTORE);
                        
                        bool success = SetWindowPos(selectedWindow.Handle, IntPtr.Zero, rect.Left, rect.Top, width, height, SWP_NOZORDER | SWP_SHOWWINDOW);
                        
                        if (success)
                        {
                            App.Log($"应用预设分辨率完成：{preset}");
                            Growl.Success($"已应用预设分辨率：{preset}");
                        }
                        else
                        {

                        }
                    }
                }
                else if (WindowComboBox.SelectedItem == null)
                {

                }
            }
            catch (Exception ex)
            {
                App.LogError("应用预设分辨率时出错：" + ex.Message, ex);

            }
        }

        /// <summary>
        /// 获取选中的预设分辨率
        /// </summary>
        /// <param name="defaultWidth">默认宽度</param>
        /// <param name="defaultHeight">默认高度</param>
        /// <returns>预设分辨率的宽度和高度</returns>
        private (int Width, int Height) GetSelectedPresetResolution(int defaultWidth = 0, int defaultHeight = 0)
        {
            int width = defaultWidth;
            int height = defaultHeight;
            
            if (PresetComboBox.SelectedItem is ComboBoxItem selectedPreset)
            {
                string preset = selectedPreset.Content.ToString();
                string[] parts = preset.Split('x');
                if (parts.Length == 2 && int.TryParse(parts[0], out int presetWidth) && int.TryParse(parts[1], out int presetHeight))
                {
                    width = presetWidth;
                    height = presetHeight;
                }
            }
            
            return (width, height);
        }
        






        /// <summary>
        /// 窗口激活时调用
        /// 设置当前窗口为Growl通知的父容器，实现只在激活窗口显示通知的功能
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // 设置Growl通知的父容器为当前窗口的GrowlPanel，使通知在当前窗口显示
            Growl.SetGrowlParent(GrowlPanel_GameWindowResizer, true);
        }

        /// <summary>
        /// 窗口失去焦点时调用
        /// 取消当前窗口作为Growl通知的父容器，实现只在激活窗口显示通知的功能
        /// </summary>
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            // 取消设置Growl通知的父容器，使通知不在当前非激活窗口显示
            Growl.SetGrowlParent(GrowlPanel_GameWindowResizer, false);
        }

        // 按钮点击事件处理程序
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshWindowList();
        }





        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CenterButton_Click(object sender, RoutedEventArgs e)
        {
            PositionWindow("Center");
        }

        private void TopLeftButton_Click(object sender, RoutedEventArgs e)
        {
            PositionWindow("TopLeft");
        }

        private void TopRightButton_Click(object sender, RoutedEventArgs e)
        {
            PositionWindow("TopRight");
        }

        private void BottomLeftButton_Click(object sender, RoutedEventArgs e)
        {
            PositionWindow("BottomLeft");
        }

        private void BottomRightButton_Click(object sender, RoutedEventArgs e)
        {
            PositionWindow("BottomRight");
        }


        private void ApplyPresetButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyPresetResolution();
        }
        
        /// <summary>
        /// 应用窗口可调整大小设置
        /// </summary>
        private void ApplyResizeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WindowComboBox.SelectedItem is not WindowInfo selectedWindow)
                {

                    return;
                }
                
                bool canResize = AllowResizeCheckBox.IsChecked == true;
                bool success = WindowStyleManager.SetWindowResizable(selectedWindow.Handle, canResize);
                
                if (success)
                {
                    App.Log($"应用窗口可调整大小设置完成：" + (canResize ? "允许" : "禁止"));
                    Growl.Success(canResize ? "已允许窗口拖拽调整大小" : "已禁止窗口拖拽调整大小");
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                App.LogError("应用窗口可调整大小设置时出错：" + ex.Message, ex);

            }
        }

        /// <summary>
        /// 应用显示选项（无边框全屏和无边框窗口）
        /// </summary>
        // 窗口模式ComboBox已替换了原来的CheckBox，相关事件处理方法已移除

        /// <summary>
        /// 窗口选择变化事件处理
        /// </summary>
        private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 动态控制应用按钮的启用状态
            if (ApplyDisplayButton != null)
            {
                ApplyDisplayButton.IsEnabled = WindowComboBox.SelectedItem != null;
            }
            
            // 更新可调整大小复选框状态并获取窗口信息
            if (WindowComboBox.SelectedItem is WindowInfo selectedWindow)
            {
                bool isResizable = WindowStyleManager.IsWindowResizable(selectedWindow.Handle);
                AllowResizeCheckBox.IsChecked = isResizable;
                
                // 自动获取窗口信息
                GetCurrentWindowInfo(false);
                App.Log("窗口选择已变更，自动获取并更新窗口信息");
                Growl.Info("已更新窗口信息");
            }
        }

        /// <summary>
        /// 应用显示选项（三种窗口模式：正常窗口、无边框全屏、无边框窗口）
        /// </summary>
        private void ApplyDisplayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WindowComboBox.SelectedItem is not WindowInfo selectedWindow)
                {

                    return;
                }
                
                string selectedMode = "正常窗口";
                if (WindowModeComboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    selectedMode = selectedItem.Content.ToString();
                }
                
                // 先恢复窗口状态
                ShowWindow(selectedWindow.Handle, SW_RESTORE);
                
                // 获取屏幕尺寸
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
                
                // 获取当前窗口样式
                int currentStyle = GetWindowLong(selectedWindow.Handle, GWL_STYLE);
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 0)
                {
                    App.LogError($"获取窗口样式失败，Win32错误码：{errorCode}");

                    return;
                }
                
                bool success = false;
                
                // 移除边框样式
                int borderlessStyle = currentStyle & ~(WS_BORDER | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME);
                // 恢复正常样式
                int normalStyle = currentStyle | WS_OVERLAPPEDWINDOW;
                
                if (selectedMode == "无边框全屏")
                {
                    // 设置无边框样式
                    if (SetWindowLong(selectedWindow.Handle, GWL_STYLE, borderlessStyle) == 0)
                    {
                        errorCode = Marshal.GetLastWin32Error();
                        App.LogError($"设置窗口样式失败，Win32错误码：{errorCode}");

                        return;
                    }
                    
                    // 设置为全屏尺寸
                    success = SetWindowPos(selectedWindow.Handle, IntPtr.Zero, 0, 0, screenWidth, screenHeight,
                        SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

                }
                else if (selectedMode == "无边框窗口")
                {
                    // 获取窗口当前位置和大小
                    GetWindowRect(selectedWindow.Handle, out RECT rect);
                    
                    // 设置无边框样式
                    if (SetWindowLong(selectedWindow.Handle, GWL_STYLE, borderlessStyle) == 0)
                    {
                        errorCode = Marshal.GetLastWin32Error();
                        App.LogError($"设置窗口样式失败，Win32错误码：{errorCode}");

                        return;
                    }
                    
                    // 保持当前窗口位置和大小
                    success = SetWindowPos(selectedWindow.Handle, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height,
                        SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);

                }
                else // 正常窗口
                {
                    // 恢复正常样式
                    if (SetWindowLong(selectedWindow.Handle, GWL_STYLE, normalStyle) == 0)
                    {
                        errorCode = Marshal.GetLastWin32Error();
                        App.LogError($"恢复窗口样式失败，Win32错误码：{errorCode}");

                        return;
                    }
                    
                    // 刷新窗口
                    SetWindowPos(selectedWindow.Handle, IntPtr.Zero, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED | SWP_SHOWWINDOW);
                    
                    // 获取选中的预设分辨率
                    (int defaultWidth, int defaultHeight) = GetSelectedPresetResolution(1280, 720);
                    
                    // 设置为默认大小并居中
                    int x = (screenWidth - defaultWidth) / 2;
                    int y = (screenHeight - defaultHeight) / 2;
                    
                    success = SetWindowPos(selectedWindow.Handle, IntPtr.Zero, x, y, defaultWidth, defaultHeight,
                        SWP_NOZORDER | SWP_SHOWWINDOW);

                }
                
                if (success)
                {
                    App.Log($"应用显示选项完成：{selectedMode}");
                    Growl.Success($"已应用显示选项：{selectedMode}");
                }
                else
                {
                    errorCode = Marshal.GetLastWin32Error();
                    App.LogError($"设置窗口位置失败，Win32错误码：{errorCode}");

                    // 尝试恢复原始样式
                    SetWindowLong(selectedWindow.Handle, GWL_STYLE, currentStyle);
                }
            }
            catch (Exception ex)
            {
                App.LogError("应用显示选项时出错：" + ex.Message, ex);

            }
        }       
    }
    
    /// <summary>
    /// 窗口样式管理器，用于控制窗口的可拖拽性
    /// </summary>
    public static class WindowStyleManager
    {
        // 窗口样式常量
        private const int GWL_STYLE = -16;
        private const int WS_THICKFRAME = 0x00040000; // 可调整大小的边框，与WS_SIZEBOX相同
        private const int WS_SIZEBOX = WS_THICKFRAME; // 别名，功能相同
        
        // SetWindowPos常量
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_FRAMECHANGED = 0x0020; // 强制重新绘制边框
        
        // DllImport声明
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        /// <summary>
        /// 设置窗口是否可以拖拽调整大小
        /// </summary>
        /// <param name="hWnd">目标窗口句柄</param>
        /// <param name="canResize">是否允许调整大小</param>
        /// <returns>操作是否成功</returns>
        public static bool SetWindowResizable(IntPtr hWnd, bool canResize)
        {
            try
            {
                // 获取当前窗口样式
                int currentStyle = GetWindowLong(hWnd, GWL_STYLE);
                if (currentStyle == 0)
                {
                    return false; // 获取样式失败
                }
                
                int newStyle;
                if (canResize)
                {
                    // 添加可调整大小样式
                    newStyle = currentStyle | WS_THICKFRAME;
                }
                else
                {
                    // 移除可调整大小样式
                    newStyle = currentStyle & ~WS_THICKFRAME;
                }
                
                // 如果样式没有变化，直接返回成功
                if (newStyle == currentStyle)
                {
                    return true;
                }
                
                // 设置新的窗口样式
                if (SetWindowLong(hWnd, GWL_STYLE, newStyle) == 0)
                {
                    return false; // 设置样式失败
                }
                
                // 刷新窗口，使样式生效
                return SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        /// <summary>
        /// 检查窗口是否可以拖拽调整大小
        /// </summary>
        /// <param name="hWnd">目标窗口句柄</param>
        /// <returns>是否可以调整大小</returns>
        public static bool IsWindowResizable(IntPtr hWnd)
        {
            try
            {
                int currentStyle = GetWindowLong(hWnd, GWL_STYLE);
                if (currentStyle == 0)
                {
                    return false;
                }
                
                // 检查是否包含可调整大小样式
                return (currentStyle & WS_THICKFRAME) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}