using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace Others
{
    public class GamePathFinder
    {
        public List<string> FindGamePaths()
        {
            List<string> results = new List<string>();

            try
            {
                // 注册表路径
                string registryPath = @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Compatibility Assistant\Store";

                // 要查找的路径片段
                string[] targetFragments = new string[]
                {
                    @"dmmdzz_4399\DmmdzzLoader.exe",
                    @"dmmdzz_4399\hide_pc_launcher.exe",
                    @"dmmdzz_7k7k\hide_pc_launcher.exe",
                    @"dmmdzz_7k7k\DmmdzzLoader.exe"
                };

                // 打开注册表项
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath))
                {
                    if (key == null)
                    {
                        return results;
                    }

                    // 获取所有子项名称
                    string[] valueNames = key.GetValueNames();

                    // 遍历所有名称并检查是否符合条件
                    foreach (string name in valueNames)
                    {
                        foreach (string fragment in targetFragments)
                        {
                            if (name.Contains(fragment))
                            {
                                // 提取所需的路径部分
                                int index = name.LastIndexOf('\\');
                                if (index > 0)
                                {
                                    string resultPath = name.Substring(0, index);
                                    if (!results.Contains(resultPath))
                                    {
                                        results.Add(resultPath);
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误：{ex.Message}");
            }

            return results;
        }
    }
}
