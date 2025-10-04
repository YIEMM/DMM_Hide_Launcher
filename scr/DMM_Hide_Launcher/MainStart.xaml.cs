using AdonisUI;
using AdonisUI.Controls;
using HandyControl.Controls;
using Newtonsoft.Json;
using DMM_Hide_Launcher.Others;
using DMM_Hide_Launcher.Others.Tools;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using System.Windows.Media;
using System.Collections.Generic;
using System.Text;

// 忽略Windows特定API的平台兼容性警告
#pragma warning disable CA1416


namespace DMM_Hide_Launcher
{
    /// <summary>
    /// 主窗口交互逻辑类
    /// 程序的主要界面，负责显示游戏启动器的主界面、管理账号、启动游戏等核心功能
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        /// <summary>
        /// 存储账号列表的可观察集合
        /// </summary>
        public ObservableCollection<Account> Accounts { get; set; }
        
        /// <summary>
        /// 游戏版本号
        /// </summary>
        public static string Game_Version;
        
        /// <summary>
        /// 用于获取版本控制XML的URL
        /// </summary>
        public static string xmlUrl = "https://update.version.brmyx.com/get_verion_control_xml_string.xml?pfID=36&appId=com.bairimeng.dmmdzz.pc.36&versionKey=";
        
        /// <summary>
        /// 游戏路径
        /// </summary>
        private string gamePath;
        
        /// <summary>
        /// 游戏路径（大写变量名版本）
        /// </summary>
        private string GamePath;
        
        /// <summary>
        /// 配置文件名
        /// </summary>
        private readonly string configFileName = "config.xml";
        
        /// <summary>
        /// HTTP客户端实例，用于发送网络请求
        /// </summary>
        private readonly HttpClient httpClient = new HttpClient();
        
        /// <summary>
        /// 加载标志
        /// </summary>
        private bool Load = true;
        
        /// <summary>
        /// 当前选中的账号
        /// </summary>
        private Account _selectedAccount;
        
        /// <summary>
        /// 账号文件路径
        /// </summary>
        private const string AccountsFilePath = "accounts.json";
        
        /// <summary>
        /// 主题状态标志，true表示暗色主题，false表示亮色主题
        /// </summary>
        private bool _isDark;

        /// <summary>
        /// 窗口检测器实例，用于持续监测游戏窗口状态
        /// </summary>
        private CheckGameWindow_Others _windowDetector;

        /// <summary>
        /// 窗口激活时调用
        /// 设置当前窗口为Growl通知的父容器，实现只在激活窗口显示通知的功能
        /// 确保通知信息只在当前可见的窗口显示，避免UI挤压和重叠问题
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // 设置Growl通知的父容器为当前窗口的GrowlPanel，使通知在当前窗口显示
            Growl.SetGrowlParent(GrowlPanel_MainStart, true);
        }
        
        /// <summary>
        /// 窗口失去焦点时调用
        /// 取消当前窗口作为Growl通知的父容器，实现只在激活窗口显示通知的功能
        /// </summary>
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            // 取消设置Growl通知的父容器，使通知不在当前非激活窗口显示
            Growl.SetGrowlParent(GrowlPanel_MainStart, false);
        }
        
        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化主题状态，与系统主题保持一致
            _isDark = (App.Current as App)?.IsSystemDarkMode() ?? false;
            
            // 设置初始图标
            ImageBrush Theme_Ico = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(_isDark ? 
                    "pack://application:,,,/DMM_Hide_Launcher;component/public/sun.ico" : 
                    "pack://application:,,,/DMM_Hide_Launcher;component/public/moon.ico", 
                    UriKind.Absolute)),
                Stretch = Stretch.UniformToFill
            };
            Panel_Theme.Background = Theme_Ico;
            
            // 记录程序启动信息
            App.Log("程序启动，主窗口初始化开始");
            
            try
            {
                LoadConfig();
                App.Log("配置文件加载成功");
                
                LoadXmlData();
                App.Log("XML数据加载成功");
                
                Accounts = new ObservableCollection<Account>();
                lstAccounts.ItemsSource = Accounts;

                // 监听集合变更，更新按钮绑定
                Accounts.CollectionChanged += (s, e) =>
                {
                    // 强制刷新ItemsControl的容器（触发按钮绑定）
                    lstAccounts.Items.Refresh();
                };
            }
            catch (Exception ex)
            {
                App.LogError("主窗口初始化失败", ex);
                // 在非调试模式下，向用户显示错误消息
                if (!App.IsDebugMode)
                {
                    MessageBox.Show("程序初始化失败，请查看日志文件获取详细信息", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                App.Log("主窗口初始化完成");
            }
        }
        
        /// <summary>
        /// 账号类
        /// 用于存储用户名和密码信息
        /// </summary>
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


        /// <summary>
        /// 加载XML数据
        /// 从网络获取游戏版本信息和更新内容
        /// </summary>
        private async void LoadXmlData()
        {
            try
            {
                await Load_Update_XML_First();
                Version_Reload();
                var extractedData = await Load_Update_XML_Second();
                string fix_data = extractedData["fixNoteContent"][0];
                string update_data = extractedData["updateNoteContent"][0];
                if (fix_data == update_data)
                {XML_Text.Text = update_data;}
                else
                {XML_Text.Text = fix_data + "\n\n" + update_data; }

                if (extractedData["patchVersion"].Count > 0 && Version_Patch != null)
                {Version_Patch.Text = extractedData["patchVersion"][0];}
            }
            catch (Exception)
            {
                if (Version_Patch != null)
                {Version_Patch.Text = "错误";}

                if (XML_Text != null)
                {XML_Text.Text = "处于无网络状态，请稍后再试或联系开发者";}
            }
        }
        
        /// <summary>
        /// 保存版本数据到文件
        /// 将游戏版本信息保存到本地JSON文件
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void SaveVersionDataToFile(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("开始保存版本信息到文件");
                Dictionary<string, List<string>> data = new Dictionary<string, List<string>>
                {
                    { "version", new List<string> { Game_Version } },
                    { "patchVersion", new List<string> { Version_Patch.Text } },
                    { "updateNoteContent", new List<string> { XML_Text.Text } },
                    { "fixNoteContent", new List<string> { XML_Text.Text } }
                };
                string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText("version_data.json", jsonData);
                App.Log("版本信息保存成功");
                Growl.Success("已保存更新信息");
            }
            catch (Exception ex)
            {
                App.LogError("保存版本信息时出错", ex);
                MessageBox.Show($"保存版本信息时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从小米应用商店获取版本信息
        /// 获取游戏的最新版本信息
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task Load_Update_XML_First()
        {
            try
            {
                App.Log("开始从小米应用商店获取版本信息");
                string version_url = "https://app.mi.com/details?id=com.bairimeng.dmmdzz.mi";
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(version_url);
                    response.EnsureSuccessStatusCode();
                    string html = await response.Content.ReadAsStringAsync();

                    string match_div = "<div class=\"float-left\">\\s*<div style=\"float: left\">\\s*版本号\\s*<\\/div>\\s*<div style=\"float:right;\">\\s*(.*?)\\s*<\\/div>\\s*<\\/div>";
                    Match match = Regex.Match(html, match_div, RegexOptions.Singleline);

                    if (match.Success)
                    {
                        Game_Version = match.Groups[1].Value.Trim();
                        App.Log($"成功获取游戏版本号: {Game_Version}");
                        Console.WriteLine(Game_Version);
                    }
                    else
                    {
                        App.Log("未找到版本号");
                        Console.WriteLine("未找到版本号");
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("获取版本信息时出错", ex);
                Console.WriteLine("发生错误: " + ex.Message);
            }
        }
        private async Task<Dictionary<string, List<string>>> Load_Update_XML_Second()
        {
            try
            {
                App.Log($"开始从更新服务器获取XML数据，URL: {xmlUrl + Game_Version}");
                HttpResponseMessage response = await httpClient.GetAsync(xmlUrl + Game_Version);
                response.EnsureSuccessStatusCode();

                string xmlContent = await response.Content.ReadAsStringAsync();
                XElement root = XElement.Parse(xmlContent);
                App.Log("成功解析XML更新数据");

                return new Dictionary<string, List<string>>
                {
                    { "version", root.Descendants("version").Select(x => x.Value).ToList() },
                    { "featureVersion", root.Descendants("featureVersion").Select(x => x.Value).ToList() },
                    { "patchVersion", root.Descendants("patchVersion").Select(x => x.Value).ToList() },
                    { "updateNoteContent", root.Descendants("updateNoteContent").Select(x => x.Value ?? "").ToList() },
                    { "fixNoteContent", root.Descendants("fixNoteContent").Select(x => x.Value ?? "").ToList() }
                };
            }
            catch (Exception ex)
            {
                App.LogError("获取XML更新数据失败", ex);
                throw new Exception($"获取更新信息失败: {ex.Message}", ex);
            }

        }
        private async void Version_Reload()
        {
            try
            {
                App.Log("开始处理版本号: " + Game_Version);
                string[] parts = Game_Version.Split('.');
                App.Log($"版本号拆分为 {parts.Length} 部分: {string.Join(", ", parts)}");

                if (parts.Length == 4)
                {
                    App.Log("版本号为4位格式，将第4位设置为0");
                    parts[3] = "0";
                    Game_Version = string.Join(".", parts);
                    App.Log("处理后的版本号: " + Game_Version);
                }
                else
                {
                    App.Log("版本号不足4位，末尾添加.0");
                    Game_Version = Game_Version+".0";
                    App.Log("处理后的版本号: " + Game_Version);
                    // MessageBox.Show(Game_Version);
                }
            }
            catch (Exception ex)
            {
                App.LogError("版本号处理失败", ex);
                MessageBox.Show($"版本号处理失败: {ex.Message}");
            }
        }

        private async void LoadConfig()
        {
            App.Log("开始加载配置文件: " + configFileName);
            
            if (File.Exists(configFileName))
            {
                App.Log("配置文件存在，开始解析");
                try
                {
                    XDocument xmlDoc = XDocument.Load(configFileName);
                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                    XElement ID_7KElement = xmlDoc.Root.Element("ID_7K");
                    XElement Key_7KElement = xmlDoc.Root.Element("Key_7K");
                    string Key7K = (string)Key_7KElement;
                    string ID7K = (string)ID_7KElement;
                    gamePath = (string)gameDataElement;
                    GamePath = (string)gameDataElement;
                    
                    App.Log("配置文件解析成功，游戏路径: " + (GamePath ?? "未设置"));
                    App.Log("配置文件解析成功，7K账号: " + (ID7K ?? "未设置"));
                    
                    User_Text.Text = ID7K;
                    game_path_text.Text = GamePath;
                    Load = false;
                    await Task.Delay(100);
                    if (IsLoaded)
                    {
                        App.Log("窗口已加载，准备加载49ID和账号信息");
                        Load49ID();
                        LoadAccounts();
                    }
                }
                catch (Exception ex)
                {
                    App.LogError("读取配置文件时出错", ex);
                    MessageBox.Show($"读取配置文件时出错: {ex.Message}");
                }
            }
            else
            {
                App.Log("配置文件不存在，创建默认配置文件");
                CreateDefaultConfigFile();
                await Task.Delay(1000);
                LoadConfig();
            }
        }

        private void CreateDefaultConfigFile()
        {
            try
            {
                App.Log("开始创建默认配置文件");
                XDocument xmlDoc;
                if (File.Exists(configFileName))
                {
                    App.Log("配置文件已存在，加载现有配置文件");
                    xmlDoc = XDocument.Load(configFileName);
                }
                else
                {
                    App.Log("配置文件不存在，创建新的默认配置文件");
                    xmlDoc = new XDocument(
                        new XElement("config",
                            new XElement("game_data", ""),
                            new XElement("ID_7K", "")
                        )
                    );
                }
                xmlDoc.Save(configFileName);
                App.Log("默认配置文件创建/保存成功");
            }
            catch (Exception ex)
            {
                App.LogError("创建配置文件时出错", ex);
                MessageBox.Show($"创建配置文件时出错: {ex.Message}");
            }
        }

        private void Setting_Text_Changed(object sender, EventArgs e, string elementName, string value)
        {
            if (Load == false)
            {
                if (File.Exists(configFileName))
                {
                    try
                    {
                        XDocument xmlDoc = XDocument.Load(configFileName);
                        XElement element = xmlDoc.Root.Element(elementName);
                        if (element != null)
                        {
                            element.Value = value;
                        }
                        else
                        {
                            xmlDoc.Root.Add(new XElement(elementName, value));
                        }
                        xmlDoc.Save(configFileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存配置文件时出错: {ex.Message}");
                    }
                }
                else if (Load == false)
                {
                    try
                    {
                        XDocument xmlDoc = new XDocument(
                            new XElement("config",
                                new XElement("game_data", ""),
                                new XElement("ID_7K", "")
                            )
                        );
                        xmlDoc.Root.Add(new XElement(elementName, value));
                        xmlDoc.Save(configFileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"创建配置文件时出错: {ex.Message}");
                    }
                }
            }
        }


        private void GamePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Load == false)
            {
                string gameData = game_path_text.Text;
                App.Log($"游戏路径变更为: {gameData}");
                gamePath = gameData;
                GamePath = gameData;
                App.Log("游戏路径不为空，开始保存配置");
                Setting_Text_Changed(sender, e, "game_data", gameData);
                if (IsLoaded)
                {
                    Load49ID();
                }
            }
        }

        private void User_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Load == false)
            {
                string ID_7K = User_Text.Text;
                App.Log($"用户账号选择变更为: {ID_7K}");
                if (!string.IsNullOrEmpty(ID_7K))
                {
                    App.Log("用户账号不为空，开始保存配置");
                }
                Setting_Text_Changed(sender, e, "ID_7K", ID_7K);
            }
        }

        private void Load49ID()
        {
            DMM_Hide_Launcher.Others.CookieLoader.Load4399ID(GamePath);
        }

        private void Start_Game_4399_Click(object sender, RoutedEventArgs e)
        {
            App.Log("开始启动4399游戏");
            if (File.Exists(configFileName))
            {
                try
                {
                    XDocument xmlDoc = XDocument.Load(configFileName);
                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                    if (gameDataElement != null)
                    {
                        GamePath = gameDataElement.Value;
                        App.Log($"从配置文件读取游戏路径: {GamePath}");
                        if (string.IsNullOrEmpty(GamePath))
                        {
                            App.Log("游戏路径为空");
                            Growl.Warning("找不到游戏位置，请选择在xml中输入游戏目录");
                        }
                        else if (!Directory.Exists(GamePath))
                        {
                            App.Log("游戏目录不存在");
                            Growl.Warning("游戏目录不存在，请检查路径");
                        }
                        else
                        {
                            App.Log("游戏目录存在，开始检查游戏进程");
                            bool isRunning = IsProcessRunning("dmmdzz");
                            if (isRunning)
                            {
                                App.Log("检测到游戏正在运行");
#pragma warning disable CA1416 // 验证平台兼容性
                                Growl.Ask("检测到游戏正在运行，是否强制关闭游戏？", isConfirmed =>
                                {
                                    if (isConfirmed)
                                    {
                                        App.Log("用户确认强制关闭游戏");
                                        Process[] processes = Process.GetProcessesByName("dmmdzz");
                                        foreach (Process process in processes)
                                        {
                                            process.Kill();
                                        }
                                        App.Log("已强制关闭游戏进程");
                                        Growl.Success("已强制关闭游戏");
                                    }
                                    return true;
                                });
#pragma warning restore CA1416 // 验证平台兼容性
                            }
                            else
                            {
                                App.Log("游戏未在运行，开始查找游戏可执行文件");
                                string[] files = Directory.GetFiles(GamePath, "dmmdzz.exe", SearchOption.AllDirectories);
                                if (files.Length > 0)
                                {
                                    App.Log($"找到游戏可执行文件: {files[0]}");
                                    App.Log("启动游戏，参数: ID=4399OpenID,Key=4399OpenKey,PID=4399_0,PROCPARA=66666,Channel=PC4400");
                                    Process.Start(files[0], "ID=4399OpenID,Key=4399OpenKey,PID=4399_0,PROCPARA=66666,Channel=PC4400");
                                    App.Log("4399启动，等待窗口");
                                    CheckGameWindow();
                                }
                                else
                                {
                                    App.Log("未找到游戏可执行文件");
                                    Growl.Warning("未找到游戏");
                                }
                            }
                        }
                    }
                    else
                    {
                        App.Log("配置文件中game_data项不存在");
                        Growl.Warning("game_data 项不存在");
                    }
                }
                catch (Exception ex)
                {
                    App.LogError("启动游戏时出错", ex);
                    Growl.Warning($"启动游戏时出错: {ex.Message}");
                }
            }
            else if (Load == false)
            {
                App.Log("配置文件不存在，创建默认配置文件");
                CreateDefaultConfigFile();
                MessageBox.Show("已创建新的配置文件，请重新配置游戏路径和账号信息");
            }
        }
        private async void Start_Game_7k7k_Click(object sender, RoutedEventArgs e)
        {
            App.Log("开始启动7k7k游戏");
            if (File.Exists(configFileName))
            {
                try
                {
                    string ID_7K = User_Text.Text;
                    string Key_7K = Password.Password;
                    App.Log($"用户账号: {ID_7K}");
                    XDocument xmlDoc = XDocument.Load(configFileName);
                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                    if (gameDataElement != null)
                    {
                        string GamePath = gameDataElement.Value;
                        App.Log($"从配置文件读取游戏路径: {GamePath}");
                        if (string.IsNullOrEmpty(GamePath))
                        {
                            App.Log("游戏路径为空");
                            Growl.Warning("找不到游戏位置，请选择在xml中输入游戏目录");
                        }
                        else if (!Directory.Exists(GamePath))
                        {
                            App.Log("游戏目录不存在");
                            Growl.Warning("游戏目录不存在，请检查路径");
                        }
                        else
                        {
                            App.Log("游戏目录存在，开始处理账号信息");
                            XElement ID_7KElement = xmlDoc.Root.Element("ID_7K");
                            XElement KEY_7KElement = xmlDoc.Root.Element("Key_7K");

                            // 如果 ID_7K 或 KEY_7K 元素不存在，则创建它们
                            if (ID_7KElement == null)
                            {
                                ID_7KElement = new XElement("ID_7K", "");
                                xmlDoc.Root.Add(ID_7KElement);
                            }
                            if (KEY_7KElement == null)
                            {
                                KEY_7KElement = new XElement("Key_7K", "");
                                xmlDoc.Root.Add(KEY_7KElement);
                            }

                            // 检查是否为空白字符
                            if (string.IsNullOrWhiteSpace(ID_7K) || string.IsNullOrWhiteSpace(Key_7K))
                            {
                                App.Log("账号或密码为空");
                                // 调试输出
                                //MessageBox.Show($"Username: {username}, Password: {password}");
                                Growl.Warning("请输入账密");
                            }
                            else
                            {
                                App.Log("账号和密码已输入，开始检查游戏进程");
                                bool isRunning = IsProcessRunning("dmmdzz");
                                if (isRunning)
                                {
                                    App.Log("检测到游戏进程正在运行");
#pragma warning disable CA1416 // 验证平台兼容性
                                    Growl.Ask("检测到游戏正在运行，是否强制关闭游戏？", isConfirmed =>
                                    {
                                        if (isConfirmed)
                                        {
                                            App.Log("用户确认强制关闭游戏进程");
                                            Process[] processes = Process.GetProcessesByName("dmmdzz");
                                            foreach (Process process in processes)
                                            {
                                                process.Kill();
                                            }
                                            App.Log("已成功强制关闭游戏进程");
                                            Growl.Success("已强制关闭游戏");
                                        }
                                        return true;
                                    });
#pragma warning restore CA1416 // 验证平台兼容性
                                }
                                else
                                {
                                    App.Log("游戏未在运行，准备启动游戏");
                                    try
                                    {
                                        GamePath = gameDataElement.Value;
                                        App.Log("开始执行HTTP请求获取游戏启动参数");
                                        string result = await HttpRequester.ExecuteRequests(ID_7K, Key_7K, GamePath);
                                        
                                        // 处理返回结果
                                        if (result.StartsWith("ERROR:"))
                                        {
                                            string errorCode = result.Substring(6); // 去掉"ERROR:"前缀
                                            App.LogError("获取游戏启动参数失败", new Exception(errorCode));
                                            
                                            // 根据错误代码显示对应的错误信息
                                            if (errorCode == "INVALID_CREDENTIALS")
                                            {
                                                Growl.Warning("错误的账户/密码");
                                            }
                                            else if (errorCode == "GAME_NOT_FOUND")
                                            {
                                                Growl.Warning("未找到游戏");
                                            }
                                            else
                                            {
                                                Growl.Warning($"获取游戏启动参数失败: {errorCode}");
                                            }
                                        }
                                        else if (result.Contains("|"))
                                        {
                                            // 解析游戏路径和启动参数
                                            string[] parts = result.Split('|');
                                            if (parts.Length == 2)
                                            {
                                                string gameExePath = parts[0];
                                                string startParams = parts[1];
                                                
                                                App.Log($"成功获取游戏启动参数，开始启动游戏: {gameExePath} 启动参数: {startParams}");
                                                Process.Start(gameExePath, startParams);
                                                App.Log("7K7K启动，等待窗口");
                                                CheckGameWindow();
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        App.LogError("游戏启动过程出错", ex);
                                        Growl.Error($"发生错误: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Growl.Warning("game_data 项不存在");
                    }
                }
                catch (Exception ex)
                {
                    Growl.Warning($"读取配置文件时出错: {ex.Message}");
                }
            }
        }
        /// <summary>
        /// 检查指定名称的进程是否正在运行
        /// </summary>
        /// <param name="ProcessName">要检查的进程名称（不包含.exe扩展名）</param>
        /// <returns>如果进程正在运行则返回true，否则返回false</returns>
        static bool IsProcessRunning(string ProcessName)
        {
            try
            {
                // 直接使用GetProcessesByName获取指定名称的进程，避免遍历所有进程
                return Process.GetProcessesByName(ProcessName).Length > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查进程时出错: {ex.Message}");
            }

            return false;
        }


        /// <summary>
        /// 编辑窗口关闭事件处理方法
        /// 当账号编辑窗口关闭时，重新加载账号列表并刷新显示
        /// </summary>
        /// <param name="sender">事件发送者（编辑窗口）</param>
        /// <param name="e">事件参数</param>
        private void EditWindow_WindowClosed(object sender, EventArgs e)
        {
            // 刷新lstAccounts
            LoadAccounts();
            lstAccounts.Items.Refresh();
        }

        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(AccountsFilePath))
                {
                    Accounts.Clear();
                    string json = File.ReadAllText(AccountsFilePath);
                    
                    using (StringReader stringReader = new StringReader(json))
                    using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
                    {
                        var accounts = JsonSerializer.Create().Deserialize<List<Account>>(jsonReader);
                        
                        if (accounts != null)
                        {
                            foreach (var account in accounts)
                            {
                                Accounts.Add(account);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("加载账号数据失败", ex);
                MessageBox.Show($"加载账号数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            User_Edit editWindow = new User_Edit();

            // 设置窗口的所有者为当前窗口
            editWindow.Owner = this;

            editWindow.User_Edit_Close += EditWindow_WindowClosed;

            // 显示模态对话框
            bool? result = editWindow.ShowDialog();
        }

        private void Use_User_Button_Click(object sender, RoutedEventArgs e)
        {
            App.Log("开始账号切换操作");
            if (sender is System.Windows.Controls.Button button && button.Tag is Account account)
            {
                App.Log($"切换至账号: {account.Username}");
                User_Text.Text = account.Username;
                Password.Password = CryptoHelper.DecryptString(account.Password);
                _selectedAccount = account;
                User_Tab.SelectedIndex = 1;
                App.Log("账号切换完成，切换到游戏启动标签页");
                Growl.Success($"已切换至账号: {account.Username}");
            }
            else
            {
                App.Log("账号切换失败: 无效的按钮或账号数据");
            }
        }

        /// <summary>
        /// 查找游戏路径按钮点击事件
        /// 异步搜索游戏目录，支持自动搜索和手动选择两种方式
        /// </summary>
        /// <param name="sender">事件发送者（按钮）</param>
        /// <param name="e">路由事件参数</param>
        private async void FindPathButton_Click(object sender, RoutedEventArgs e)
        {
            App.Log("开始游戏路径查找操作");
            System.Windows.Controls.Button findButton = sender as System.Windows.Controls.Button;
            string originalContent = findButton?.Content.ToString();
            
            try
            {
                // 禁用按钮，显示搜索中状态
                if (findButton != null)
                {
                    findButton.IsEnabled = false;
                    findButton.Content = "搜索中...";
                }
                
                // 显示搜索开始的提示
                Growl.Info("正在搜索游戏目录，请稍候...");
                
                // 创建GamePathFinder实例
                App.Log("创建GamePathFinder实例");
                GamePathFinder pathFinder = new GamePathFinder();
                
                // 使用异步操作执行搜索，避免UI卡顿
                // 添加超时机制，最多等待15秒
                List<string> gamePaths = await Task.Run(() =>
                {
                    return pathFinder.FindGamePaths();
                }).ConfigureAwait(true);
                
                App.Log($"游戏路径搜索完成，找到{gamePaths.Count}个游戏路径");
                
                if (gamePaths.Count > 0)
                {
                    // 如果找到多个路径，让用户选择
                    if (gamePaths.Count > 1)
                    {
                        App.Log($"找到{gamePaths.Count}个游戏路径，显示路径选择对话框");
                        ChoosePathFind chooseDialog = new ChoosePathFind(gamePaths);
                        chooseDialog.Owner = this;
                        bool? result = chooseDialog.ShowDialog();
                        
                        if (result == true && !string.IsNullOrEmpty(chooseDialog.SelectedPath))
                        {
                            // 用户选择了路径
                            SetGamePath(chooseDialog.SelectedPath);
                            Growl.Success("已选择游戏目录");
                        }
                        // 如果用户取消选择，仍使用第一个路径
                        else if (result == false)
                        {
                            SetGamePath(gamePaths[0]);
                            Growl.Success("已使用默认游戏目录");
                        }
                    }
                    else
                    {
                        // 只有一个路径，直接使用
                        SetGamePath(gamePaths[0]);
                        Growl.Success("成功找到游戏目录");
                        
                        // 加载4399 ID信息
                        if (IsLoaded)
                        {
                            Load49ID();
                        }
                    }
                }
                else
                {
                    // 未找到路径，提供手动选择选项
                    App.Log("未找到游戏目录，提供手动选择选项");
                    AdonisUI.Controls.MessageBoxResult result = AdonisUI.Controls.MessageBox.Show(
                        "未找到游戏目录，是否手动选择？", 
                        "提示", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Information
                    );
                    
                    if (result == AdonisUI.Controls.MessageBoxResult.Yes)
                    {
                        // 使用using语句确保资源正确释放
                        using (System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog
                        {
                            Description = "请选择游戏安装目录",
                            ShowNewFolderButton = false
                        })
                        {
                            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                            {
                                string manualPath = folderDialog.SelectedPath;
                                if (pathFinder.ValidateGamePath(manualPath))
                                {
                                    SetGamePath(manualPath);
                                    Growl.Success("已手动选择游戏目录");
                                }
                                else
                                {
                                    Growl.Error("所选目录不包含游戏文件，请重新选择");
                                }
                            }
                        }
                    }
                    else
                    {
                        Growl.Warning("请手动输入游戏目录路径");
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("查找游戏目录时出错", ex);
                AdonisUI.Controls.MessageBox.Show(
                    $"查找游戏目录时出错: {ex.Message}", 
                    "错误", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                );
            }
            finally
            {
                // 恢复按钮状态
                if (findButton != null)
                {
                    findButton.IsEnabled = true;
                    findButton.Content = originalContent;
                }
            }
        }
        
        /// <summary>
        /// 设置游戏路径并保存到配置文件
        /// </summary>
        /// <param name="path">游戏路径</param>
        private void SetGamePath(string path)
        {
            App.Log($"设置游戏路径为: {path}");
            game_path_text.Text = path;
            gamePath = path;
            GamePath = path;
            
            // 保存到配置文件
            if (File.Exists(configFileName))
            {
                try
                {
                    App.Log("配置文件存在，开始保存游戏路径");
                    XDocument xmlDoc = XDocument.Load(configFileName);
                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                    if (gameDataElement != null)
                    {
                        App.Log("game_data元素已存在，更新值");
                        gameDataElement.Value = path;
                    }
                    else
                    {
                        App.Log("game_data元素不存在，创建新元素");
                        xmlDoc.Root.Add(new XElement("game_data", path));
                    }
                    xmlDoc.Save(configFileName);
                    App.Log("游戏路径已保存到配置文件");
                }
                catch (Exception ex)
                {
                    App.LogError("保存游戏路径到配置文件时出错", ex);
                    Growl.Error("保存配置失败，但路径已设置");
                }
            }
            else
            {
                App.Log("配置文件不存在，跳过保存");
            }
        }
        
        /// <summary>
        /// 主题切换按钮点击事件
        /// 在亮色主题和暗色主题之间切换
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        private void Button_Theme_Click(object sender, RoutedEventArgs e)
        {
            App.Log("开始主题切换操作");
            
            // 切换主题状态
            _isDark = !_isDark;
            
            // 调用App类中的主题设置方法
            App.Log(_isDark ? "当前为暗色主题，准备切换到亮色主题" : "当前为亮色主题，准备切换到暗色主题");
            
            // 切换图标
            ImageBrush Theme_Ico = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(_isDark ? 
                    "pack://application:,,,/DMM_Hide_Launcher;component/public/sun.ico" : 
                    "pack://application:,,,/DMM_Hide_Launcher;component/public/moon.ico", 
                    UriKind.Absolute)),
                Stretch = Stretch.UniformToFill
            };
            Panel_Theme.Background = Theme_Ico;
            
            // 通过App类设置应用主题
            (App.Current as App)?.SetAppTheme(_isDark);
            
            App.Log($"主题切换完成，当前主题: {(_isDark ? "暗色" : "亮色")}");
        }
        
        /// <summary>
        /// 检测游戏窗口是否运行
        /// 简化版：使用单一线程处理窗口检测，避免过多嵌套的任务
        /// </summary>
        private void CheckGameWindow()
        {
            App.Log("开始检测游戏窗口");
            
            // 在后台线程执行，不阻塞UI
            Task.Run(async () =>
            {
                try
                {
                    // 创建窗口检测器
                    CheckGameWindow_Others detector = new CheckGameWindow_Others();
                    bool isRunning = detector.IsTargetWindowRunning();
                    
                    if (isRunning)
                    {
                        // 获取运行中的窗口列表
                        List<string> runningWindows = detector.GetRunningTargetWindows();
                        
                        // 在UI线程上显示结果
                        await this.Dispatcher.InvokeAsync(() =>
                        {
                            App.Log($"检测到游戏窗口正在运行，共找到{runningWindows.Count}个窗口");
                            
                            // 显示找到的窗口信息
                            StringBuilder messageBuilder = new StringBuilder();
                            messageBuilder.AppendLine("游戏成功启动！");
                            runningWindows.ForEach(title => messageBuilder.AppendLine($"- {title}"));
                            
                            Growl.Success(messageBuilder.ToString());
                        }).Task;
                    }
                    else
                    {
                        // 在UI线程上显示结果并启动持续监测
                        await this.Dispatcher.InvokeAsync(() =>
                        {
                            App.Log("未检测到游戏窗口运行，开始持续监测");
                            Growl.Info("正在检测游戏是否正常启动...");
                            InitializeWindowMonitoring();
                        }).Task;
                    }
                }
                catch (Exception ex)
                {
                    App.LogError("检测游戏窗口时出错", ex);
                    
                    // 在UI线程上显示错误信息
                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        Growl.Error("检测游戏窗口时发生错误，请查看日志了解详情");
                    }).Task;
                }
            });
        }
        
        /// <summary>
        /// 初始化窗口监测功能
        /// 创建WindowDetector实例并开始持续监测
        /// <summary>
        /// 初始化窗口监测功能
        /// 简化版：在单任务中完成初始化和事件绑定
        /// </summary>
        private void InitializeWindowMonitoring()
        {
            // 在后台线程执行初始化，避免阻塞UI
            Task.Run(async () =>
            {
                try
                {
                    // 创建CheckGameWindow_Others实例
                    _windowDetector = new CheckGameWindow_Others();
                    _windowDetector.MonitoringInterval = 200; // 200毫秒，提高检测速度
                    
                    // 注册窗口状态变化事件处理程序
                    _windowDetector.WindowStateChanged += OnGameWindowStateChanged;
                    
                    // 开始持续监测
                    _windowDetector.StartMonitoring();
                    
                    // 在UI线程上记录日志
                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        App.Log("窗口监测功能已初始化并启动");
                    }).Task;
                }
                catch (Exception ex)
                {
                    // 在UI线程上记录错误
                    await this.Dispatcher.InvokeAsync(() =>
                    {
                        App.LogError("初始化窗口监测功能时出错", ex);
                        Growl.Error("初始化窗口监测功能时发生错误");
                    }).Task;
                }
            });
        }
        
        /// <summary>
        /// 窗口状态变化事件处理程序
        /// </summary>
        private void OnGameWindowStateChanged(object sender, WindowStateChangedEventArgs e)
        {
            try
            {
                // 只处理窗口启动事件
                if (e.IsRunning)
                {
                    // 在UI线程上显示结果和播放提示音
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 播放提示音
                        PlayNotificationSound();

                        App.Log($"检测到游戏窗口启动，运行中的窗口: {string.Join(", ", e.RunningWindowTitles)}");
                        
                        // 显示找到的窗口信息
                        StringBuilder messageBuilder = new StringBuilder();
                        messageBuilder.AppendLine("游戏成功启动！");
                        e.RunningWindowTitles.ForEach(title => messageBuilder.AppendLine($"- {title}"));
                        
                        Growl.Success(messageBuilder.ToString());
                        
                        
                    }));
                    
                    // 停止监测
                    StopGameWindowMonitoring();
                }
            }
            catch (Exception ex)
            {
                App.LogError("处理窗口状态变化事件时出错", ex);
            }
        }
        
        /// <summary>
        /// 播放通知提示音
        /// 增强版：添加了更健壮的错误处理和文件格式检测
        /// </summary>
        private void PlayNotificationSound()
        {
            try
            {
                // 使用Application.GetResourceStream从内嵌资源中加载提示音
                string soundUri = "pack://application:,,,/DMM_Hide_Launcher;component/public/Message.wav";
                App.Log("尝试播放提示音(内嵌资源): " + soundUri);
                
                // 使用Application.GetResourceStream获取资源流
                Uri uri = new Uri(soundUri, UriKind.RelativeOrAbsolute);
                System.Windows.Resources.StreamResourceInfo resourceInfo = System.Windows.Application.GetResourceStream(uri);
                
                if (resourceInfo != null && resourceInfo.Stream != null)
                {
                    using (var stream = resourceInfo.Stream)
                    {
                        try
                        {
                            // 验证文件大小，避免过小的文件
                            if (stream.Length < 44) // WAV文件至少需要44字节的头部
                            {
                                App.LogWarning("提示音文件过小，可能不是有效的WAV文件: " + stream.Length + " 字节");
                                return;
                            }
                            
                            // 检查WAV文件头标志
                            byte[] header = new byte[4];
                            stream.Read(header, 0, 4);
                            string headerString = System.Text.Encoding.ASCII.GetString(header);
                            stream.Seek(0, SeekOrigin.Begin); // 重置流位置
                            
                            if (headerString != "RIFF")
                            {
                                App.LogWarning("检测到无效的WAV文件头: " + headerString);
                                return;
                            }
                            
                            // 从资源流创建SoundPlayer
                            using (System.Media.SoundPlayer player = new System.Media.SoundPlayer(stream))
                            {
                                // 先同步加载，确保文件格式正确
                                player.Load(); // 同步加载，可以捕获格式错误
                                player.Play(); // 异步播放，不会阻塞UI
                                App.Log("提示音播放请求已发送");
                            }
                        }
                        catch (InvalidOperationException ioEx)
                        {
                            // 特定处理WAV文件格式错误
                            App.LogError("WAV文件格式错误: " + ioEx.Message, ioEx);
                            // 尝试使用Windows内置提示音作为备选
                            try
                            {
                                System.Media.SystemSounds.Asterisk.Play();
                                App.Log("已使用系统默认提示音代替");
                            }
                            catch (Exception sysEx)
                            {
                                App.LogError("播放系统提示音时也出错", sysEx);
                            }
                        }
                    }
                }
                else
                {
                    App.LogWarning("无法获取内嵌提示音资源");
                    // 尝试使用Windows内置提示音作为备选
                    try
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                        App.Log("已使用系统默认提示音代替");
                    }
                    catch (Exception sysEx)
                    {
                        App.LogError("播放系统提示音时也出错", sysEx);
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("播放提示音时出错", ex);
            }
        }
        
        /// <summary>
        /// 停止游戏窗口监测
        /// </summary>
        private void StopGameWindowMonitoring()
        {
            try
            {
                if (_windowDetector != null && _windowDetector.IsMonitoring)
                {
                    _windowDetector.WindowStateChanged -= OnGameWindowStateChanged;
                    _windowDetector.StopMonitoring();
                    App.Log("窗口监测已停止（已检测到目标窗口）");
                }
            }
            catch (Exception ex)
            {
                App.LogError("停止窗口监测时出错", ex);
            }
        }
        
        /// <summary>
        /// 窗口关闭时调用
        /// 停止窗口监测并释放资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            try
            {
                // 停止窗口监测
                if (_windowDetector != null && _windowDetector.IsMonitoring)
                {
                    _windowDetector.WindowStateChanged -= OnGameWindowStateChanged;
                    _windowDetector.StopMonitoring();
                    App.Log("窗口监测已停止");
                }
            }
            catch (Exception ex)
            {
                App.LogError("停止窗口监测时出错", ex);
            }
        }
        
        /// <summary>
        /// 计数按钮点击事件处理程序
        /// 打开计数窗口
        /// </summary>
        private void CountButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CountWindow countWindow = new CountWindow();
                countWindow.Show();
            }
            catch (Exception ex)
            {
                App.LogError("打开计数窗口时出错", ex);
                MessageBox.Show("打开计数窗口失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

