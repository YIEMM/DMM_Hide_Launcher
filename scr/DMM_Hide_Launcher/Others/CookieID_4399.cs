using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DMM_Hide_Launcher.Others
{
    public class CookieLoader
    {
        public static void Load4399ID(string GamePath)
        {
            // 定义文件路径
            string filePath1 = GamePath + "/" + @"game/dmmdzz_Data/Plugins/x86/PC4399SDK_RES/cookies.dat";
            string filePath2 = GamePath + "/" + @"game1/dmmdzz_Data/Plugins/x86/PC4399SDK_RES/cookies.dat";
            string filePath3 = GamePath + "/" + @"game2/dmmdzz_Data/Plugins/x86/PC4399SDK_RES/cookies.dat";
            if (File.Exists(filePath1))
            {
                Show4399ID(filePath1);
            }
            else if (File.Exists(filePath2))
            {
                Show4399ID(filePath2);
            }
            else if (File.Exists(filePath3))
            {
                Show4399ID(filePath3);
            }
            else
            {
                Console.WriteLine("4399ID=none");
                var WPF = Application.Current.Windows.OfType<DMM_Hide_Launcher.MainWindow>().FirstOrDefault(); WPF.ID_4399.Text = "";
            }
        }
        private static void Show4399ID(string cookiePath)
        {
            try
            {
                if (!File.Exists(cookiePath))
                {
                    //MessageBox.Show("错误：Cookie文件不存在！", "错误",
                    //    MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string[] lines = File.ReadAllLines(cookiePath);

                // 定义正则表达式提取ck_accname字段的值
                Regex regex = new Regex(
                    @"^\.4399\.com\tTRUE\t/\tFALSE\t\d+\tck_accname\t(.+)$",
                    RegexOptions.Multiline);

                string ckAccNameValue = null;

                foreach (string line in lines)
                {
                    // 跳过注释行
                    if (line.StartsWith("#")) continue;

                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        ckAccNameValue = match.Groups[1].Value;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(ckAccNameValue))
                {
                    // 传递给后续处理函数
                    ProcessCkAccNameValue(ckAccNameValue);
                }
                else
                {
                    //MessageBox.Show("未找到当前4399登录账号", "未找到",
                    //    MessageBoxButton.OK, MessageBoxImage.Information);
                    var WPF = Application.Current.Windows.OfType<DMM_Hide_Launcher.MainWindow>().FirstOrDefault(); WPF.ID_4399.Text = "未登录账号(？)";
                }
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"读取文件时出错: {ex.Message}", "错误",
                //MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine(ex.Message);
            }
        }

        // 后续处理函数示例
        private static void ProcessCkAccNameValue(string rawValue)
        {
            try
            {
                // 1. 尝试判断是否为URL编码
                bool isUrlEncoded = IsUrlEncoded(rawValue);

                // 2. 根据情况解码
                string processedValue = isUrlEncoded
                    ? HttpUtility.UrlDecode(rawValue)
                    : rawValue;

                // 3. 进一步处理（示例：检查是否包含数字）
                string extractedId = ExtractNumericId(processedValue);
                var WPF = Application.Current.Windows.OfType<DMM_Hide_Launcher.MainWindow>().FirstOrDefault(); WPF.ID_4399.Text = processedValue;
                //// 4. 显示处理结果
                //MessageBox.Show(
                //    $"处理后的值: {processedValue}\n",
                //    "处理结果",
                //    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理ck_accname值时出错: {ex.Message}", "处理错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine(ex.Message);
            }
        }

        // 判断字符串是否为URL编码
        private static bool IsUrlEncoded(string input)
        {
            try
            {
                string decoded = HttpUtility.UrlDecode(input);
                return !string.Equals(input, decoded, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        // 从字符串中提取数字ID
        private static string ExtractNumericId(string input)
        {
            try
            {
                Match match = Regex.Match(input, @"\d+");
                var WPF = Application.Current.Windows.OfType<DMM_Hide_Launcher.MainWindow>().FirstOrDefault(); WPF.ID_4399.Text = match.Value;
                return match.Success ? match.Value : "未找到";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提取数字ID时出错: {ex.Message}", "提取错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine(ex.Message);
                return "未找到";
            }
        }
    }
}