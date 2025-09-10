using AdonisUI.Controls;
using HandyControl.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Button = System.Windows.Controls.Button;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;


namespace DMMDZZ_Game_Start
{
    /// <summary>
    /// 账号类
    /// 用于存储用户名和密码信息，支持序列化
    /// </summary>
    [Serializable]
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
    /// 账号编辑窗口类
    /// 负责账号的添加、编辑、删除和保存操作
    /// </summary>
    public partial class User_Edit : AdonisWindow
    {
        /// <summary>
        /// 账号列表的可观察集合
        /// </summary>
        public ObservableCollection<Account> Accounts { get; set; }
        
        /// <summary>
        /// 账号文件路径
        /// </summary>
        private const string AccountsFilePath = "accounts.json";

        /// <summary>
        /// 当前选中的账号
        /// </summary>
        private Account _selectedAccount;
        
        /// <summary>
        /// 窗口关闭事件
        /// 当账号编辑窗口关闭时触发
        /// </summary>
        public event EventHandler User_Edit_Close;
        
        /// <summary>
        /// 账号编辑窗口构造函数
        /// 初始化窗口组件并加载账号列表
        /// </summary>
        public User_Edit()
        {
            App.Log("账号编辑窗口初始化开始");
            InitializeComponent();
            Accounts = new ObservableCollection<Account>();
            lstAccounts.ItemsSource = Accounts;

            // 监听集合变更，更新按钮绑定
            Accounts.CollectionChanged += (s, e) =>
            {
                // 强制刷新ItemsControl的容器（触发按钮绑定）
                lstAccounts.Items.Refresh();
            };

            LoadAccounts();
            App.Log("账号编辑窗口初始化完成");
        }

        // 加载保存的账号数据
        private void LoadAccounts()
        {
            App.Log("开始加载账号数据: " + AccountsFilePath);
            try
            {
                if (File.Exists(AccountsFilePath))
                {
                    App.Log("账号文件存在，开始读取和解析");
                    string json = File.ReadAllText(AccountsFilePath);
                    var accounts = JsonSerializer.Deserialize<List<Account>>(json);

                    if (accounts != null)
                    {
                        App.Log($"成功解析账号文件，共找到 {accounts.Count} 个账号");
                        foreach (var account in accounts)
                        {
                            App.Log($"加载账号: {account.Username}");
                            Accounts.Add(account);
                        }
                    }
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
            finally
            {
                App.Log("账号加载操作完成");
            }
        }

        // 保存账号数据
        private void SaveAccounts()
        {
            App.Log("开始保存账号数据到: " + AccountsFilePath);
            try
            {
                string json = JsonSerializer.Serialize(Accounts);
                File.WriteAllText(AccountsFilePath, json);
                App.Log($"账号数据保存成功，共保存 {Accounts.Count} 个账号");
            }
            catch (Exception ex)
            {
                App.LogError("保存账号数据失败", ex);
                MessageBox.Show($"保存账号数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 保存按钮点击事件
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            App.Log("保存按钮点击，开始处理账号信息");
            // 获取输入的用户名和密码
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;


            // 验证输入
            if (string.IsNullOrEmpty(username))
            {
                App.Log("用户名验证失败: 用户名为空");
                //MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK);
                Growl.Info("请输入用户名");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                App.Log("密码验证失败: 密码为空");
                //MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK);
                Growl.Info("请输入密码");
                return;
            }

            // 检查用户名是否已存在（编辑模式除外）
            if (_selectedAccount == null && Accounts.Count > 0)
            {
                App.Log("检查用户名是否已存在: " + username);
                foreach (var account in Accounts)
                {
                    if (account.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        App.Log("用户名验证失败: 用户名已存在");
                        //MessageBox.Show("该用户名已存在，请使用其他用户名", "提示", MessageBoxButton.OK);
                        Growl.Info("该用户名已存在，请使用其他用户名");
                        return;
                    }
                }
            }

            // 判断是添加新账号还是编辑已有账号
            if (_selectedAccount != null)
            {
                App.Log($"编辑已有账号: 从 {_selectedAccount.Username} 更新为 {username}");
                // 编辑已有账号
                _selectedAccount.Username = username;
                _selectedAccount.Password = password;
                Growl.Success("账号已更新");
                lstAccounts.Items.Refresh();
                

            }
            else
            {
                App.Log($"添加新账号: {username}");
                // 添加新账号
                Accounts.Add(new Account { Username = username, Password = password });
                //MessageBox.Show("账号已添加", "成功", MessageBoxButton.OK);
                Growl.Success("帐号已添加");
            }

            // 保存账号数据
            SaveAccounts();
            App.Log("账号保存操作完成");

            // 清空输入框
            ClearInputs();
        }

        // 取消按钮点击事件
        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            App.Log("取消按钮点击，清除输入数据");
            ClearInputs();
            _selectedAccount = null;
            Growl.Success("已清除");
        }

        // 清空输入框
        private void ClearInputs()
        {
            App.Log("清空输入框和选中账号信息");
            txtUsername.Clear();
            txtPassword.Clear();
            _selectedAccount = null;
        }

        // 编辑按钮点击事件 - 修复为使用Tag属性
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            App.Log("编辑按钮点击，准备编辑账号信息");
            if (sender is Button button && button.Tag is Account account)
            {
                App.Log($"开始编辑账号: {account.Username}");
                txtUsername.Text = account.Username;
                txtPassword.Password = account.Password;
                _selectedAccount = account;
                App.Log("账号信息已加载到编辑界面");
            }
            else
            {
                App.Log("编辑操作失败: 无效的按钮或账号数据");
            }
        }

        // 删除按钮点击事件 - 修复为使用Tag属性
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            App.Log("删除按钮点击，准备删除账号信息");
            if (sender is Button button && button.Tag is Account account)
            {
                App.Log($"请求删除账号: {account.Username}");
                if (MessageBox.Show($"确定删除账号 '{account.Username}'？", "确认",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    App.Log($"用户确认删除账号: {account.Username}");
                    Accounts.Remove(account);
                    if (_selectedAccount == account)
                    {
                        App.Log("删除的是当前编辑的账号，清空输入框");
                        ClearInputs();
                    }
                    SaveAccounts();
                    Growl.Success("账号已删除");
                    App.Log($"账号删除成功: {account.Username}");
                }
                else
                {
                    App.Log("用户取消删除账号操作");
                }
            }
            else
            {
                App.Log("删除操作失败: 无效的按钮或账号数据");
            }
        }

        private void User_Edit_Closed(object sender, EventArgs e)
        {
            App.Log("账号编辑窗口关闭");
            User_Edit_Close?.Invoke(this, EventArgs.Empty);
        }
    }
}