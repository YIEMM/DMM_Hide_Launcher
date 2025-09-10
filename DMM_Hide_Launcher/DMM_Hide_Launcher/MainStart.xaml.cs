using AdonisUI;
using AdonisUI.Controls;
using HandyControl.Controls;
using Login_7k7k;
using Newtonsoft.Json;
using Others;
using DMMDZZ_Game_Start.Others;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using System.Windows.Media;


namespace DMMDZZ_Game_Start
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
        /// 主窗口构造函数
        /// 初始化窗口组件并开始加载程序资源
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
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
            CookieID_4399.CookieLoader.Load4399ID(GamePath);
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
                                    App.Log("4399游戏启动成功");
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
                                        App.Log("开始执行HTTP请求启动游戏");
                                        string result = await HttpRequester.ExecuteRequests(ID_7K, Key_7K, GamePath);
                                        App.Log("游戏启动请求已发送");
                                    }
                                    catch (Exception ex)
                                    {
                                        App.LogError("游戏启动过程出错", ex);
                                        MessageBox.Show($"发生错误: {ex.Message}");
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
        static bool IsProcessRunning(string ProcessName)
        {
            try
            {
                foreach (Process process in Process.GetProcesses())
                {
                    if (process.ProcessName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查进程时出错: {ex.Message}");
            }

            return false;
        }


        private void EditWindow_WindowClosed(object sender, EventArgs e)
        {
            // 刷新lstAccounts
            LoadAccounts();
            lstAccounts.Items.Refresh();
        }

        private void LoadAccounts()
        {
            App.Log("开始加载账号信息: " + AccountsFilePath);
            
            try
            {
                if (File.Exists(AccountsFilePath))
                {
                    App.Log("账号文件存在，开始解析");
                    
                    App.Log("开始清空现有账号列表");
                    Accounts.Clear();
                    App.Log("账号列表清空完成");
                    
                    App.Log("开始读取账号文件内容");
                    string json = File.ReadAllText(AccountsFilePath);
                    App.Log("账号文件内容读取完成");
                    
                    App.Log("开始设置JSON读取器");
                    using (StringReader stringReader = new StringReader(json))
                    using (JsonTextReader jsonReader = new JsonTextReader(stringReader))
                    {
                        App.Log("开始反序列化账号数据");
                        var accounts = JsonSerializer.Create().Deserialize<List<Account>>(jsonReader);
                        App.Log("账号数据反序列化完成");

                        if (accounts != null)
                        {
                            App.Log("成功解析账号文件，共找到 " + accounts.Count + " 个账号");
                            
                            App.Log("开始添加账号到集合");
                            foreach (var account in accounts)
                            {
                                App.Log($"添加账号: {account.Username}");
                                Accounts.Add(account);
                            }
                            App.Log("所有账号添加完成");
                        }
                        else
                        {
                            App.Log("解析结果为空，未找到任何账号");
                        }
                    }
                    App.Log("账号加载流程完成");
                }
                else
                {
                    App.Log("账号文件不存在，跳过加载");
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
                Password.Password = account.Password;
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

        private void FindPathButton_Click(object sender, RoutedEventArgs e)
        {
            App.Log("开始游戏路径查找操作");
            try
            {
                // 创建GamePathFinder实例
                App.Log("创建GamePathFinder实例");
                GamePathFinder pathFinder = new GamePathFinder();
                App.Log("开始搜索游戏路径");
                List<string> gamePaths = pathFinder.FindGamePaths();
                App.Log($"游戏路径搜索完成，找到{gamePaths.Count}个游戏路径");
                
                if (gamePaths.Count > 0)
                {
                    // 如果找到多个路径，选择第一个
                    App.Log($"默认游戏路径设置为: {gamePaths[0]}");
                    game_path_text.Text = gamePaths[0];
                    gamePath = gamePaths[0];
                    GamePath = gamePaths[0];
                    
                    // 保存到配置文件
                    if (File.Exists(configFileName))
                    {
                        App.Log("配置文件存在，开始保存游戏路径");
                        XDocument xmlDoc = XDocument.Load(configFileName);
                        XElement gameDataElement = xmlDoc.Root.Element("game_data");
                        if (gameDataElement != null)
                        {
                            App.Log("game_data元素已存在，更新值");
                            gameDataElement.Value = gamePaths[0];
                        }
                        else
                        {
                            App.Log("game_data元素不存在，创建新元素");
                            xmlDoc.Root.Add(new XElement("game_data", gamePaths[0]));
                        }
                        xmlDoc.Save(configFileName);
                        App.Log("游戏路径已保存到配置文件");
                    }
                    else
                    {
                        App.Log("配置文件不存在，跳过保存");
                    }
                    
                    // 如果找到多个路径，调用选择页面
                        if (gamePaths.Count > 1)
                        {
                            App.Log($"找到{gamePaths.Count}个游戏路径，显示路径选择对话框");
                            ChoosePathFind chooseDialog = new ChoosePathFind(gamePaths);
                            chooseDialog.Owner = this;
                            bool? result = chooseDialog.ShowDialog();
                            
                            if (result == true && !string.IsNullOrEmpty(chooseDialog.SelectedPath))
                            {
                                App.Log($"用户选择了游戏路径: {chooseDialog.SelectedPath}");
                                // 用户选择了路径
                                game_path_text.Text = chooseDialog.SelectedPath;
                                gamePath = chooseDialog.SelectedPath;
                                GamePath = chooseDialog.SelectedPath;
                                
                                // 保存到配置文件
                                if (File.Exists(configFileName))
                                {
                                    App.Log("配置文件存在，开始保存用户选择的游戏路径");
                                    XDocument xmlDoc = XDocument.Load(configFileName);
                                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                                    if (gameDataElement != null)
                                    {
                                        App.Log("game_data元素已存在，更新为用户选择的路径");
                                        gameDataElement.Value = chooseDialog.SelectedPath;
                                    }
                                    else
                                    {
                                        App.Log("game_data元素不存在，创建新元素保存用户选择的路径");
                                        xmlDoc.Root.Add(new XElement("game_data", chooseDialog.SelectedPath));
                                    }
                                    xmlDoc.Save(configFileName);
                                    App.Log("用户选择的游戏路径已保存到配置文件");
                                }
                                else
                                {
                                    App.Log("配置文件不存在，跳过保存用户选择的路径");
                                }
                                
                                Growl.Success("已选择游戏目录");
                            }
                        }
                    else
                    {
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
                    Growl.Warning("未找到游戏目录，请手动输入");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查找游戏目录时出错: {ex.Message}");
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
            // 先清除当前的资源字典
            System.Windows.Application.Current.Resources.MergedDictionaries.Clear();
            
            if (_isDark)
            {
                App.Log("当前为暗色主题，准备切换到亮色主题");
                // 切换到亮色主题
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
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml")
                });
                _isDark = false;
                // 切换到太阳图标（亮色主题）
                ImageBrush Theme_Ico = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/DMM_Hide_Launcher;component/public/moon.ico", UriKind.Absolute)),
                    Stretch = Stretch.UniformToFill
                };
                Panel_Theme.Background = Theme_Ico;
            }
            else
            {
                App.Log("当前为亮色主题，准备切换到暗色主题");
                // 切换到暗色主题
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
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/AdonisUI.ClassicTheme;component/Resources.xaml")
                });
                _isDark = true;
                // 切换到月亮图标（暗色主题）
                ImageBrush Theme_Ico = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri("pack://application:,,,/DMM_Hide_Launcher;component/public/sun.ico", UriKind.Absolute)),
                    Stretch = Stretch.UniformToFill
                };
                Panel_Theme.Background = Theme_Ico;
            }
            
            App.Log($"主题切换完成，当前主题: {(_isDark ? "暗色" : "亮色")}");
        }
    }
}

