using HandyControl.Controls;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

namespace DMM_Hide_Launcher.Others
{
    /// <summary>
    /// HTTP请求器类
    /// 负责处理与7K7K游戏平台的HTTP请求交互，包括登录验证等功能
    /// </summary>
    public class HttpRequester
    {
        /// <summary>
        /// 执行登录请求序列
        /// 依次执行获取服务器ID、登录验证和游戏核心请求
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <param name="GamePath">游戏路径</param>
        /// <returns>请求结果字符串</returns>
        public static async Task<string> ExecuteRequests(string username, string password, string GamePath)
        {
            // 获取当前日期
            string playdate = DateTime.Now.ToString("yyyy-MM-dd");
            string GamePath1 = GamePath;
            using (HttpClient client = new HttpClient())
            {
                // 设置通用请求头
                SetCommonHeaders(client);

                // 第一个请求
                string serverId = await GetServerId(client, username, password);

                // 第二个请求
                string timekey = await Login(client, username, password, serverId);

                // 第三个请求
                return await CoreToGame(client, serverId, timekey, username, password, playdate, GamePath1);

            }
        }

        /// <summary>
        /// 设置HTTP客户端的通用请求头
        /// 配置压缩、用户代理等通用请求头信息
        /// </summary>
        /// <param name="client">HTTP客户端实例</param>
        static void SetCommonHeaders(HttpClient client)
        {
            // 让 HttpClient 自动处理压缩
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

            // 设置通用请求头
            client.DefaultRequestHeaders.Add("sec-ch-ua-platform", "Windows");
            client.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");
            client.DefaultRequestHeaders.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36 Edg/135.0.0.0");
            client.DefaultRequestHeaders.Add("accept", "application/json, text/javascript, */*; q=0.01");
            client.DefaultRequestHeaders.Add("sec-ch-ua", "\"Microsoft Edge\";v=\"135\", \"Not-A.Brand\";v=\"8\", \"Chromium\";v=\"135\"");
            client.DefaultRequestHeaders.Add("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.Add("origin", "https://web.7k7k.com");
            client.DefaultRequestHeaders.Add("sec-fetch-site", "same-origin");
            client.DefaultRequestHeaders.Add("sec-fetch-mode", "cors");
            client.DefaultRequestHeaders.Add("sec-fetch-dest", "empty");
            client.DefaultRequestHeaders.Add("referer", "https://web.7k7k.com/games/tpbsn/dlq/");
            client.DefaultRequestHeaders.Add("accept-encoding", "gzip, deflate, br, zstd");
            client.DefaultRequestHeaders.Add("accept-language", "zh-CN,zh;q=0.9");
            client.DefaultRequestHeaders.Add("priority", "u=1, i");
        }

        static async Task<string> GetServerId(HttpClient client, string username, string password)
        {
            try
            {
                string url = "https://web.7k7k.com/games/get_servers.php";
                string postData = "gid=780&rand=" + new Random().NextDouble();
                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                // 解析 SERVER_ID（这里简单假设可以从 cookie 中获取）
                return GetCookieValue(response, "SERVER_ID");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"获取服务器 ID 时发生错误: {ex.Message}");
                return "";
            }
        }

        static async Task<string> Login(HttpClient client, string username, string password, string serverId)
        {
            try
            {
                string url = "https://web.7k7k.com/source/Post.php";
                string postData = $"username={username}&password={password}&formtype=index_log";
                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");

                // 设置 cookie
                client.DefaultRequestHeaders.Add("cookie", $"SERVER_ID={serverId}");

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                // 解析 timekey
                return GetCookieValue(response, "timekey");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"登录时发生错误: {ex.Message}");
                return "";
            }
        }

        static async Task<string> CoreToGame(HttpClient client, string serverId, string timekey, string username, string password, string playdate, string GamePath)
        {
            try
            {
                string url = "https://web.7k7k.com/games/dlq/core_togame.php";
                string postData = "gid=780&sid=1";
                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");

                // 设置 cookie
                string cookie = $"SERVER_ID={serverId}; timekey={timekey}; username={username}; identity={username}; nickname=114514; userid=114514; kk=114514; logintime={timekey}; k7_lastlogin=114514; loginfrom=0011; avatar=http%3A%2F%2Fsface.7k7kimg.cn%2Fuicons%2Fphoto_default_s.png; securitycode=f5b4abfccd713a2b6e563042869ac6e0; k7_lastlogin=1970-01-01+13%3A44%3A42; k7_union=9999999; k7_username={username}; k7_uid=894078393; k7_from=17944284; k7_reg=1727098013; k7_ip=10.19.84.36; userprotect=a3f8773d6255165006eb2d16583732e2; userpermission=31f2d13b9ada1dad754295935f8236a7; k7_lastloginip=114.114.114.114";
                client.DefaultRequestHeaders.Add("cookie", cookie);

                HttpResponseMessage response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                // 读取响应体内容
                string responseBody = await ReadResponseAsString(response);

                // 去除首尾空格
                responseBody = responseBody.Trim();

                // 解析 JSON
                using (JsonDocument jsonDoc = JsonDocument.Parse(responseBody))
                {
                    JsonElement root = jsonDoc.RootElement;
                    if (root.TryGetProperty("status", out JsonElement statusElement) &&
                        statusElement.GetInt32() == 1 &&
                        root.TryGetProperty("url", out JsonElement urlElement) &&
                        urlElement.GetString() == "/games/tpbsn/dlq")
                    {
                        Growl.Warning("错误的账户/密码");
                    }
                    else
                    {
                        string[] files = Directory.GetFiles(GamePath, "dmmdzz.exe", SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            //MessageBox.Show(responseBody);
                            {
                                string json = responseBody;
                                string pid = "7K7K_";
                                string procpPara = "66666";
                                string channel = "PC7K7K";
                                string code7k7k = ConvertJsonToFormat(json, pid, procpPara, channel);
                                Process.Start(files[0], code7k7k);
                                //MessageBox.Show(code7k7k);
                                //MessageBox.Show(code7k7k);
                            }
                            static string ConvertJsonToFormat(string json, string pid, string procpPara, string channel)
                            {
                                var query = System.Web.HttpUtility.ParseQueryString(new Uri(JObject.Parse(json)["url"].ToString()).Query);
                                string id = query["userid"];
                                string key = string.Join("&", query.AllKeys.Select(k => $"{k}-{query[k]}"));
                                return $"ID={id},Key={key},PID={pid},PROCPARA={procpPara},Channel={channel}";
                            }
                        }
                        else
                        {
                            Growl.Warning("未找到游戏");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 错误处理
                return ex.Message;
            }
            return "";
        }


        static string GetCookieValue(HttpResponseMessage response, string cookieName)
        {
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (string cookie in cookies)
                {
                    if (cookie.StartsWith($"{cookieName}="))
                    {
                        return cookie.Split('=')[1].Split(';')[0];
                    }
                }
            }
            return "";
        }

        static async Task<string> ReadResponseAsString(HttpResponseMessage response)
        {
            Encoding encoding = GetEncodingFromResponse(response);
            byte[] responseBytes = await response.Content.ReadAsByteArrayAsync();

            // 处理压缩响应
            responseBytes = await DecompressResponse(response, responseBytes);

            try
            {
                return encoding.GetString(responseBytes);
            }
            catch (DecoderFallbackException)
            {
                // 尝试其他编码
                Encoding[] fallbackEncodings = new Encoding[] { Encoding.GetEncoding("GB2312"), Encoding.GetEncoding("GBK") };
                foreach (Encoding fallbackEncoding in fallbackEncodings)
                {
                    try
                    {
                        return fallbackEncoding.GetString(responseBytes);
                    }
                    catch (DecoderFallbackException)
                    {
                        continue;
                    }
                }
                return encoding.GetString(responseBytes);
            }
        }

        static async Task<byte[]> DecompressResponse(HttpResponseMessage response, byte[] responseBytes)
        {
            // 检查是否是 gzip 压缩
            if (response.Content.Headers.ContentEncoding.Contains("gzip"))
            {
                using (var ms = new System.IO.MemoryStream(responseBytes))
                using (var gzipStream = new GZipStream(ms, CompressionMode.Decompress))
                using (var outputMs = new System.IO.MemoryStream())
                {
                    await gzipStream.CopyToAsync(outputMs);
                    return outputMs.ToArray();
                }
            }
            // 检查是否是 deflate 压缩
            else if (response.Content.Headers.ContentEncoding.Contains("deflate"))
            {
                using (var ms = new System.IO.MemoryStream(responseBytes))
                using (var deflateStream = new DeflateStream(ms, CompressionMode.Decompress))
                using (var outputMs = new System.IO.MemoryStream())
                {
                    await deflateStream.CopyToAsync(outputMs);
                    return outputMs.ToArray();
                }
            }
            return responseBytes;
        }

        static Encoding GetEncodingFromResponse(HttpResponseMessage response)
        {
            if (response.Content.Headers.ContentType != null && response.Content.Headers.ContentType.CharSet != null)
            {
                try
                {
                    return Encoding.GetEncoding(response.Content.Headers.ContentType.CharSet);
                }
                catch (ArgumentException)
                {
                    // 如果指定的编码格式无效，默认使用 UTF - 8
                    return Encoding.UTF8;
                }
            }
            // 如果响应头中没有指定编码格式，默认使用 UTF - 8
            return Encoding.UTF8;
        }
    }
}