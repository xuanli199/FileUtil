using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace FileOccupyDetector
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "所有文件 (*.*)|*.*",
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "选择文件夹"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (paths.Length > 0)
                {
                    txtFilePath.Text = paths[0];
                }
            }
        }

        private void DetectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text) || (!File.Exists(txtFilePath.Text) && !Directory.Exists(txtFilePath.Text)))
            {
                MessageBox.Show("请选择一个有效的文件或文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var processes = FindProcessesLockingFile(txtFilePath.Text);
                dgProcesses.ItemsSource = processes;

                if (processes.Count == 0)
                {
                    MessageBox.Show("没有找到占用该文件的进程", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"检测过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(txtFilePath.Text) && (File.Exists(txtFilePath.Text) || Directory.Exists(txtFilePath.Text)))
            {
                DetectButton_Click(sender, e);
            }
        }

        private void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int processId)
            {
                try
                {
                    Process process = Process.GetProcessById(processId);
                    string processName = process.ProcessName;

                    MessageBoxResult result = MessageBox.Show(
                        $"确定要终止进程 {processName} (ID: {processId})?", 
                        "确认", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        process.Kill();
                        MessageBox.Show($"进程 {processName} 已终止", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshButton_Click(sender, e);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法终止进程: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private List<ProcessInfo> FindProcessesLockingFile(string filePath)
        {
            List<ProcessInfo> result = new List<ProcessInfo>();
            
            try
            {
                // 使用FileUtil类获取占用文件的进程
                var processes = FileUtil.GetProcessesLockingFile(filePath);
                
                foreach (var proc in processes)
                {
                    result.Add(new ProcessInfo
                    {
                        ProcessId = proc.Id,
                        ProcessName = proc.ProcessName,
                        MainWindowTitle = proc.MainWindowTitle
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取占用进程时出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return result;
        }

        // 使用FileUtil类处理文件占用检测
    }

    public class ProcessInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; }
        public string MainWindowTitle { get; set; }
    }
}