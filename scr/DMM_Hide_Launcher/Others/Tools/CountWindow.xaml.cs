using AdonisUI.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Text;


namespace DMM_Hide_Launcher.Others.Tools
{
    /// <summary>
    /// 卡片升级计算器窗口的交互逻辑
    /// </summary>
    public partial class CountWindow : AdonisWindow
    {
        // 碎片消耗数组，每个品质的每个等级的消耗量
        private int[][] suipian = new int[][] 
        {
            // 普通卡
            new int[] { 80, 10, 20, 80, 120, 320, 640, 1200, 1600, 2000, 2280, 2600, 3000 },
            // 稀有卡
            new int[] { 40, 5, 10, 40, 60, 160, 320, 600, 800, 1000, 1140, 1300, 1500 },
            // 史诗卡
            new int[] { 20, 2, 5, 20, 30, 80, 160, 300, 400, 500, 570, 650, 750 }
        };

        // 白金币消耗数组，每个品质的每个等级的消耗量
        private int[][] baijinbi = new int[][] 
        {
            // 普通卡
            new int[] { 0, 150, 300, 1000, 1800, 3200, 4800, 8000, 10000, 13500, 15000, 17500, 20000 },
            // 稀有卡
            new int[] { 0, 300, 600, 2000, 3600, 6400, 9600, 16000, 20000, 27000, 30000, 35000, 40000 },
            // 史诗卡
            new int[] { 0, 600, 1200, 4000, 7200, 12800, 19200, 32000, 40000, 54000, 60000, 70000, 80000 }
        };

        // 万能碎片消耗数组，每个品质的每个等级的消耗量
        private int[][] wanneng = new int[][] 
        {
            // 普通卡
            new int[] { 0, 40, 80, 320, 480, 1280, 2560, 4800, 6400, 8000, 9120, 10400, 12000 },
            // 稀有卡
            new int[] { 0, 80, 160, 640, 960, 2560, 5120, 9600, 12800, 16000, 18240, 20800, 24000 },
            // 史诗卡
            new int[] { 0, 128, 320, 1280, 1920, 5120, 10240, 19200, 25600, 32000, 36480, 41600, 48000 }
        };

        public CountWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 计算按钮点击事件
        /// </summary>
        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            App.Log("计算按钮被点击，开始处理资源计算请求");
            try
            {
                // 解析输入值
                int currentLevel = ParseInput(CurrentLevelTextBox.Text, 0, 13, "当前等级输入无效，请输入一个0到13之间的整数。");
                int targetLevel = ParseInput(TargetLevelTextBox.Text, 0, 13, "目标等级输入无效，请输入一个0到13之间的整数。");
                int qualityIndex = QualityComboBox.SelectedIndex; // 0-普通卡，1-稀有卡，2-史诗卡

                App.Log($"成功解析输入值：当前等级={currentLevel}，目标等级={targetLevel}，品质索引={qualityIndex}");

                // 验证输入
                if (currentLevel >= targetLevel)
                {
                    App.LogWarning($"输入验证失败：当前等级({currentLevel})已经达到或超过目标等级({targetLevel})");
                    ShowErrorMessage("当前等级已经达到或超过目标等级，无需升级。");
                    return;
                }

                if (qualityIndex < 0 || qualityIndex >= 3)
                {
                    App.LogWarning($"输入验证失败：无效的卡片品质索引({qualityIndex})");
                    ShowErrorMessage("请选择有效的卡片品质。");
                    return;
                }

                // 计算资源需求
                CalculateResourcesNeeded(currentLevel, targetLevel, qualityIndex);
            }
            catch (System.Exception ex)
            {
                App.LogError("计算过程中发生错误：" + ex.Message, ex);
                ShowErrorMessage("计算过程中发生错误：" + ex.Message);
            }
        }

        /// <summary>
        /// 重置按钮点击事件
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            App.Log("重置按钮被点击，重置所有输入和结果");
            CurrentLevelTextBox.Text = "0";
            TargetLevelTextBox.Text = "13";
            QualityComboBox.SelectedIndex = 0;
            Count_Out.Text = "";
        }

        /// <summary>
        /// 计算升级所需资源
        /// </summary>
        private void CalculateResourcesNeeded(int currentLevel, int targetLevel, int qualityIndex)
        {
            App.Log($"开始计算资源需求：当前等级={currentLevel}，目标等级={targetLevel}，品质索引={qualityIndex}");
            
            if (currentLevel >= targetLevel)
            {
                App.LogWarning($"计算资源需求时发现当前等级({currentLevel})已经达到或超过目标等级({targetLevel})");
                Count_Out.Text = "当前等级已经达到或超过目标等级，无需升级。";
                return;
            }

            // 计算总消耗
            int totalSuipian = 0;
            int totalBaijinbi = 0;
            int totalWanneng = 0;
            for (int level = currentLevel; level < targetLevel; level++)
            {
                totalSuipian += suipian[qualityIndex][level];
                totalBaijinbi += baijinbi[qualityIndex][level];
                totalWanneng += wanneng[qualityIndex][level];
                
                App.Log($"等级 {level} 到 {level+1}：碎片={suipian[qualityIndex][level]}，白金币={baijinbi[qualityIndex][level]}，万能碎片={wanneng[qualityIndex][level]}");
            }

            App.Log($"资源计算完成：碎片={totalSuipian}，白金币={totalBaijinbi}，万能碎片={totalWanneng}");

            
            if (totalWanneng > 0)
            {
                Count_Out.Text = $"从等级 {currentLevel} 升级到 {targetLevel} 需要的资源：\n碎片：{totalSuipian}\n白金币：{totalBaijinbi}\n万能碎片：{totalWanneng}";
            }
            else
            {
                Count_Out.Text = $"从等级 {currentLevel} 升级到 {targetLevel} 需要的资源：\n碎片：{totalSuipian}\n白金币：{totalBaijinbi}\n万能碎片：不可使用";
            }
        }

        /// <summary>
        /// 解析输入值
        /// </summary>
        private int ParseInput(string input, int min, int max, string errorMessage)
        {
            App.Log($"解析输入值：input='{input}', min={min}, max={max}");
            
            int value;
            
            if (string.IsNullOrWhiteSpace(input))
            {
                App.LogError("输入解析失败：没有输入");
                throw new System.Exception("没有输入，请重新输入。");
            }

            if (!int.TryParse(input, out value))
            {
                App.LogError($"输入解析失败：无法转换为整数，input='{input}'");
                throw new System.Exception(errorMessage);
            }

            if (value < min || value > max)
            {
                App.LogError($"输入解析失败：值不在范围内({min}-{max})，value={value}");
                throw new System.Exception(errorMessage);
            }

            App.Log($"输入解析成功：value={value}");
            return value;
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            App.LogError("显示错误消息：" + message);
            Count_Out.Text = message;
            System.Windows.MessageBox.Show(message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}