using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Linq;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using Application = System.Windows.Application;
using AdonisUI;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// QQ登录处理器类
    /// 负责处理7K7K平台的QQ登录流程
    /// </summary>
    public class QQLoginHandler
    {
        // QQ登录URL - 根据流程图
        private static readonly string QQ_LOGIN_URL = "https://8.7k7k.com/Connect2.1/example/oauth/index.php?referer=http://web.7k7k.com/api/gwlogin.php?aid=17923609&refer=//web.7k7k.com/games/tpbsn/dlq/";
        
        // 存储获取到的cookie
        private Dictionary<string, string> _browserCookies = new Dictionary<string, string>();
        private bool _loginCompleted = false;
        private string _errorMessage = string.Empty;
        private string _loginParams = string.Empty;
        private Window _loginWindow;
        private WebView2 _webView;
        private Thread _browserThread;

        /// <summary>
        /// 运行QQ登录流程
        /// </summary>
        public async Task<string> RunQQLoginProcess()
        {
            App.Log("开始执行QQ登录流程");
            try
            {
                // 调用原始的登录流程方法
                return await RunQQLoginProcessInternal();
            }
            catch (Exception ex)
            {
                // 记录异常并返回错误信息
                App.LogError("QQ登录流程异常", ex);
                return "ERROR:QQ登录过程发生异常: " + ex.Message;
            }
        }
        
        /// <summary>
        /// 内部登录流程方法 - 包含实际的登录流程实现
        /// </summary>
        private async Task<string> RunQQLoginProcessInternal()
        {
            // 创建线程安全的状态变量
            var browserCookies = new Dictionary<string, string>();
            var loginCompleted = new System.Threading.ManualResetEvent(false);
            string errorMessage = string.Empty;

            try
            {
                App.Log("开始执行QQ登录流程");
                
                // 确保在UI线程上执行
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // 创建登录窗口
                        var loginWindow = new Window
                        {
                            Title = "QQ登录 - 7K7K逃跑吧少年",
                            Width = 800,
                            Height = 600,
                            WindowStartupLocation = WindowStartupLocation.CenterScreen,
                            ResizeMode = ResizeMode.NoResize
                        };

                        // 创建WebView2控件
                        var webView = new WebView2();
                        
                        // 尝试设置WebView2的初始URL，这可能有助于初始化
                        webView.Source = new Uri(QQ_LOGIN_URL);
                        App.Log($"已设置WebView2初始URL: {QQ_LOGIN_URL}");

                        // 设置WebView2的初始化完成事件
                        webView.CoreWebView2InitializationCompleted += async (sender, e) =>
                        {
                            try
                            {
                                App.Log("WebView2初始化完成事件触发");
                                
                                if (e.IsSuccess)
                                {
                                    App.Log("WebView2初始化成功");
                                    
                                    // 注册导航完成事件
                                    webView.CoreWebView2.NavigationCompleted += async (navSender, navE) =>
                                    {
                                        try
                                        {
                                            if (!(navSender is CoreWebView2 navWebView))
                                                return;
                                            
                                            // 记录导航信息用于调试
                                            Debug.WriteLine($"导航完成: {navWebView.Source}, 成功: {navE.IsSuccess}");
                                            App.Log($"导航完成: {navWebView.Source}, 成功: {navE.IsSuccess}");
                                            
                                            // 检查导航是否成功
                                            if (!navE.IsSuccess)
                                            {
                                                Debug.WriteLine($"导航失败，错误码: {navE.WebErrorStatus}");
                                                App.Log($"导航失败，错误码: {navE.WebErrorStatus}");
                                                return;
                                            }
                                            
                                            // 更新所有Cookie，无论当前URL是什么
                                            await UpdateAllCookiesToDictionary(navWebView.CookieManager, browserCookies);
                                            
                                            // 记录当前所有Cookie
                                            StringBuilder cookieLog = new StringBuilder("当前Cookie列表: ");
                                            foreach (var cookie in browserCookies)
                                            {
                                                cookieLog.Append($"{cookie.Key}={cookie.Value}, ");
                                            }
                                            App.Log(cookieLog.ToString().TrimEnd(',', ' '));
                                            
                                            // 检查URL是否是目标页面
                                            string url = navWebView.Source.ToString();
                                            App.Log($"当前URL: {url}");
                                            App.Log($"检查URL是否包含目标字符串...");
                                            App.Log($"包含web.7k7k.com/games/tpbsn/dlq: {url.Contains("web.7k7k.com/games/tpbsn/dlq")}");
                                            App.Log($"包含gwlogin.php: {url.Contains("gwlogin.php")}");
                                            App.Log($"包含userinfo.html: {url.Contains("userinfo.html")}");
                                            
                                            // 检查是否已经获取到必要的cookie，无论当前URL是什么
                                            if (browserCookies.ContainsKey("SERVER_ID") && browserCookies.ContainsKey("timekey"))
                                            {
                                                App.Log("已获取到必要的Cookie信息，准备关闭窗口");
                                                
                                                // 关闭窗口
                                                webView.Stop();
                                                loginWindow.Close();
                                            }
                                            else
                                            {
                                                // 记录缺失的Cookie信息
                                                StringBuilder missingCookies = new StringBuilder();
                                                if (!browserCookies.ContainsKey("SERVER_ID"))
                                                    missingCookies.Append("SERVER_ID, ");
                                                if (!browserCookies.ContainsKey("timekey"))
                                                    missingCookies.Append("timekey");
                                                
                                                App.Log($"尚未获取到必要的Cookie: {missingCookies.ToString().TrimEnd(',', ' ')}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            App.LogError("导航完成事件异常", ex);
                                            Debug.WriteLine($"导航完成事件异常: {ex.Message}");
                                        }
                                    };
                                    
                                    // 初始获取所有Cookie
                                    var initialCookies = await webView.CoreWebView2.CookieManager.GetCookiesAsync(null);
                                    App.Log($"初始Cookie数量: {initialCookies.Count}");
                                    
                                    // 如果初始导航没有自动发生，手动导航到登录URL
                                    if (webView.CoreWebView2.Source == null || webView.CoreWebView2.Source.ToString() != QQ_LOGIN_URL)
                                    {
                                        App.Log("手动导航到登录URL");
                                        webView.CoreWebView2.Navigate(QQ_LOGIN_URL);
                                    }
                                }
                                else
                                {
                                    string errorDetails = e.InitializationException != null ? e.InitializationException.Message : "未知错误";
                                    errorMessage = $"WebView2初始化失败: {errorDetails}\n请确保已安装Microsoft Edge WebView2 Runtime";
                                    App.LogError("WebView2初始化失败", new Exception(errorDetails, e.InitializationException));
                                    loginWindow.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                errorMessage = "WebView2初始化过程异常: " + ex.Message;
                                App.LogError("WebView2初始化过程异常", ex);
                                loginWindow.Close();
                            }
                        };

                        // 创建DockPanel并添加WebView2
                        var dockPanel = new DockPanel();
                        dockPanel.Children.Add(webView);
                        loginWindow.Content = dockPanel;

                        // 设置登录超时计时器
                        using (var timer = new System.Threading.Timer((state) =>
                        {
                            if (!loginCompleted.WaitOne(0))
                            {
                                errorMessage = "登录超时，请在3分钟内完成登录";
                                System.Windows.Application.Current.Dispatcher.Invoke(() => loginWindow.Close());
                            }
                        }, null, 3 * 60 * 1000, Timeout.Infinite))
                        {
                            // 运行登录窗口
                            loginWindow.Closed += (sender, e) =>
                            {
                                App.Log("登录窗口已关闭");
                                loginCompleted.Set();
                            };

                            // 显示窗口并等待其关闭
                            loginWindow.ShowDialog();
                        }
                    }
                    catch (Exception ex)
                    {
                        errorMessage = "浏览器线程异常: " + ex.Message;
                        App.LogError("浏览器线程异常", ex);
                        loginCompleted.Set();
                    }
                });
                
                // 等待登录完成或超时
                await Task.Run(() => loginCompleted.WaitOne());
                App.Log("登录流程已完成");
                
                // 检查是否有错误
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    return "ERROR:" + errorMessage;
                }
                
                // 检查是否获取到了必要的cookie
                if (!browserCookies.ContainsKey("SERVER_ID") || !browserCookies.ContainsKey("timekey"))
                {
                    return "ERROR:未获取到必要的登录信息，请检查登录是否成功";
                }
                
                // 将cookie复制到类变量中，以便后续使用
                lock (_browserCookies)
                {
                    _browserCookies.Clear();
                    foreach (var cookie in browserCookies)
                    {
                        _browserCookies.Add(cookie.Key, cookie.Value);
                    }
                }
            
            // 使用cookie调用core_togame.php获取登录参数
            string loginParamsResult = await GetLoginParamsFromCoreToGame();
            
            // 如果是成功的登录参数，直接返回格式化好的字符串，与普通版本保持一致
            if (loginParamsResult.StartsWith("LOGIN_PARAMS:"))
            {
                string loginParamsJson = loginParamsResult.Replace("LOGIN_PARAMS:", "");
                var loginParams = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(loginParamsJson);
                return FormatLoginParams(loginParams);
            }
            
            // 否则返回原始错误信息
            return loginParamsResult;
        }
        catch (Exception ex)
        {
            App.LogError("QQ登录内部流程异常", ex);
            return "ERROR:" + ex.Message;
        }
    }

        /// <summary>
        /// WebView2导航完成事件处理
        /// </summary>
        private async void WebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (!(sender is CoreWebView2 webView))
                    return;
                
                // 记录导航信息用于调试
                Debug.WriteLine($"导航完成: {webView.Source}, 成功: {e.IsSuccess}");
                App.Log($"导航完成: {webView.Source}, 成功: {e.IsSuccess}");
                
                // 检查导航是否成功
                if (!e.IsSuccess)
                {
                    Debug.WriteLine($"导航失败，错误码: {e.WebErrorStatus}");
                    App.Log($"导航失败，错误码: {e.WebErrorStatus}");
                    return;
                }
                
                // 检查URL是否是目标页面
                string url = webView.Source.ToString();
                if (url.Contains("web.7k7k.com/games/tpbsn/dlq") || url.Contains("gwlogin.php") || url.Contains("userinfo.html"))
                {
                    // 强制更新所有Cookie
                    await UpdateAllCookiesToDictionary(webView.CookieManager, _browserCookies);
                    
                    // 检查是否已经获取到必要的cookie
                    if (_browserCookies.ContainsKey("SERVER_ID") && _browserCookies.ContainsKey("timekey"))
                    {
                        _loginCompleted = true;
                        App.Log("已获取到必要的Cookie信息，登录成功");
                        
                        // 关闭窗口
                        if (_loginWindow != null && _loginWindow.Dispatcher.CheckAccess())
                        {
                            _webView.Stop();
                            _loginWindow.Close();
                        }
                        else if (_loginWindow != null)
                        {
                            _loginWindow.Dispatcher.Invoke(() =>
                            {
                                if (_webView != null)
                                    _webView.Stop();
                                if (_loginWindow != null)
                                    _loginWindow.Close();
                            });
                        }
                    }
                    else
                    {
                        // 记录缺失的Cookie信息
                        StringBuilder missingCookies = new StringBuilder();
                        if (!_browserCookies.ContainsKey("SERVER_ID"))
                            missingCookies.Append("SERVER_ID, ");
                        if (!_browserCookies.ContainsKey("timekey"))
                            missingCookies.Append("timekey");
                        
                        App.Log($"尚未获取到必要的Cookie: {missingCookies.ToString().TrimEnd(',', ' ')}");
                    }
                }
            }
            catch (Exception ex)
            {
                App.LogError("导航完成事件异常", ex);
                Debug.WriteLine($"导航完成事件异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新所有Cookie到指定字典
        /// </summary>
        private async Task UpdateAllCookiesToDictionary(CoreWebView2CookieManager cookieManager, Dictionary<string, string> cookiesDictionary)
        {
            try
            {
                if (cookieManager == null)
                {
                    Debug.WriteLine("CookieManager为null");
                    App.LogError("CookieManager为null", null);
                    return;
                }
                
                if (cookiesDictionary == null)
                {
                    Debug.WriteLine("cookiesDictionary为null");
                    App.LogError("cookiesDictionary为null", null);
                    return;
                }
                
                // 获取所有相关域名的Cookie
                var cookies1 = await cookieManager.GetCookiesAsync("https://web.7k7k.com");
                var cookies2 = await cookieManager.GetCookiesAsync("https://8.7k7k.com");
                var cookies3 = await cookieManager.GetCookiesAsync("https://connect.qq.com");
                
                // 合并所有Cookie
                var allCookies = new List<CoreWebView2Cookie>();
                allCookies.AddRange(cookies1);
                allCookies.AddRange(cookies2);
                allCookies.AddRange(cookies3);
                
                // 记录已更新的Cookie列表
                StringBuilder cookieLog = new StringBuilder();
                foreach (var cookie in allCookies)
                {
                    if (!string.IsNullOrEmpty(cookie.Name) && !string.IsNullOrEmpty(cookie.Value))
                    {
                        cookiesDictionary[cookie.Name] = cookie.Value;
                        Debug.WriteLine($"Cookie更新: {cookie.Name}={cookie.Value}");
                        cookieLog.Append($"{cookie.Name}, ");
                    }
                }
                
                // 记录已更新的Cookie数量和列表
                App.Log($"已更新Cookie数量: {cookiesDictionary.Count}, Cookie列表: {cookieLog.ToString().TrimEnd(',', ' ')}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新Cookie异常: {ex.Message}");
                App.LogError("更新Cookie时发生异常", ex);
                throw; // 重新抛出异常以便上层调用者能够捕获
            }
        }

        /// <summary>
        /// 使用cookie调用core_togame.php获取登录参数
        /// </summary>
        private async Task<string> GetLoginParamsFromCoreToGame()
        {
            try
            {
                // 验证必要的Cookie是否存在
                if (!_browserCookies.ContainsKey("SERVER_ID"))
                {
                    string error = "SERVER_ID Cookie不存在";
                    Debug.WriteLine(error);
                    App.LogError(error, null);
                    return "ERROR:" + error;
                }
                
                if (!_browserCookies.ContainsKey("timekey"))
                {
                    string error = "timekey Cookie不存在";
                    Debug.WriteLine(error);
                    App.LogError(error, null);
                    return "ERROR:" + error;
                }
                
                App.Log("开始调用core_togame.php获取登录参数");
                
                using (var client = new HttpClient())
                {
                    // 设置超时时间为15秒
                    client.Timeout = TimeSpan.FromSeconds(15);
                    
                    // 设置完整的请求头
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    client.DefaultRequestHeaders.Referrer = new Uri("https://web.7k7k.com/games/tpbsn/dlq/");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/javascript"));
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.01));
                    client.DefaultRequestHeaders.Add("Origin", "https://web.7k7k.com");
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "Windows");
                    client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Microsoft Edge\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135\"");
                    client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
                    client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
                    client.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
                    client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
                    client.DefaultRequestHeaders.Add("Accept-Language", "zh-CN, zh; q=0.9");
                    client.DefaultRequestHeaders.Add("priority", "u=1, i");
                    
                    // 构建cookie字符串，确保包含SERVER_ID和timekey
                    StringBuilder cookieBuilder = new StringBuilder();
                    foreach (var kvp in _browserCookies)
                    {
                        cookieBuilder.Append($"{kvp.Key}={kvp.Value}; ");
                    }
                    
                    string cookieString = cookieBuilder.ToString().TrimEnd(' ', ';');
                    client.DefaultRequestHeaders.Add("Cookie", cookieString);
                    
                    // 记录日志但不记录敏感信息
                    App.Log($"使用Cookie: [已添加{_browserCookies.Count}个Cookie]");
                    Debug.WriteLine($"使用Cookie: {cookieString}");
                    
                    // 简化请求数据，只包含gid=780&sid=1参数
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("gid", "780"),
                        new KeyValuePair<string, string>("sid", "1")
                    });
                    Debug.WriteLine("请求体: gid=780&sid=1");
                    
                    // 发送POST请求到正确的URL
                    string apiUrl = "https://web.7k7k.com/games/dlq/core_togame.php";
                    App.Log($"正在调用API: {apiUrl}");
                    Debug.WriteLine($"正在调用API: {apiUrl}");
                    
                    HttpResponseMessage response = null;
                    try
                    {
                        response = await client.PostAsync(apiUrl, content);
                        
                        // 记录响应状态
                        App.Log($"获取登录参数响应状态码: {response.StatusCode}");
                        Debug.WriteLine($"响应状态码: {response.StatusCode}");
                        
                        // 确保响应成功
                        response.EnsureSuccessStatusCode();
                    }
                    catch (TaskCanceledException ex)
                    {
                        string errorMsg = "请求超时，请检查网络连接";
                        Debug.WriteLine($"请求超时异常: {ex.Message}");
                        App.LogError(errorMsg, ex);
                        return "ERROR:" + errorMsg;
                    }
                    catch (HttpRequestException ex)
                    {
                        string errorMsg = $"HTTP请求失败: {ex.Message}";
                        Debug.WriteLine(errorMsg);
                        App.LogError(errorMsg, ex);
                        return "ERROR:" + errorMsg;
                    }
                    
                    // 读取响应内容
                    string responseContent = await response.Content.ReadAsStringAsync();
                    
                    // 记录响应内容长度但不记录敏感信息
                    App.Log($"获取登录参数成功，响应长度: {responseContent.Length} 字符");
                    Debug.WriteLine($"API响应: {responseContent}");
                    
                    // 解析JSON响应
                    JObject jsonResponse;
                    try
                    {
                        jsonResponse = JObject.Parse(responseContent);
                    }
                    catch (Exception ex)
                    {
                        string errorMsg = "响应不是有效的JSON格式";
                        Debug.WriteLine(errorMsg);
                        App.LogError(errorMsg, ex);
                        return "ERROR:" + errorMsg;
                    }
                    
                    // 检查登录状态
                    if (jsonResponse["status"] != null && jsonResponse["status"].ToString() == "1")
                    {
                        // 转换为登录参数格式
                        Dictionary<string, string> loginParams = ConvertJsonToFormat(jsonResponse);
                        App.Log("登录参数获取成功，准备返回");
                        return "LOGIN_PARAMS:" + Newtonsoft.Json.JsonConvert.SerializeObject(loginParams);
                    }
                    else if (jsonResponse["status"] != null && jsonResponse["status"].ToString() == "0")
                    {
                        // 检查是否为未实名认证的情况
                        if (jsonResponse["url"] != null && jsonResponse["url"].ToString() == "未实名")
                        {
                            // 在UI线程上弹出MessageBox提示
                        if (Application.Current != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show("当前账户未实名认证", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                        }
                            string errorMsg = "当前账户未实名认证";
                            App.LogError(errorMsg, null);
                            return "ERROR:" + errorMsg;
                        }
                        string message = jsonResponse["message"]?.ToString() ?? "登录失败";
                        App.LogError($"API返回错误状态: {message}", null);
                        return "ERROR:" + message;
                    }
                    else
                    {
                        string message = jsonResponse["message"]?.ToString() ?? "登录失败";
                        App.LogError($"API返回未知状态: {message}", null);
                        return "ERROR:" + message;
                    }
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"获取登录参数时发生异常: {ex.Message}";
                Debug.WriteLine(errorMsg);
                App.LogError(errorMsg, ex);
                return "ERROR:" + errorMsg;
            }
        }

        /// <summary>
        /// 将JSON响应转换为包含所有必要参数的字典
        /// </summary>
        private Dictionary<string, string> ConvertJsonToFormat(JObject json)
        {
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            
            // 检查JSON响应中是否包含url字段
            if (json["url"] != null)
            {
                string url = json["url"].ToString();
                
                try
                {
                    // 解析URL中的查询参数
                    Uri uri = new Uri(url);
                    string query = uri.Query;
                    
                    if (!string.IsNullOrEmpty(query))
                    {
                        // 移除开头的问号
                        query = query.TrimStart('?');
                        
                        // 分割查询参数
                        string[] paramPairs = query.Split('&');
                        
                        foreach (string pair in paramPairs)
                        {
                            string[] parts = pair.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                string key = parts[0].Trim();
                                string value = parts[1].Trim();
                                
                                // 存储参数，确保键唯一
                                if (!parameters.ContainsKey(key))
                                {
                                    parameters[key] = value;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"解析URL参数异常: {ex.Message}");
                }
            }
            
            // 从JSON响应中提取其他参数（不覆盖已有的）
            foreach (var property in json.Properties())
            {
                if (property.Name != "status" && property.Name != "message" && property.Name != "url")
                {
                    string value = property.Value.ToString();
                    if (!parameters.ContainsKey(property.Name))
                    {
                        parameters[property.Name] = value;
                    }
                }
            }
            
            return parameters;
        }

        /// <summary>
        /// 生成符合游戏启动格式的登录参数字符串
        /// </summary>
        public static string FormatLoginParams(Dictionary<string, string> loginParams)
        {
            // 获取用户ID
            string userId = loginParams.ContainsKey("userid") ? loginParams["userid"] : "";
            
            // 构建Key部分（使用连字符连接键值对），排除url参数
            List<string> keyParts = new List<string>();
            foreach (var kvp in loginParams)
            {
                // 排除url参数
                if (kvp.Key != "url")
                {
                    keyParts.Add($"{kvp.Key}-{kvp.Value}");
                }
            }
            string keyStr = string.Join("&", keyParts);
            
            // 完整输出格式
            return $"ID={userId},Key={keyStr},PID=7K7K_,PROCPARA=66666,Channel=PC7K7K";
        }
    }
}