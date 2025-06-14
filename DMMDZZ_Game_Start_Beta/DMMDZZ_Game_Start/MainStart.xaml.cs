using AdonisUI.Controls;
using Login_7k7k;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml.Linq;
using static DMMDZZ_Game_Start.MainWindow;
using static DMMDZZ_Game_Start.User_Edit;
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
        private static readonly string xmlUrl = "https://update.version.brmyx.com/get_verion_control_xml_string.xml?pfID=36&appId=com.bairimeng.dmmdzz.pc.36";
        private string gamePath;
        private string GamePath;
        private readonly string configFileName = "config.xml";
        private readonly HttpClient httpClient = new HttpClient();
        private bool Load = true;
        private Account _selectedAccount;
        private const string AccountsFilePath = "accounts.json";

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
                var extractedData = await FetchXmlData();

                if (extractedData["fixNoteContent"].Count > 0 && XML_Text != null)
                {
                    XML_Text.Text = extractedData["fixNoteContent"][0];
                }

                if (extractedData["patchVersion"].Count > 0 && Version_Patch != null)
                {
                    Version_Patch.Text = extractedData["patchVersion"][0];
                }
            }
            catch (Exception )
            {
                if (Version_Patch != null)
                {
                    Version_Patch.Text = "错误";
                }

                if (XML_Text != null)
                {
                    XML_Text.Text = "处于无网络状态，请稍后再试或联系开发者";
                }
            }
        }
        //保存
        private void SaveVersionDataToFile(Dictionary<string, List<string>> data)
        {
            try
            {
                string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText("version_data.json", jsonData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存版本信息时出错: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, List<string>>> FetchXmlData()
        {
            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(xmlUrl);
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

        private async  void LoadConfig()
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
                            MessageBox.Show("找不到游戏位置，请选择在xml中输入游戏目录");
                        }
                        else if (!Directory.Exists(GamePath))
                        {
                            MessageBox.Show("游戏目录不存在，请检查路径");
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
                                MessageBox.Show("未找到游戏");
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("game_data 项不存在");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"启动游戏时出错: {ex.Message}");
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
                            MessageBox.Show("找不到游戏位置，请选择在xml中输入游戏目录");
                        }
                        else if (!Directory.Exists(GamePath))
                        {
                            MessageBox.Show("游戏目录不存在，请检查路径");
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
                                MessageBox.Show("请输入账密");
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
                    else
                    {
                        MessageBox.Show("game_data 项不存在");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取配置文件时出错: {ex.Message}");
                }
            }
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
            }
        }
    }
    }

