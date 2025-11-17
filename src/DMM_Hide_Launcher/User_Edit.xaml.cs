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
using System.Windows.Data;
using Button = System.Windows.Controls.Button;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;
using System.Security.Cryptography;
using System.Text;
using System.Management;
using DMM_Hide_Launcher.Others;



namespace DMM_Hide_Launcher
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
    /// 导出账号数据包装类
    /// 用于存储账号列表和加密标记信息
    /// </summary>
    [Serializable]
    public class ExportAccountData
    {
        /// <summary>
        /// 指示密码是否使用AES加密
        /// </summary>
        public bool AES { get; set; }
        
        /// <summary>
        /// 账号列表
        /// </summary>
        public List<Account> Accounts { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ExportAccountData()
        {
            Accounts = new List<Account>();
        }
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
        /// 窗口激活时调用
        /// 设置当前窗口为Growl通知的父容器，实现只在激活窗口显示通知的功能
        /// 确保通知信息只在当前可见的窗口显示，避免UI挤压和重叠问题
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // 设置Growl通知的父容器为当前窗口的GrowlPanel，使通知在当前窗口显示
            Growl.SetGrowlParent(GrowlPanel_User_Edit, true);
        }
        
        /// <summary>
        /// 窗口失去焦点时调用
        /// 取消当前窗口作为Growl通知的父容器，实现只在激活窗口显示通知的功能
        /// </summary>
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            // 取消设置Growl通知的父容器，使通知不在当前非激活窗口显示
            Growl.SetGrowlParent(GrowlPanel_User_Edit, false);
        }
        
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
            App.Log("开始加载账号数据: 使用ConfigManager");
            try
            {
                // 清空当前账号列表
                Accounts.Clear();
                
                // 使用ConfigManager获取账号信息
                List<Others.Account> configAccounts = ConfigManager.GetAccounts();
                
                if (configAccounts != null && configAccounts.Count > 0)
                {
                    App.Log($"成功从ConfigManager获取 {configAccounts.Count} 个账号");
                    foreach (var configAccount in configAccounts)
                    {
                        // 解密密码
                        string decryptedPassword = CryptoHelper.DecryptString(configAccount.Password);
                        Account decryptedAccount = new Account
                        {
                            Username = configAccount.Username,
                            Password = decryptedPassword
                        };
                        App.Log($"加载账号: {decryptedAccount.Username}");
                        
                        // 添加到当前列表
                        Accounts.Add(decryptedAccount);
                    }
                }
                else
                {
                    App.Log("从ConfigManager未找到账号信息");
                    
                    // 如果没有账号信息，检查是否存在accounts.json文件，如果存在则导入
                    ImportFromOldAccountsFile();
                }
            }
            catch (Exception ex)
            {
                App.LogError("加载账号数据失败", ex);
                MessageBox.Show($"加载账号数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 从旧的accounts.json文件导入账号信息
        /// </summary>
        private void ImportFromOldAccountsFile()
        {
            try
            {
                if (File.Exists(AccountsFilePath))
                {
                    App.Log("检测到旧的accounts.json文件，开始导入账号信息");
                    string json = File.ReadAllText(AccountsFilePath);
                    var encryptedAccounts = JsonSerializer.Deserialize<List<Account>>(json);

                    if (encryptedAccounts != null && encryptedAccounts.Count > 0)
                    {
                        App.Log($"成功解析账号文件，共找到 {encryptedAccounts.Count} 个账号");
                        foreach (var encryptedAccount in encryptedAccounts)
                        {
                            // 解密密码
                            string decryptedPassword = CryptoHelper.DecryptString(encryptedAccount.Password);
                            Account decryptedAccount = new Account
                            {
                                Username = encryptedAccount.Username,
                                Password = decryptedPassword
                            };
                            App.Log($"加载账号: {decryptedAccount.Username}");
                            
                            // 添加到当前列表
                            Accounts.Add(decryptedAccount);
                            
                            // 保存到ConfigManager
                            ConfigManager.AddOrUpdateAccount(encryptedAccount.Username, encryptedAccount.Password);
                        }
                        
                        // 导入完成后删除旧文件
                        File.Delete(AccountsFilePath);
                        App.Log("已删除旧的accounts.json文件");
                    }
                }
                else
                {
                    App.Log("账号文件不存在，跳过加载");
                }
            }
            catch (Exception ex)
            {
                App.LogError("从旧文件导入账号数据失败", ex);
                // 这里不显示错误，因为这是一个可选操作
            }
        }
        
        // 保存账号数据
        private void SaveAccounts()
        {
            App.Log("开始保存账号数据到ConfigManager");
            try
            {
                // 先清空ConfigManager中的所有账号
                var configAccounts = ConfigManager.GetAccounts();
                foreach (var configAccount in configAccounts)
                {
                    ConfigManager.DeleteAccount(configAccount.Username);
                }
                
                // 然后重新添加所有账号
                foreach (var account in Accounts)
                {
                    // 加密密码
                    string encryptedPassword = CryptoHelper.EncryptString(account.Password);
                    
                    // 添加到ConfigManager
                    ConfigManager.AddOrUpdateAccount(account.Username, encryptedPassword);
                }
                
                App.Log($"账号数据保存成功，共保存 {Accounts.Count} 个账号到ConfigManager");
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
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
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
#pragma warning disable CA1416 // 验证平台兼容性
                Growl.Ask($"确定删除账号 '{account.Username}'？", isConfirmed =>
                {
                    if (isConfirmed)
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
                    return true;
                });
#pragma warning restore CA1416 // 验证平台兼容性
            }
            else
            {
                App.Log("删除操作失败: 无效的按钮或账号数据");
            }
        }

        // 导出密码按钮点击事件
        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            App.Log("导出密码按钮点击，开始处理导出操作");
            try
            {
                // 询问用户是否需要明文导出（用于跨设备）
                var exportOptionResult = MessageBox.Show("是否需要明文导出以支持跨设备导入？\n\n提示：明文导出将不加密密码，请注意保存安全。\n\n[是] = 明文导出 | [否] = 加密导出 | [取消] = 取消导出", 
                    "导出选项", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (exportOptionResult == MessageBoxResult.Cancel)
                {
                    App.Log("用户取消导出操作");
                    return;
                }

                bool isPlainTextExport = exportOptionResult == MessageBoxResult.Yes;

                // 创建保存文件对话框
                Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = isPlainTextExport ? "DMM未加密文件 (*.dmm)|*.dmm|所有文件 (*.*)|*.*" : "DMM加密文件 (*.dmm)|*.dmm|所有文件 (*.*)|*.*",
                    FilterIndex = 1,
                    RestoreDirectory = true,
                    FileName = isPlainTextExport ? "accounts_out.dmm" : "accounts_out_AES.dmm"
                };

                // 显示保存文件对话框
                bool? result = saveFileDialog.ShowDialog();

                // 如果用户点击了确定按钮
                if (result == true)
                {
                    string exportFilePath = saveFileDialog.FileName;
                    App.Log($"用户选择导出到文件: {exportFilePath}");
                    
                    if (isPlainTextExport)
                    {
                        ExportPlainTextAccounts(exportFilePath);
                    }
                    else
                    {
                        ExportAccounts(exportFilePath);
                    }
                    Growl.Success("账号导出成功");
                }
            }
            catch (Exception ex)
            {
                App.LogError("导出账号失败", ex);
                Growl.Error($"导出账号失败: {ex.Message}");
            }
        }

        // 拖放进入窗口事件
        private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            // 检查是否有文件被拖入
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                // 获取拖入的文件路径数组
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                
                // 检查是否包含.dmm文件
                if (files.Length == 1 && Path.GetExtension(files[0]).Equals(".dmm", StringComparison.OrdinalIgnoreCase))
                {
                    // 设置拖放效果为复制
                    e.Effects = System.Windows.DragDropEffects.Copy;
                }
                else
                {
                    // 不允许拖放
                    e.Effects = System.Windows.DragDropEffects.None;
                }
            }
            else
            {
                // 不允许拖放
                e.Effects = System.Windows.DragDropEffects.None;
            }
            
            // 标记事件已处理
            e.Handled = true;
        }
        
        // 拖放悬停窗口事件
        private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            // 复用DragEnter的逻辑
            Window_DragEnter(sender, e);
        }
        
        // 拖放完成事件
        private void Window_Drop(object sender, System.Windows.DragEventArgs e)
        {
            App.Log("检测到文件拖放操作");
            
            try
            {
                // 检查是否有文件被拖入
                if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    // 获取拖入的文件路径数组
                    string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                    
                    // 处理单个.dmm文件
                    if (files.Length == 1 && Path.GetExtension(files[0]).Equals(".dmm", StringComparison.OrdinalIgnoreCase))
                    {
                        string importFilePath = files[0];
                        App.Log($"用户通过拖放导入文件: {importFilePath}");
                        
                        // 询问用户是否进行混合覆盖导入
                        if (Accounts.Count > 0)
                        {
                            if (MessageBox.Show("当前已有账号，导入将自动进行混合覆盖（同名账号更新密码，新账号添加），是否继续？", "确认导入", 
                                MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                            {
                                App.Log("用户取消拖放导入操作");
                                return;
                            }
                        }
                        
                        // 导入账号数据并获取成功导入的账号数量
                        int successCount = ImportAccounts(importFilePath);
                        
                        if (successCount > 0)
                        {
                            Growl.Success($"账号导入成功，共处理 {successCount} 个账号");
                        }
                        else
                        {
                            Growl.Warning("导入完成，但未成功处理任何账号");
                        }
                    }
                    else
                    {
                        Growl.Warning("请只拖入单个.dmm文件");
                        App.Log("拖放操作失败: 请只拖入单个.dmm文件");
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("拖放导入账号失败", ex);
                Growl.Error($"导入失败: {ex.Message}");
            }
        }
        
        // 导入密码按钮点击事件
        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            App.Log("导入密码按钮点击，开始处理导入操作");
            try
            {
                // 创建打开文件对话框
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "DMM账号文件 (*.dmm)|*.dmm|所有文件 (*.*)|*.*",
                    FilterIndex = 1,
                    RestoreDirectory = true
                };

                // 显示打开文件对话框
                bool? result = openFileDialog.ShowDialog();

                // 如果用户点击了确定按钮
                if (result == true)
                {
                    string importFilePath = openFileDialog.FileName;
                    App.Log($"用户选择从文件导入: {importFilePath}");
                    
                    // 询问用户是否进行混合覆盖导入
                    if (Accounts.Count > 0)
                    {
                        if (MessageBox.Show("当前已有账号，导入将自动进行混合覆盖（同名账号更新密码，新账号添加），是否继续？", "确认导入", 
                            MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        {
                            App.Log("用户取消导入操作");
                            return;
                        }
                    }
                    
                    // 导入账号数据并获取成功导入的账号数量
                    int successCount = ImportAccounts(importFilePath);
                    
                    if (successCount > 0)
                    {
                        Growl.Success($"账号导入成功，共处理 {successCount} 个账号");
                    }
                    else
                    {
                        Growl.Warning("导入完成，但未成功处理任何账号");
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("导入账号失败", ex);
                Growl.Error($"导入账号失败: {ex.Message}");
            }
        }

        // 导出账号数据到文件（加密方式，设备绑定）
        private void ExportAccounts(string filePath)
        {
            App.Log("开始导出账号数据（加密方式）");
            try
            {
                // 创建导出数据包装类
                ExportAccountData exportData = new ExportAccountData();
                exportData.AES = true; // 标记为AES加密
                
                // 创建加密后的账号列表
                foreach (var account in Accounts)
                {
                    // 加密密码
                    string encryptedPassword = CryptoHelper.EncryptString(account.Password);
                    exportData.Accounts.Add(new Account
                    {
                        Username = account.Username,
                        Password = encryptedPassword
                    });
                }

                // 序列化导出数据为JSON
                string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                
                // 写入文件
                File.WriteAllText(filePath, json);
                App.Log($"账号导出成功（加密方式），共导出 {exportData.Accounts.Count} 个账号，AES标记已设置为true");
            }
            catch (Exception ex)
            {
                App.LogError("导出账号数据失败", ex);
                throw;
            }
        }

        // 导出账号数据到文件（明文方式，支持跨设备）
        private void ExportPlainTextAccounts(string filePath)
        {
            App.Log("开始导出账号数据（明文方式）");
            try
            {
                // 创建导出数据包装类
                ExportAccountData exportData = new ExportAccountData();
                exportData.AES = false; // 标记为非AES加密（明文）
                
                // 创建包含明文密码的账号列表
                foreach (var account in Accounts)
                {
                    exportData.Accounts.Add(new Account
                    {
                        Username = account.Username,
                        Password = account.Password // 明文密码，不加密
                    });
                }

                // 序列化导出数据为JSON
                string json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
                
                // 写入文件
                File.WriteAllText(filePath, json);
                App.Log($"账号导出成功（明文方式），共导出 {exportData.Accounts.Count} 个账号，AES标记已设置为false");
            }
            catch (Exception ex)
            {
                App.LogError("导出账号数据失败（明文方式）", ex);
                throw;
            }
        }

        // 导入账号数据（混合覆盖模式）
        private int ImportAccounts(string filePath)
        {
            App.Log("开始导入账号数据");
            try
            {
                // 读取文件内容
                string json = File.ReadAllText(filePath);
                
                // 尝试解析为ExportAccountData格式（带AES标记的格式）
                ExportAccountData importData = JsonSerializer.Deserialize<ExportAccountData>(json);
                
                if (importData == null || importData.Accounts == null || importData.Accounts.Count == 0)
                {
                    App.LogWarning("导入的账号数据为空或格式不正确");
                    return 0;
                }
                
                App.Log($"成功解析为带AES标记的数据格式，AES加密状态: {importData.AES}");
                
                // 混合覆盖：更新或新增账号
                int updatedCount = 0;
                int addedCount = 0;
                
                foreach (var importedAccount in importData.Accounts)
                {
                    // 查找本地是否存在相同用户名的账号
                    var existingAccount = Accounts.FirstOrDefault(a => a.Username == importedAccount.Username);
                    
                    // 处理密码
                    string passwordToUse = importedAccount.Password;
                    
                    if (importData.AES)
                    {
                        // 如果数据已加密（根据AES标记），尝试解密密码
                        try
                        {
                            passwordToUse = CryptoHelper.DecryptString(importedAccount.Password);
                            App.Log($"成功解密账号 '{importedAccount.Username}' 的密码");
                        }
                        catch (Exception ex)
                        {
                            App.LogError($"解密账号 '{importedAccount.Username}' 的密码失败，可能是在不同设备上导出的加密数据", ex);
                            // 对于单个账号解密失败，跳过该账号
                            continue;
                        }
                    }
                    else
                    {
                        App.Log($"账号 '{importedAccount.Username}' 为明文数据，无需解密");
                    }
                    
                    if (existingAccount != null)
                    {
                        // 如果存在相同用户名的账号，更新密码
                        existingAccount.Password = passwordToUse;
                        updatedCount++;
                        App.Log($"已更新账号 '{importedAccount.Username}' 的密码");
                    }
                    else
                    {
                        // 如果不存在相同用户名的账号，添加新账号
                        Accounts.Add(new Account
                        {
                            Username = importedAccount.Username,
                            Password = passwordToUse
                        });
                        addedCount++;
                        App.Log($"已添加新账号 '{importedAccount.Username}'");
                    }
                }
                
                // 保存到本地文件
                SaveAccounts();
                App.Log($"账号导入完成（混合覆盖模式），成功更新 {updatedCount} 个账号，新增 {addedCount} 个账号，AES标记状态: {importData.AES}");
                return updatedCount + addedCount;
            }
            catch (Exception ex)
            {
                App.LogError("导入账号数据失败", ex);
                throw;
            }
        }

        private void User_Edit_Closed(object sender, EventArgs e)
        {
            App.Log("账号编辑窗口关闭");
            // 窗口关闭时触发自定义事件
            User_Edit_Close?.Invoke(this, EventArgs.Empty);
        }
    }
}