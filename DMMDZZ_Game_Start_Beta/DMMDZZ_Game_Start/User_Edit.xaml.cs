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
    [Serializable]
    public class Account
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public partial class User_Edit : AdonisWindow
    {
        public ObservableCollection<Account> Accounts { get; set; }
        private const string AccountsFilePath = "accounts.json";

        // 当前选中的账号
        private Account _selectedAccount;
        //定义事件
        public event EventHandler User_Edit_Close;
        public User_Edit()
        {
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
        }

        // 加载保存的账号数据
        private void LoadAccounts()
        {
            try
            {
                if (File.Exists(AccountsFilePath))
                {
                    string json = File.ReadAllText(AccountsFilePath);
                    var accounts = JsonSerializer.Deserialize<List<Account>>(json);

                    if (accounts != null)
                    {
                        foreach (var account in accounts)
                        {
                            Accounts.Add(account);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载账号数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 保存账号数据
        private void SaveAccounts()
        {
            try
            {
                string json = JsonSerializer.Serialize(Accounts);
                File.WriteAllText(AccountsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存账号数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 保存按钮点击事件
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 获取输入的用户名和密码
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;


            // 验证输入
            if (string.IsNullOrEmpty(username))
            {
                //MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK);
                Growl.Info("请输入用户名");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                //MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK);
                Growl.Info("请输入密码");
                return;
            }

            // 检查用户名是否已存在（编辑模式除外）
            if (_selectedAccount == null && Accounts.Count > 0)
            {
                foreach (var account in Accounts)
                {
                    if (account.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                    {
                        //MessageBox.Show("该用户名已存在，请使用其他用户名", "提示", MessageBoxButton.OK);
                        Growl.Info("该用户名已存在，请使用其他用户名");
                        return;
                    }
                }
            }

            // 判断是添加新账号还是编辑已有账号
            if (_selectedAccount != null)
            {
                // 编辑已有账号
                _selectedAccount.Username = username;
                _selectedAccount.Password = password;
                Growl.Success("账号已更新");
                lstAccounts.Items.Refresh();
                

            }
            else
            {
                // 添加新账号
                Accounts.Add(new Account { Username = username, Password = password });
                //MessageBox.Show("账号已添加", "成功", MessageBoxButton.OK);
                Growl.Success("帐号已添加");
            }

            // 保存账号数据
            SaveAccounts();

            // 清空输入框
            ClearInputs();
        }

        // 取消按钮点击事件
        private async void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ClearInputs();
            _selectedAccount = null;
            Growl.Success("已清除");
        }

        // 清空输入框
        private void ClearInputs()
        {
            txtUsername.Clear();
            txtPassword.Clear();
            _selectedAccount = null;
        }

        // 编辑按钮点击事件 - 修复为使用Tag属性
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Account account)
            {
                txtUsername.Text = account.Username;
                txtPassword.Password = account.Password;
                _selectedAccount = account;
            }
        }

        // 删除按钮点击事件 - 修复为使用Tag属性
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Account account)
            {
                if (MessageBox.Show($"确定删除账号 '{account.Username}'？", "确认",
                    MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Accounts.Remove(account);
                    if (_selectedAccount == account) ClearInputs();
                    SaveAccounts();
                    Growl.Success("账号已删除");
                }
            }
        }

        private void User_Edit_Closed(object sender, EventArgs e)
        {
            User_Edit_Close?.Invoke(this, EventArgs.Empty);
        }
    }
}