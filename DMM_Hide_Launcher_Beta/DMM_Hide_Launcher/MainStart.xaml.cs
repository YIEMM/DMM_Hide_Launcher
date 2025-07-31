using AdonisUI;
using AdonisUI.Controls;
using HandyControl.Controls;
using Login_7k7k;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;


namespace DMMDZZ_Game_Start
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : AdonisWindow
    {
        public ObservableCollection<Account> Accounts { get; set; }
        public static string Game_Version;
        public static string xmlUrl = "https://update.version.brmyx.com/get_verion_control_xml_string.xml?pfID=36&appId=com.bairimeng.dmmdzz.pc.36&versionKey=";
        private string gamePath;
        private string GamePath;
        private readonly string configFileName = "config.xml";
        private readonly HttpClient httpClient = new HttpClient();
        private bool Load = true;
        private Account _selectedAccount;
        private const string AccountsFilePath = "accounts.json";
        private bool _isDark;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            LoadXmlData();
            Accounts = new ObservableCollection<Account>();
            lstAccounts.ItemsSource = Accounts;

            // 监听集合变更，更新按钮绑定
            Accounts.CollectionChanged += (s, e) =>
            {
                // 强制刷新ItemsControl的容器（触发按钮绑定）
                lstAccounts.Items.Refresh();
            };
        }

        public class Account
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }


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
        //保存
        private void SaveVersionDataToFile(object sender, RoutedEventArgs e)
        {
            try
            {
                Dictionary<string, List<string>> data = new Dictionary<string, List<string>>
                {
                    { "version", new List<string> { Game_Version } },
                    { "patchVersion", new List<string> { Version_Patch.Text } },
                    { "updateNoteContent", new List<string> { XML_Text.Text } },
                    { "fixNoteContent", new List<string> { XML_Text.Text } }
                };
                string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText("version_data.json", jsonData);
                Growl.Success("已保存更新信息");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存版本信息时出错: {ex.Message}");
            }
        }
        private async Task Load_Update_XML_First()
        {
            try
            {
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
                        Console.WriteLine(Game_Version);
                    }
                    else
                    {
                        Console.WriteLine("未找到版本号");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("发生错误: " + ex.Message);
            }
        }
        private async Task<Dictionary<string, List<string>>> Load_Update_XML_Second()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(xmlUrl + Game_Version);
                response.EnsureSuccessStatusCode();

                string xmlContent = await response.Content.ReadAsStringAsync();
                XElement root = XElement.Parse(xmlContent);

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
                throw new Exception($"获取更新信息失败: {ex.Message}", ex);
            }

        }
        private async void Version_Reload()
        {
            try
            {
                string[] parts = Game_Version.Split('.');

                if (parts.Length == 4)
                {
                    parts[3] = "0";
                    Game_Version = string.Join(".", parts);

                }
                else
                {
                    Game_Version = Game_Version+".0";
                    MessageBox.Show(Game_Version);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"版本号处理失败: {ex.Message}");
            }
        }

        private async void LoadConfig()
        {
            if (File.Exists(configFileName))
            {
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
                    User_Text.Text = ID7K;
                    game_path_text.Text = GamePath;
                    Load = false;
                    await Task.Delay(100);
                    if (IsLoaded)
                    {
                        Load49ID();
                        LoadAccounts();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取配置文件时出错: {ex.Message}");
                }
            }
            else
            {
                CreateDefaultConfigFile();
                await Task.Delay(1000);
                LoadConfig();
            }
        }

        private void CreateDefaultConfigFile()
        {
            try
            {
                XDocument xmlDoc;
                if (File.Exists(configFileName))
                {
                    xmlDoc = XDocument.Load(configFileName);
                }
                else
                {
                    xmlDoc = new XDocument(
                        new XElement("config",
                            new XElement("game_data", ""),
                            new XElement("ID_7K", "")
                        )
                    );
                }
                xmlDoc.Save(configFileName);
            }
            catch (Exception ex)
            {
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
                gamePath = gameData;
                GamePath = gameData;
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
                Setting_Text_Changed(sender, e, "ID_7K", ID_7K);
            }
        }

        private void Load49ID()
        {
            CookieID_4399.CookieLoader.Load4399ID(GamePath);
        }

        private void Start_Game_4399_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(configFileName))
            {
                try
                {
                    XDocument xmlDoc = XDocument.Load(configFileName);
                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                    if (gameDataElement != null)
                    {
                        GamePath = gameDataElement.Value;
                        if (string.IsNullOrEmpty(GamePath))
                        {
                            Growl.Warning("找不到游戏位置，请选择在xml中输入游戏目录");
                        }
                        else if (!Directory.Exists(GamePath))
                        {
                            Growl.Warning("游戏目录不存在，请检查路径");
                        }
                        else
                        {
                            bool isRunning = IsProcessRunning("dmmdzz");
                            if (isRunning)
                            {
#pragma warning disable CA1416 // 验证平台兼容性
                                Growl.Ask("检测到游戏正在运行，是否强制关闭游戏？", isConfirmed =>
                                {
                                    if (isConfirmed)
                                    {
                                        Process[] processes = Process.GetProcessesByName("dmmdzz");
                                        foreach (Process process in processes)
                                        {
                                            process.Kill();
                                        }
                                        Growl.Success("已强制关闭游戏");
                                    }
                                    return true;
                                });
#pragma warning restore CA1416 // 验证平台兼容性
                            }
                            else
                            {
                                string[] files = Directory.GetFiles(GamePath, "dmmdzz.exe", SearchOption.AllDirectories);
                                if (files.Length > 0)
                                {
                                    Process.Start(files[0], "ID=4399OpenID,Key=4399OpenKey,PID=4399_0,PROCPARA=66666,Channel=PC4400");
                                }
                                else
                                {
                                    Growl.Warning("未找到游戏");
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
                    Growl.Warning($"启动游戏时出错: {ex.Message}");
                }
            }
            else if (Load == false)
            {
                CreateDefaultConfigFile();
                MessageBox.Show("已创建新的配置文件，请重新配置游戏路径和账号信息");
            }
        }
        private async void Start_Game_7k7k_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(configFileName))
            {
                try
                {
                    string ID_7K = User_Text.Text;
                    string Key_7K = Password.Password;
                    XDocument xmlDoc = XDocument.Load(configFileName);
                    XElement gameDataElement = xmlDoc.Root.Element("game_data");
                    if (gameDataElement != null)
                    {
                        string GamePath = gameDataElement.Value;
                        if (string.IsNullOrEmpty(GamePath))
                        {
                            Growl.Warning("找不到游戏位置，请选择在xml中输入游戏目录");
                        }
                        else if (!Directory.Exists(GamePath))
                        {
                            Growl.Warning("游戏目录不存在，请检查路径");
                        }
                        else
                        {
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
                                // 调试输出
                                //MessageBox.Show($"Username: {username}, Password: {password}");
                                Growl.Warning("请输入账密");
                            }
                            else
                            {
                                bool isRunning = IsProcessRunning("dmmdzz");
                                if (isRunning)
                                {
#pragma warning disable CA1416 // 验证平台兼容性
                                    Growl.Ask("检测到游戏正在运行，是否强制关闭游戏？", isConfirmed =>
                                    {
                                        if (isConfirmed)
                                        {
                                            Process[] processes = Process.GetProcessesByName("dmmdzz");
                                            foreach (Process process in processes)
                                            {
                                                process.Kill();
                                            }
                                            Growl.Success("已强制关闭游戏");
                                        }
                                        return true;
                                    });
#pragma warning restore CA1416 // 验证平台兼容性
                                }
                                else
                                {
                                    try
                                    {
                                        GamePath = gameDataElement.Value;
                                        string result = await HttpRequester.ExecuteRequests(ID_7K, Key_7K, GamePath);
                                    }
                                    catch (Exception ex)
                                    {
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
            if (sender is System.Windows.Controls.Button button && button.Tag is Account account)
            {
                User_Text.Text = account.Username;
                Password.Password = account.Password;
                _selectedAccount = account;
                User_Tab.SelectedIndex = 1;
                Growl.Success($"已切换至账号: {account.Username}");
            }
        }

        private void Button_Theme_Click(object sender, RoutedEventArgs e)
        {
            ResourceLocator.SetColorScheme(System.Windows.Application.Current.Resources, _isDark ? ResourceLocator.LightColorScheme :  ResourceLocator.DarkColorScheme);
            _isDark = !_isDark;
        }
    }
}

