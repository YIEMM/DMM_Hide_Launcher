using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Navigation;
using AdonisUI.Controls;
using HandyControl.Controls;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// WebBrowser Cookie获取相关的Win32 API
    /// </summary>
    public static class Win32CookieAPI
    {
        [DllImport("wininet.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool InternetGetCookieEx(
            string lpszURL,
            string lpszCookieName,
             StringBuilder lpszCookieData,
            ref int lpdwBufferLength,
            int dwFlags,
            IntPtr lpReserved);

        public const int INTERNET_COOKIE_HTTPONLY = 0x00002000;
        public const int ERROR_SUCCESS = 0;
    }

    /// <summary>
    /// QQ登录窗口
    /// </summary>
    public partial class QQLoginWindow : AdonisWindow
    {


        /// <summary>
        /// 用户信息类，用于存储从Cookie提取的关键信息
        /// </summary>
        public class UserInfo
        {
            public string ServerId { get; set; } = "";
            public string Timekey { get; set; } = "";
            public string Username { get; set; } = "";
            public string UserId { get; set; } = "";
            public string Time { get; set; } = "";
            public string Securitycode { get; set; } = "";
        }


        /// <summary>
        /// WebBrowser使用Edge内核的注册表值
        /// </summary>
        private const int BROWSER_EMULATION_VALUE = 898989545;

        /// <summary>
        /// 关闭窗口前的延迟时间（毫秒）
        /// </summary>
        private const int DEFAULT_CLOSE_DELAY = 1;

        /// <summary>
        /// 默认Cookie缓冲区大小
        /// </summary>
        private const int DEFAULT_COOKIE_BUFFER_SIZE = 8192;
        /// <summary>
        /// 7K7K QQ登录授权URL
        /// </summary>
        private const string QQ_LOGIN_URL = "http://8.7k7k.com/Connect2.1/example/oauth/index.php?referer=http://web.7k7k.com/games/tpbsn/dlq";
        /// <summary>
        /// 7K7K 微信登录授权URL
        /// </summary>
        private const string WECHAT_LOGIN_URL = "https://open.weixin.qq.com/connect/qrconnect?response_type=code&appid=wx4b2ea8fbad86e262&redirect_uri=http://zc.7k7k.com/Wx/oauth/callback.php&state=73cdedfbb78f07edb3950b62ed58cd1e%2526referer%253Dhttp%253A%252F%252Fweb.7k7k.com%252Fapi%252Fwxlogin_wd.php%253Faid%253D17923610%2526refer%253D//web.7k7k.com/games/tpbsn/dlq/%253Fthird%253D1%2523bottom&scope=snsapi_login,snsapi_userinfo";
        /// <summary>
        /// 7K7K参数请求链接
        /// </summary>
        private const string GAME_CORE_URL = "https://web.7k7k.com/games/dlq/core_togame.php";

        /// <summary>
        /// 目标游戏页面URL片段
        /// </summary>
        private const string TARGET_GAME_URL = "web.7k7k.com/games/tpbsn/dlq/";

        // 全局变量，用于存储从Cookie提取的信息（保持向后兼容）
        public static string serverId = "";
        public static string timekey = "";
        public static string username = "";
        public static string userid = "";
        public static string GamePath = "";
        public static string time = "";
        public static string securitycode = "";

        /// <summary>
        /// 7KQQ登录密钥
        /// </summary>
        public static string Login_KEY_7KQQ { get; private set; } = "";

        /// <summary>
        /// 用户是否主动关闭登录窗口
        /// </summary>
        public static bool UserClosed { get; private set; } = false;


        /// <summary>
        /// 修改注册表信息使WebBrowser使用Edge内核
        /// </summary>
        public static void SetEdgeKernel()
        {
            try
            {
                // 获取程序文件名称
                string appName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
                string featureControlRegKey = "HKEY_CURRENT_USER\\Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl\\";
                
                // 设置浏览器对应用程序以Edge模式运行
                Registry.SetValue(featureControlRegKey + "FEATURE_BROWSER_EMULATION", appName, BROWSER_EMULATION_VALUE, RegistryValueKind.DWord);
                
                // 启用子元素裁剪优化，提高渲染性能
                Registry.SetValue(featureControlRegKey + "FEATURE_ENABLE_CLIPCHILDREN_OPTIMIZATION", appName, 1, RegistryValueKind.DWord);
                
                App.Log($"已设置WebBrowser使用Edge内核: {appName}");
            }
            catch (Exception ex)
            {
                App.LogError("设置WebBrowser内核时出错", ex);
            }
        }


        public QQLoginWindow()
        {
            InitializeComponent();
            Loaded += QQLoginWindow_Loaded;
            webBrowser.Visibility = Visibility.Collapsed;
            Login_KEY_7KQQ = "";
            UserClosed = false;
        }

        private void QQLoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            App.Log("第三方登录窗口已加载");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            App.Log("用户点击关闭按钮");
            UserClosed = true;
            Growl.Info("已关闭登录窗口");
            Close();
        }

        private void BtnQQLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("用户点击QQ登录按钮");
                SetEdgeKernel();
                webBrowser.Navigate(QQ_LOGIN_URL);
                App.Log("QQ登录页面已加载");
                webBrowser.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                App.LogError("加载QQ登录窗口时出错", ex);
                System.Windows.MessageBox.Show($"加载QQ登录窗口时出错: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void BtnWeChatLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("用户点击微信登录按钮");
                SetEdgeKernel();
                webBrowser.Navigate(WECHAT_LOGIN_URL);
                App.Log("微信登录页面已加载");
                webBrowser.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                App.LogError("加载微信登录窗口时出错", ex);
                System.Windows.MessageBox.Show($"加载微信登录窗口时出错: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void BtnLastLogin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                App.Log("用户点击上一次登录按钮");
                webBrowser.Visibility = Visibility.Collapsed;
                SetEdgeKernel();
                webBrowser.Navigate("https://web.7k7k.com/games/tpbsn/dlq/");
                App.Log("游戏页面已加载");
            }
            catch (Exception ex)
            {
                App.LogError("加载游戏页面时出错", ex);
                System.Windows.MessageBox.Show($"加载游戏页面时出错: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 发送POST请求到7K7k服务器
        /// </summary>
        /// <param name="serverId">服务器ID</param>
        /// <param name="timekey">时间密钥</param>
        /// <param name="username">用户名</param>
        /// <returns>响应内容</returns>
        private async Task<string> SendGameRequestAsync(string serverId, string timekey, string username)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // 设置请求头
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36 Edg/135.0.0.0");
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                    client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                    client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN, zh; q=0.9");
                    client.DefaultRequestHeaders.Add("Origin", "https://web.7k7k.com");
                    client.DefaultRequestHeaders.Add("Referer", "https://web.7k7k.com/games/tpbsn/dlq/");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "empty");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "cors");
                    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "Windows");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Microsoft Edge\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135\"");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("priority", "u=1, i");

                    // 构建Cookie字符串
                    string cookie = $"SERVER_ID={serverId}; timekey={timekey}; username={username}; identity={username}; nickname=114514; userid={userid}; kk=114514; logintime={time}; k7_lastlogin=114514; loginfrom=0011; avatar=http%3A%2F%2Fsface.7k7kimg.cn%2Fuicons%2Fphoto_default_s.png; securitycode={securitycode}; k7_lastlogin=1970-01-01+23%3A59%3A59; k7_union=9999999; k7_username={username}; k7_uid=none; k7_from=17944284; k7_reg=1727098013; k7_ip=10.19.84.36; userprotect=a3f8773d6255165006eb2d16583732e2; userpermission=31f2d13b9ada1dad754295935f8236a7; k7_lastloginip=114.114.114.114";
                    client.DefaultRequestHeaders.Add("Cookie", cookie);

                    // 构建POST数据
                    var postData = new StringContent("gid=780&sid=1", Encoding.UTF8, "application/x-www-form-urlencoded");
                    
                    // 发送POST请求
                    var response = await client.PostAsync(GAME_CORE_URL, postData);
                    response.EnsureSuccessStatusCode();
                    
                    // 读取并处理响应内容（支持gzip压缩）
                    string responseContent;
                    var content = response.Content;
                    var contentEncoding = response.Content.Headers.ContentEncoding;
                    
                    if (contentEncoding.Contains("gzip"))
                    {
                        // 处理gzip压缩响应
                        using (var stream = await content.ReadAsStreamAsync())
                        using (var gzipStream = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionMode.Decompress))
                        using (var reader = new StreamReader(gzipStream, Encoding.UTF8))
                        {
                            responseContent = reader.ReadToEnd();
                        }
                    }
                    else if (contentEncoding.Contains("deflate"))
                    {
                        // 处理deflate压缩响应
                        using (var stream = await content.ReadAsStreamAsync())
                        using (var deflateStream = new System.IO.Compression.DeflateStream(stream, System.IO.Compression.CompressionMode.Decompress))
                        using (var reader = new StreamReader(deflateStream, Encoding.UTF8))
                        {
                            responseContent = reader.ReadToEnd();
                        }
                    }
                    else
                    {
                        // 直接读取未压缩响应
                        responseContent = await response.Content.ReadAsStringAsync();
                    }
                    
                    App.Log($"POST请求成功，原始响应内容: {responseContent}");
                    
                    // 解析响应内容
                    string parsedResponse = ParseGameResponse(responseContent);
                    App.Log($"解析后的响应内容: {parsedResponse}");
                    
                    return parsedResponse;
                }
            }
            catch (Exception ex)
            {
                App.LogError("发送POST请求时出错", ex);
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// 从Cookie字符串中提取关键信息
        /// </summary>
        /// <param name="cookieString">Cookie字符串</param>
        /// <returns>提取的用户信息对象</returns>
        private UserInfo ExtractUserInfoFromCookie(string cookieString)
        {
            App.Log("开始解析Cookie");
            
            // 空值检查
            if (string.IsNullOrWhiteSpace(cookieString))
            {
                App.LogError("Cookie字符串为空");
                return new UserInfo();
            }
            
            try
            {
                // 创建字典存储cookie键值对（不区分大小写）
                Dictionary<string, string> cookieDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                // 分割Cookie并填充字典
                foreach (string pair in cookieString.Split(';'))
                {
                    string trimmedPair = pair.Trim();
                    if (trimmedPair.Contains("="))
                    {
                        int equalsIndex = trimmedPair.IndexOf('=');
                        if (equalsIndex > 0 && equalsIndex < trimmedPair.Length - 1)
                        {
                            string cookieName = trimmedPair.Substring(0, equalsIndex).Trim();
                            string cookieValue = trimmedPair.Substring(equalsIndex + 1).Trim();
                            
                            // 只添加非空值
                            if (!string.IsNullOrEmpty(cookieName) && !cookieDict.ContainsKey(cookieName))
                            {
                                cookieDict.Add(cookieName, cookieValue);
                            }
                        }
                    }
                }
                
                // 创建用户信息对象
                UserInfo userInfo = new UserInfo();
                
                // 提取关键信息
                string tempValue;
                
                cookieDict.TryGetValue("SERVER_ID", out tempValue);
                userInfo.ServerId = tempValue;
                
                cookieDict.TryGetValue("timekey", out tempValue);
                userInfo.Timekey = tempValue;
                
                cookieDict.TryGetValue("username", out tempValue);
                userInfo.Username = tempValue;
                
                cookieDict.TryGetValue("userid", out tempValue);
                userInfo.UserId = tempValue;
                
                cookieDict.TryGetValue("logintime", out tempValue);
                userInfo.Time = tempValue;
                
                cookieDict.TryGetValue("securitycode", out tempValue);
                userInfo.Securitycode = tempValue;
                
                // 从identity中获取username（如果username为空）
                if (string.IsNullOrEmpty(userInfo.Username))
                {
                    cookieDict.TryGetValue("identity", out tempValue);
                    userInfo.Username = tempValue;
                }
                
                // 更新全局变量（保持向后兼容）
                serverId = userInfo.ServerId;
                timekey = userInfo.Timekey;
                username = userInfo.Username;
                userid = userInfo.UserId;
                time = userInfo.Time;
                securitycode = userInfo.Securitycode;
                
                App.Log("Cookie解析完成");
                return userInfo;
            }
            catch (Exception ex)
            {
                App.LogError("解析Cookie时出错", ex);
                return new UserInfo();
            }
        }

        /// <summary>
        /// 解析游戏服务器响应内容
        /// </summary>
        /// <param name="responseContent">响应内容（JSON格式）</param>
        /// <returns>格式化后的字符串</returns>
        private string ParseGameResponse(string responseContent)
        {
            try
            {
                App.Log("开始解析7K方响应");
                
                // 解析JSON响应
                JObject jsonObj = JObject.Parse(responseContent);
                
                // 检查是否有status字段且为1（成功）
                if (jsonObj.TryGetValue("status", out JToken statusToken) && statusToken.Type == JTokenType.Integer && (int)statusToken == 1)
                {
                    // 获取url字段
                    if (jsonObj.TryGetValue("url", out JToken urlToken) && urlToken.Type == JTokenType.String)
                    {
                        string url = (string)urlToken;
                        App.Log("获取到游戏URL");
                        
                        // 解析URL中的查询参数
                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                        {
                            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
                            
                            // 验证必需参数
                            string userid = queryParams["userid"];
                            if (string.IsNullOrEmpty(userid))
                            {
                                App.LogError("响应URL中缺少必需的userid参数");
                                return "ERROR:MISSING_USERID";
                            }
                            
                            // 使用StringBuilder高效构建Key参数
                            StringBuilder keyBuilder = new StringBuilder();
                            foreach (string paramName in queryParams.AllKeys)
                            {
                                if (!string.IsNullOrEmpty(paramName))
                                {
                                    string paramValue = queryParams[paramName] ?? "";
                                    if (keyBuilder.Length > 0)
                                    {
                                        keyBuilder.Append("&");
                                    }
                                    keyBuilder.Append($"{paramName}-{paramValue}");
                                }
                            }
                            string key = keyBuilder.ToString();
                            
                            // 构建最终格式
                            string result = $"ID={userid},Key={key},PID=7K7K_,PROCPARA=66666,Channel=PC7K7K";
                            
                            App.Log("游戏响应解析成功");
                            return result;
                        }
                        else
                        {
                            App.LogError("无效的游戏URL格式");
                            return "ERROR:INVALID_URL";
                        }
                    }
                    else
                    {
                        App.LogError("响应中缺少有效的url字段");
                        return "ERROR:MISSING_URL";
                    }
                }
                else
                {
                    // 检查是否有错误信息
                    string errorMsg = "请求失败";
                    if (jsonObj.TryGetValue("msg", out JToken msgToken) && msgToken.Type == JTokenType.String)
                    {
                        errorMsg = (string)msgToken;
                    }
                    
                    // 检查是否为未实名提示
                    if (jsonObj.TryGetValue("url", out JToken urlToken) && urlToken.Type == JTokenType.String)
                    {
                        string url = (string)urlToken;
                        if (url == "\u672a\u5b9e\u540d" || url.Contains("未实名"))
                        {
                            App.Log("检测到未实名提示");
                            Growl.Warning("未实名认证，请先完成实名认证");
                            return "ERROR:NOT_REALNAME";
                        }
                    }
                    
                    App.LogError($"游戏请求失败: {errorMsg}");
                    return $"ERROR:REQUEST_FAILED-{errorMsg}";
                }
            }
            catch (JsonReaderException ex)
            {
                App.LogError("JSON格式解析错误", ex);
                return "ERROR:JSON_PARSE_ERROR";
            }
            catch (UriFormatException ex)
            {
                App.LogError("URL格式解析错误", ex);
                return "ERROR:URL_PARSE_ERROR";
            }
            catch (Exception ex)
            {
                App.LogError("解析响应内容时发生未知错误", ex);
                return "ERROR:UNKNOWN_ERROR";
            }
        }

        /// <summary>
        /// 获取指定URL的Cookie
        /// </summary>
        /// <param name="url">目标URL</param>
        /// <returns>Cookie字符串</returns>
        private string GetCookieForUrl(string url)
        {
            try
            {
                int bufferSize = DEFAULT_COOKIE_BUFFER_SIZE;
                StringBuilder cookieData = new StringBuilder(bufferSize);
                
                if (Win32CookieAPI.InternetGetCookieEx(
                    url, 
                    null, 
                    cookieData, 
                    ref bufferSize, 
                    Win32CookieAPI.INTERNET_COOKIE_HTTPONLY, 
                    IntPtr.Zero))
                {
                    string cookies = cookieData.ToString();
                    
                    // 对Cookie进行两遍URL解码
                    string decodedCookies = DecodeCookie(cookies);
                    
                    App.Log($"原始Cookie (URL编码): {cookies}");
                    App.Log($"第一次解码后: {System.Web.HttpUtility.UrlDecode(cookies)}");
                    App.Log($"第二次解码后 (最终结果): {decodedCookies}");
                    App.Log($"成功获取URL [{url}] 的Cookie: {decodedCookies}");
                    return decodedCookies;
                }
                else
                {
                    App.Log($"无法获取URL [{url}] 的Cookie，错误代码: {Marshal.GetLastWin32Error()}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                App.LogError($"获取Cookie时出错 (URL: {url})", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 解码Cookie字符串（进行两遍URL解码）
        /// </summary>
        /// <param name="cookie">编码后的Cookie字符串</param>
        /// <returns>解码后的Cookie字符串</returns>
        private string DecodeCookie(string cookie)
        {
            string decodedCookie = System.Web.HttpUtility.UrlDecode(cookie);
            return System.Web.HttpUtility.UrlDecode(decodedCookie);
        }

        /// <summary>
        /// 在后台发送游戏请求
        /// </summary>
        private async Task SendGameRequestInBackground()
        {
            try
            {
                // 发送POST请求到游戏服务器
                string loginKey = await SendGameRequestAsync(serverId, timekey, username);
                Login_KEY_7KQQ = loginKey;
                App.Log("后台POST请求完成");
            }
            catch (Exception ex)
            {
                App.LogError("后台发送POST请求时出错", ex);
            }
        }

        private void webBrowser_Navigated(object sender, NavigationEventArgs e)
        {
            // WebBrowser导航完成后的处理
            try
            {
                string currentUrl = e.Uri.ToString();
                App.Log($"WebBrowser已导航到: {currentUrl}");
                
                // 排除登录授权页面，避免误判
                if (currentUrl.Contains("open.weixin.qq.com") || currentUrl.Contains("8.7k7k.com/Connect2.1") || currentUrl.Contains("zc.7k7k.com/Wx/oauth"))
                {
                    return;
                }
                
                // 实时监测特定网址并输出Cookie（支持HTTP和HTTPS）
                if (currentUrl.Contains(TARGET_GAME_URL))
                {
                    App.Log("检测到目标游戏页面！正在获取Cookie...");
                    
                    // 获取该页面的Cookie
                    string cookies = GetCookieForUrl(currentUrl);
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        // 输出Cookie信息
                        App.Log($"成功获取游戏页面Cookie: {cookies}");
                        
                        // 解析Cookie提取关键信息
                        ExtractUserInfoFromCookie(cookies);
                        
                        // 在后台发送POST请求，不阻塞窗口关闭
                        _ = SendGameRequestInBackground();
                        
                        // 立即关闭窗口，不等待POST请求完成
                        App.Log("QQ登录窗口已关闭");
                        this.Close();
                    }
                    else
                    {
                        App.Log("未能获取到有效的Cookie，可能该页面未设置Cookie");
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("记录WebBrowser导航信息时出错", ex);
            }
        }
    }
}
