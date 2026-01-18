using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Newtonsoft.Json;
using AdonisUI.Controls;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;
using MessageBoxResult = AdonisUI.Controls.MessageBoxResult;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;

namespace DMM_Hide_Launcher.Others
{
    public partial class Game_Update : AdonisWindow
    {
        private string _gamePath;
        private string _targetVersion;
        private string _currentVersion;
        private HttpClient _httpClient;
        private CancellationTokenSource _cancellationTokenSource;
        private string _downloadFilePath;
        private bool _isDownloading = false;

        public Game_Update(string gamePath, string targetVersion)
        {
            InitializeComponent();
            _gamePath = gamePath;
            _targetVersion = targetVersion;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            txtTargetVersion.Text = _targetVersion;
            App.Log($"游戏更新窗口初始化完成，游戏路径: {_gamePath}, 目标版本: {_targetVersion}");

            Loaded += async (sender, e) => await CheckVersionOnLoad();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            App.Log("用户点击关闭按钮");
            Close();
        }

        private async Task CheckVersionOnLoad()
        {
            try
            {
                App.Log("窗口加载完成，开始检查版本");
                UpdateStatus("正在读取当前版本...");

                string currentVersion = await ReadAndParseVersion();
                if (currentVersion == null)
                {
                    UpdateStatus("无法读取当前版本");
                    BtnUpdate.IsEnabled = false;
                    return;
                }

                _currentVersion = currentVersion;
                txtCurrentVersion.Text = _currentVersion;
                App.Log($"当前版本: {_currentVersion}, 目标版本: {_targetVersion}");

                if (!CompareVersions(_currentVersion, _targetVersion))
                {
                    UpdateStatus("当前版本已是最新");
                    BtnUpdate.IsEnabled = false;
                    return;
                }

                UpdateStatus("检测到新版本，请点击开始更新");
                BtnUpdate.IsEnabled = true;
            }
            catch (Exception ex)
            {
                App.LogError("检查版本时出错", ex);
                UpdateStatus($"检查版本失败: {ex.Message}");
                BtnUpdate.IsEnabled = false;
            }
        }

        private async void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            App.Log("用户点击开始更新按钮");
            BtnUpdate.IsEnabled = false;
            BtnClose.IsEnabled = false;

            try
            {
                UpdateStatus("正在获取版本信息...");

                (string packageVer, string packageCrc) = await FetchVersionInfo();
                if (packageVer == null || packageCrc == null)
                {
                    UpdateStatus("获取版本信息失败");
                    BtnUpdate.IsEnabled = true;
                    BtnClose.IsEnabled = true;
                    return;
                }

                App.Log($"获取到版本信息: packageVer={packageVer}, packageCrc={packageCrc}");

                string downloadUrl = GenerateDownloadUrl(packageVer, packageCrc);
                App.Log($"生成下载链接: {downloadUrl}");

                string tempDir = Path.GetTempPath();
                string fileName = $"hide_{packageVer}_{packageCrc}_all.zip";
                string savePath = Path.Combine(tempDir, fileName);

                await DownloadGameFileAsync(downloadUrl, savePath);
            }
            catch (Exception ex)
            {
                App.LogError("更新过程发生异常", ex);
                UpdateStatus($"更新失败: {ex.Message}");
                MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnUpdate.IsEnabled = true;
                BtnClose.IsEnabled = true;
            }
        }

        private string ParseVersionString(string versionString)
        {
            try
            {
                App.Log($"开始解析版本字符串: {versionString}");

                if (string.IsNullOrWhiteSpace(versionString))
                {
                    App.LogWarning("版本字符串为空");
                    return null;
                }

                string[] parts = versionString.Split('_');
                if (parts.Length >= 1)
                {
                    string firstPart = parts[0];
                    App.Log($"版本字符串第一部分: {firstPart}");

                    if (firstPart.Length >= 3)
                    {
                        string majorVersion = firstPart.Substring(0, 1);
                        string minorVersion = firstPart.Substring(1, 2);
                        string parsedVersion = $"{majorVersion}.{minorVersion}.0.0";
                        App.Log($"解析后的版本号: {parsedVersion}");
                        return parsedVersion;
                    }
                    else if (firstPart.Length >= 2)
                    {
                        string majorVersion = firstPart.Substring(0, firstPart.Length - 2);
                        string minorVersion = firstPart.Substring(firstPart.Length - 2);
                        string parsedVersion = $"{majorVersion}.{minorVersion}.0.0";
                        App.Log($"解析后的版本号: {parsedVersion}");
                        return parsedVersion;
                    }
                }

                App.LogWarning($"无法解析版本字符串: {versionString}");
                return null;
            }
            catch (Exception ex)
            {
                App.LogError("解析版本字符串时出错", ex);
                return null;
            }
        }

        private async Task<string> ReadAndParseVersion()
        {
            try
            {
                string versionFromJson = await ReadVersionFromJson();
                if (versionFromJson != null)
                {
                    App.Log($"从JSON文件读取到版本: {versionFromJson}");
                    return versionFromJson;
                }

                App.Log("JSON文件不存在或读取失败，尝试从txt文件读取");
                string versionFilePath = FindVersionFile();

                if (versionFilePath == null)
                {
                    App.LogWarning("未找到版本文件");
                    UpdateStatus("未找到版本文件，请检查游戏目录");
                    return null;
                }

                App.Log($"找到版本文件: {versionFilePath}");

                App.Log("开始异步读取版本文件...");

                var readTask = Task.Run(async () =>
                {
                    return await File.ReadAllTextAsync(versionFilePath).ConfigureAwait(false);
                });

                if (await Task.WhenAny(readTask, Task.Delay(10000)) == readTask)
                {
                    string versionString = await readTask.ConfigureAwait(false);
                    App.Log($"读取到的版本字符串: {versionString}");

                    string parsedVersion = ParseVersionString(versionString);
                    if (parsedVersion == null)
                    {
                        UpdateStatus("版本格式解析失败");
                        return null;
                    }

                    return parsedVersion;
                }
                else
                {
                    App.LogWarning("读取版本文件超时");
                    UpdateStatus("读取版本文件超时，请检查文件是否被占用");
                    return null;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                App.LogError("读取版本文件时权限不足", ex);
                UpdateStatus("读取版本文件时权限不足");
                return null;
            }
            catch (IOException ex)
            {
                App.LogError("读取版本文件时IO错误", ex);
                UpdateStatus($"读取版本文件失败: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                App.LogError("读取版本文件时发生未知错误", ex);
                UpdateStatus($"读取版本文件失败: {ex.Message}");
                return null;
            }
        }

        private async Task<string> ReadVersionFromJson()
        {
            try
            {
                string[] possiblePaths = new string[]
                {
                    Path.Combine(_gamePath, "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game1", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game2", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game3", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json")
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        App.Log($"找到CodeVersion.json文件: {path}");

                        try
                        {
                            string jsonContent = await Task.Run(async () =>
                            {
                                return await File.ReadAllTextAsync(path).ConfigureAwait(false);
                            }).ConfigureAwait(false);

                            var jsonObj = JsonConvert.DeserializeObject<CodeVersionJson>(jsonContent);
                            if (jsonObj != null && !string.IsNullOrWhiteSpace(jsonObj.Version))
                            {
                                string version = jsonObj.Version.ToString();
                                App.Log($"从JSON读取到版本号: {version}");
                                return version;
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            App.LogError($"解析JSON文件时出错: {jsonEx.Message}", jsonEx);
                        }
                    }
                }

                App.LogWarning("未找到CodeVersion.json文件");
                return null;
            }
            catch (Exception ex)
            {
                App.LogError("从JSON读取版本时出错", ex);
                return null;
            }
        }

        private class CodeVersionJson
        {
            public string Version { get; set; }
        }

        private string FindVersionFile()
        {
            try
            {
                App.Log("开始搜索版本文件...");

                string[] possiblePaths = new string[]
                {
                    Path.Combine(_gamePath, "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game1", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game2", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "game3", "dmmdzz_Data", "HybridCLR", "Feature", "CodeVersion.json"),
                    Path.Combine(_gamePath, "dmmdzz_Data", "StreamingAssets", "Bundles", "ResLods.txt"),
                    Path.Combine(_gamePath, "game", "dmmdzz_Data", "StreamingAssets", "Bundles", "ResLods.txt"),
                    Path.Combine(_gamePath, "game1", "dmmdzz_Data", "StreamingAssets", "Bundles", "ResLods.txt"),
                    Path.Combine(_gamePath, "game2", "dmmdzz_Data", "StreamingAssets", "Bundles", "ResLods.txt"),
                    Path.Combine(_gamePath, "game3", "dmmdzz_Data", "StreamingAssets", "Bundles", "ResLods.txt")
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        App.Log($"找到版本文件: {path}");
                        return path;
                    }
                }

                App.LogWarning("在所有可能的路径中未找到版本文件");
                return null;
            }
            catch (Exception ex)
            {
                App.LogError("搜索版本文件时出错", ex);
                return null;
            }
        }

        private bool CompareVersions(string currentVersion, string targetVersion)
        {
            try
            {
                App.Log($"开始比较版本: 当前版本={currentVersion}, 目标版本={targetVersion}");

                if (string.IsNullOrWhiteSpace(currentVersion) || string.IsNullOrWhiteSpace(targetVersion))
                {
                    App.LogWarning("版本号为空，无法比较");
                    return false;
                }

                string[] currentParts = currentVersion.Split('.');
                string[] targetParts = targetVersion.Split('.');

                if (currentParts.Length < 2 || targetParts.Length < 2)
                {
                    App.LogWarning($"版本格式不正确: 当前版本={currentVersion}, 目标版本={targetVersion}");
                    return false;
                }

                if (!int.TryParse(currentParts[0], out int currentMajor) ||
                    !int.TryParse(currentParts[1], out int currentMinor) ||
                    !int.TryParse(targetParts[0], out int targetMajor) ||
                    !int.TryParse(targetParts[1], out int targetMinor))
                {
                    App.LogWarning("版本号解析失败，无法转换为数字");
                    return false;
                }

                App.Log($"版本号解析结果: 当前版本={currentMajor}.{currentMinor}, 目标版本={targetMajor}.{targetMinor}");

                if (currentMajor < targetMajor)
                {
                    App.Log($"当前版本 {currentVersion} 低于目标版本 {targetVersion}，需要更新");
                    return true;
                }
                else if (currentMajor == targetMajor && currentMinor < targetMinor)
                {
                    App.Log($"当前版本 {currentVersion} 低于目标版本 {targetVersion}，需要更新");
                    return true;
                }
                else
                {
                    App.Log($"当前版本 {currentVersion} 不低于目标版本 {targetVersion}，无需更新");
                    return false;
                }
            }
            catch (Exception ex)
            {
                App.LogError("比较版本号时出错", ex);
                return false;
            }
        }

        private async Task<(string packageVer, string packageCrc)> FetchVersionInfo()
        {
            try
            {
                string versionXmlUrl = "http://update.ss.igreatdream.com/hide_pc/release/4400/game/version.xml";
                App.Log($"开始请求版本信息接口: {versionXmlUrl}");

                var requestTask = _httpClient.GetAsync(versionXmlUrl);
                var timeoutTask = Task.Delay(30000);

                if (await Task.WhenAny(requestTask, timeoutTask) == timeoutTask)
                {
                    App.LogWarning("请求版本信息接口超时");
                    UpdateStatus("请求版本信息超时，请检查网络连接");
                    return (null, null);
                }

                HttpResponseMessage response = await requestTask.ConfigureAwait(false);
                App.Log($"HTTP响应状态码: {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    App.LogWarning($"HTTP请求失败，状态码: {(int)response.StatusCode}");
                    UpdateStatus($"请求版本信息失败: HTTP {(int)response.StatusCode}");
                    return (null, null);
                }

                string xmlContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                App.Log($"成功获取XML内容，长度: {xmlContent.Length}");

                return ParseVersionXml(xmlContent);
            }
            catch (TaskCanceledException ex)
            {
                App.LogError("请求版本信息超时", ex);
                UpdateStatus("请求版本信息超时，请检查网络连接");
                return (null, null);
            }
            catch (HttpRequestException ex)
            {
                App.LogError("请求版本信息时网络错误", ex);
                UpdateStatus($"网络请求失败: {ex.Message}");
                return (null, null);
            }
            catch (Exception ex)
            {
                App.LogError("请求版本信息时发生未知错误", ex);
                UpdateStatus($"获取版本信息失败: {ex.Message}");
                return (null, null);
            }
        }

        private (string packageVer, string packageCrc) ParseVersionXml(string xmlContent)
        {
            try
            {
                App.Log("开始解析XML内容");
                XElement root = XElement.Parse(xmlContent);

                XElement packageElement = root.Element("package");
                if (packageElement == null)
                {
                    App.LogWarning("XML中未找到package节点");
                    UpdateStatus("版本信息格式错误: 未找到package节点");
                    return (null, null);
                }

                XElement gameElement = packageElement.Element("game");
                if (gameElement == null)
                {
                    App.LogWarning("XML中未找到game节点");
                    UpdateStatus("版本信息格式错误: 未找到game节点");
                    return (null, null);
                }

                string packageVer = gameElement.Attribute("ver")?.Value;
                string packageCrc = gameElement.Attribute("crc")?.Value;

                if (string.IsNullOrEmpty(packageVer))
                {
                    App.LogWarning("XML中game节点缺少ver属性");
                    UpdateStatus("版本信息格式错误: 缺少版本号");
                    return (null, null);
                }

                if (string.IsNullOrEmpty(packageCrc))
                {
                    App.LogWarning("XML中game节点缺少crc属性");
                    UpdateStatus("版本信息格式错误: 缺少CRC值");
                    return (null, null);
                }

                App.Log($"XML解析成功: ver={packageVer}, crc={packageCrc}");
                return (packageVer, packageCrc);
            }
            catch (System.Xml.XmlException ex)
            {
                App.LogError("解析XML时格式错误", ex);
                UpdateStatus("版本信息格式错误: XML解析失败");
                return (null, null);
            }
            catch (Exception ex)
            {
                App.LogError("解析XML时发生未知错误", ex);
                UpdateStatus($"解析版本信息失败: {ex.Message}");
                return (null, null);
            }
        }

        private string GenerateDownloadUrl(string packageVer, string packageCrc)
        {
            try
            {
                string baseUrl = "http://update.ss.igreatdream.com/hide_pc/release/4400/game";
                string fileName = $"hide_{packageVer}_{packageCrc}_all.zip";
                string downloadUrl = $"{baseUrl}/{packageVer}/{fileName}";
                App.Log($"生成下载链接: {downloadUrl}");
                return downloadUrl;
            }
            catch (Exception ex)
            {
                App.LogError("生成下载链接时出错", ex);
                return null;
            }
        }

        private void UpdateStatus(string status)
        {
            try
            {
                if (!Dispatcher.CheckAccess())
                {
                    Dispatcher.Invoke(() => UpdateStatus(status));
                    return;
                }

                txtStatus.Text = status;
                App.Log($"更新状态: {status}");
            }
            catch (Exception ex)
            {
                App.LogError("更新状态时出错", ex);
            }
        }

        private async Task DownloadGameFileAsync(string url, string savePath)
        {
            _isDownloading = true;
            _cancellationTokenSource = new CancellationTokenSource();
            _downloadFilePath = savePath;
            
            bool downloadSuccess = false;
            
            try
            {
                App.Log($"开始下载: {url}");
                UpdateStatus("正在下载...");
                ShowDownloadInfoPanel(true);
                ShowCancelButton(true);

                using (var downloadClient = new HttpClient())
                {
                    downloadClient.Timeout = TimeSpan.FromMinutes(30);
                    
                    var response = await downloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cancellationTokenSource.Token);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    App.Log($"文件总大小: {totalBytes} 字节");

                    if (totalBytes == 0)
                    {
                        throw new InvalidDataException("服务器未返回文件大小信息");
                    }

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[81920];
                        long totalBytesRead = 0;
                        int bytesRead;
                        var startTime = DateTime.Now;
                        var lastUpdateTime = startTime;
                        long lastBytesRead = 0;
                        int updateCounter = 0;

                        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead, _cancellationTokenSource.Token);
                            totalBytesRead += bytesRead;

                            updateCounter++;
                            if (updateCounter >= 10)
                            {
                                updateCounter = 0;
                                var currentTime = DateTime.Now;
                                var timeElapsed = (currentTime - lastUpdateTime).TotalSeconds;
                                
                                if (timeElapsed > 0.5)
                                {
                                    var bytesSinceLastUpdate = totalBytesRead - lastBytesRead;
                                    var speedBytesPerSecond = bytesSinceLastUpdate / timeElapsed;
                                    
                                    var remainingBytes = totalBytes - totalBytesRead;
                                    var remainingSeconds = speedBytesPerSecond > 0 ? remainingBytes / speedBytesPerSecond : 0;
                                    var remainingTime = TimeSpan.FromSeconds(remainingSeconds);

                                    double progress = (double)totalBytesRead / totalBytes * 100;
                                    
                                    Dispatcher.Invoke(() =>
                                    {
                                        progressBar.Value = progress;
                                        txtDownloadedSize.Text = $"{FormatBytes(totalBytesRead)} / {FormatBytes(totalBytes)}";
                                        txtDownloadSpeed.Text = FormatSpeed(speedBytesPerSecond);
                                        txtRemainingTime.Text = $"{(int)remainingTime.TotalHours:D2}:{remainingTime.Minutes:D2}:{remainingTime.Seconds:D2}";
                                    });

                                    lastUpdateTime = currentTime;
                                    lastBytesRead = totalBytesRead;
                                }
                            }
                        }

                        await fileStream.FlushAsync(_cancellationTokenSource.Token);
                        App.Log($"下载完成，实际下载: {totalBytesRead} 字节，预期: {totalBytes} 字节");

                        if (totalBytesRead != totalBytes)
                        {
                            throw new InvalidDataException($"下载不完整：实际下载 {totalBytesRead} 字节，预期 {totalBytes} 字节");
                        }
                    }
                }

                App.Log("下载完成");
                downloadSuccess = true;
                UpdateStatus("下载完成，正在验证文件...");
                
                await ValidateAndExtractFileAsync(savePath);
            }
            catch (OperationCanceledException)
            {
                App.Log("下载已被取消");
                UpdateStatus("下载已取消");
                downloadSuccess = false;
            }
            catch (Exception ex)
            {
                App.LogError("下载文件时出错", ex);
                UpdateStatus($"下载失败: {ex.Message}");
                downloadSuccess = false;
            }
            finally
            {
                _isDownloading = false;
                ShowCancelButton(false);
                
                if (!downloadSuccess)
                {
                    CleanupDownloadFile(savePath);
                    ShowDownloadInfoPanel(false);
                    BtnUpdate.IsEnabled = true;
                    BtnClose.IsEnabled = true;
                }
            }
        }

        private async Task ValidateAndExtractFileAsync(string filePath)
        {
            try
            {
                UpdateStatus("正在验证文件...");
                App.Log($"开始验证文件: {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("下载的文件不存在");
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < 1024 * 1024)
                {
                    throw new InvalidDataException($"文件过小: {fileInfo.Length} 字节");
                }

                if (!IsValidZipFile(filePath))
                {
                    throw new InvalidDataException("ZIP文件损坏或不完整");
                }

                App.Log("文件验证通过，开始解压");
                await ExtractZipFileAsync(filePath, _gamePath);

                App.Log("删除HybridCLR文件夹");
                DeleteHybridCLRFolder();

                App.Log("重新检查当前版本");
                await CheckVersionAfterUpdate();

                UpdateStatus("解压完成，更新完成！");
                CleanupDownloadFile(filePath);
                ShowDownloadInfoPanel(false);
                BtnUpdate.IsEnabled = true;
                BtnClose.IsEnabled = true;

                App.Log("游戏更新流程完成");

                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        "游戏更新完成！\n\n请重新启动游戏以应用更新。",
                        "更新成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                });
            }
            catch (Exception ex)
            {
                App.LogError("验证或解压文件时出错", ex);
                UpdateStatus($"更新失败: {ex.Message}");
                CleanupDownloadFile(filePath);
                ShowDownloadInfoPanel(false);
                BtnUpdate.IsEnabled = true;
                BtnClose.IsEnabled = true;
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private string FormatSpeed(double bytesPerSecond)
        {
            string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s" };
            double len = bytesPerSecond;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void CleanupDownloadFile(string filePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                    App.Log($"已清理下载文件: {filePath}");
                }
            }
            catch (Exception ex)
            {
                App.LogError("清理下载文件时出错", ex);
            }
        }

        private void BtnCancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading && _cancellationTokenSource != null)
            {
                App.Log("用户取消下载");
                _cancellationTokenSource.Cancel();
                UpdateStatus("正在取消下载...");
            }
        }
        
        /// <summary>
        /// 验证ZIP文件的完整性
        /// </summary>
        private bool IsValidZipFile(string zipPath)
        {
            try
            {
                // 使用与解压相同的方式打开ZIP文件，进行更严格的验证
                using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Read, System.Text.Encoding.Default))
                {
                    // 检查ZIP文件是否能被打开，以及是否包含至少一个条目
                    if (zipArchive.Entries.Count == 0)
                    {
                        App.LogError("ZIP文件不包含任何条目，可能已损坏");
                        return false;
                    }
                    
                    // 尝试读取第一个条目的名称，进一步验证ZIP完整性
                    var firstEntry = zipArchive.Entries[0];
                    if (string.IsNullOrEmpty(firstEntry.Name))
                    {
                        App.LogError("ZIP文件条目名称无效，可能已损坏");
                        return false;
                    }
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                App.LogError($"验证ZIP文件完整性失败: {ex.Message}");
                return false;
            }
        }
        
        private void ShowDownloadInfoPanel(bool show)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowDownloadInfoPanel(show));
                return;
            }

            var downloadInfoBorder = this.FindName("downloadInfoBorder") as Border;
            if (downloadInfoBorder != null)
            {
                downloadInfoBorder.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        private void ShowCancelButton(bool show)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowCancelButton(show));
                return;
            }

            BtnCancelDownload.Visibility = show ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        private string FindGameExecutable()
        {
            try
            {
                App.Log("开始查找游戏可执行文件(dmmdzz.exe)...");

                string[] possiblePaths = new string[]
                {
                    Path.Combine(_gamePath, "dmmdzz.exe"),
                    Path.Combine(_gamePath, "game", "dmmdzz.exe"),
                    Path.Combine(_gamePath, "game1", "dmmdzz.exe"),
                    Path.Combine(_gamePath, "game2", "dmmdzz.exe"),
                    Path.Combine(_gamePath, "game3", "dmmdzz.exe")
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        App.Log($"找到游戏可执行文件: {path}");
                        return path;
                    }
                }

                App.LogWarning("未找到游戏可执行文件(dmmdzz.exe)");
                return null;
            }
            catch (Exception ex)
            {
                App.LogError("查找游戏可执行文件时出错", ex);
                return null;
            }
        }

        private async Task ExtractZipFileAsync(string zipPath, string extractPath)
        {
            try
            {
                UpdateStatus("正在解压文件...");
                
                string gameExePath = FindGameExecutable();
                if (gameExePath == null)
                {
                    throw new FileNotFoundException("未找到游戏可执行文件(dmmdzz.exe)");
                }

                string gameDirectory = Path.GetDirectoryName(gameExePath);
                App.Log($"开始解压: {zipPath} -> {gameDirectory}");

                if (!Directory.Exists(gameDirectory))
                {
                    Directory.CreateDirectory(gameDirectory);
                }

                await Task.Run(() =>
                {
                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        int totalEntries = archive.Entries.Count;
                        int currentEntry = 0;

                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            currentEntry++;
                            double progress = (double)currentEntry / totalEntries * 100;

                            Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = progress;
                                UpdateStatus($"正在解压文件... ({currentEntry}/{totalEntries}) {progress:F1}%");
                            });

                            string destinationPath = Path.Combine(gameDirectory, entry.FullName);

                            string directory = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                            {
                                Directory.CreateDirectory(directory);
                            }

                            if (!string.IsNullOrEmpty(entry.Name))
                            {
                                entry.ExtractToFile(destinationPath, true);
                            }
                        }
                    }
                });

                App.Log("解压完成");
                UpdateStatus("解压完成，更新完成！");
            }
            catch (Exception ex)
            {
                App.LogError("解压文件时出错", ex);
                UpdateStatus($"解压失败: {ex.Message}");
                throw;
            }
        }

        private void DeleteHybridCLRFolder()
        {
            try
            {
                App.Log("开始删除HybridCLR文件夹");

                string[] possiblePaths = new string[]
                {
                    Path.Combine(_gamePath, "dmmdzz_Data", "HybridCLR"),
                    Path.Combine(_gamePath, "game", "dmmdzz_Data", "HybridCLR"),
                    Path.Combine(_gamePath, "game1", "dmmdzz_Data", "HybridCLR"),
                    Path.Combine(_gamePath, "game2", "dmmdzz_Data", "HybridCLR"),
                    Path.Combine(_gamePath, "game3", "dmmdzz_Data", "HybridCLR")
                };

                foreach (string path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                        App.Log($"已删除HybridCLR文件夹: {path}");
                    }
                }

                App.Log("HybridCLR文件夹删除完成");
            }
            catch (Exception ex)
            {
                App.LogError("删除HybridCLR文件夹时出错", ex);
            }
        }

        private async Task CheckVersionAfterUpdate()
        {
            try
            {
                App.Log("重新检查当前版本");
                UpdateStatus("正在检查版本...");

                string currentVersion = await ReadAndParseVersion();
                if (currentVersion == null)
                {
                    App.LogWarning("无法读取更新后的版本");
                    return;
                }

                _currentVersion = currentVersion;
                txtCurrentVersion.Text = _currentVersion;
                App.Log($"更新后当前版本: {_currentVersion}");

                if (!CompareVersions(_currentVersion, _targetVersion))
                {
                    App.Log($"版本检查通过: 当前版本 {_currentVersion} >= 目标版本 {_targetVersion}");
                    UpdateStatus("版本检查通过，更新成功！");
                }
                else
                {
                    App.LogWarning($"版本检查失败: 当前版本 {_currentVersion} < 目标版本 {_targetVersion}");
                    UpdateStatus("版本检查失败，可能需要重新更新");
                }
            }
            catch (Exception ex)
            {
                App.LogError("检查版本时出错", ex);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            if (_isDownloading && _cancellationTokenSource != null)
            {
                App.Log("窗口关闭时正在下载，取消下载");
                _cancellationTokenSource.Cancel();
                CleanupDownloadFile(_downloadFilePath);
            }
            
            _httpClient?.Dispose();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            App.Log("游戏更新窗口已关闭，资源已释放");
        }
    }
}
