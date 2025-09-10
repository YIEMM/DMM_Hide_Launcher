using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AdonisUI.Controls;
using MessageBox = AdonisUI.Controls.MessageBox;
using MessageBoxButton = AdonisUI.Controls.MessageBoxButton;
using MessageBoxImage = AdonisUI.Controls.MessageBoxImage;

namespace DMMDZZ_Game_Start.Others
{
    public partial class ChoosePathFind : AdonisWindow
    {
        public string SelectedPath { get; private set; }

        public ChoosePathFind(List<string> paths)
        {
            InitializeComponent();
            LoadPaths(paths);
        }

        private void LoadPaths(List<string> paths)
        {
            // 对路径列表进行选择排序
            SelectionSort(paths);
            lstPaths.ItemsSource = paths;
        }

        // 选择排序算法
        private void SelectionSort(List<string> list)
        {
            int n = list.Count;
            for (int i = 0; i < n - 1; i++)
            {
                int minIndex = i;
                for (int j = i + 1; j < n; j++)
                {
                    // 比较两个字符串，找出字典序最小的
                    if (string.Compare(list[j], list[minIndex]) < 0)
                    {
                        minIndex = j;
                    }
                }
                // 交换位置
                if (minIndex != i)
                {
                    string temp = list[i];
                    list[i] = list[minIndex];
                    list[minIndex] = temp;
                }
            }
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            if (lstPaths.SelectedItem != null)
            {
                SelectedPath = lstPaths.SelectedItem.ToString();
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("请选择一个游戏路径", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LstPaths_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstPaths.SelectedItem != null)
            {
                SelectedPath = lstPaths.SelectedItem.ToString();
                DialogResult = true;
                Close();
            }
        }
    }
}