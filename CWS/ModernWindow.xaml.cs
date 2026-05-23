using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Markup;
using CWS.Services;

namespace CWS
{
    public partial class ModernWindow : Window
    {
        private CancellationTokenSource? _pptMonitorCts;
        private readonly int _pptMonitorIntervalMs = 5000;
        private FloatingBall _floatingBall;
        private bool _isSwitchingToFloating = false;

        public ModernWindow()
        {
            InitializeComponent();

            _floatingBall = new FloatingBall();
            _floatingBall.MouseDoubleClick += (s, e) => RestoreFromFloatingBall();

            txtVersionDisplay.Text = $"CWS v{UpdateChecker.CurrentVersion}";
        }

        // --- 语言管理 ---
        private void ApplyLanguage(string langCode)
        {
            try
            {
                ResourceDictionary? newDict = null;
                string externalPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Langs", $"{langCode}.xaml");

                if (File.Exists(externalPath))
                {
                    using (FileStream fs = new FileStream(externalPath, FileMode.Open, FileAccess.Read))
                    {
                        newDict = (ResourceDictionary)XamlReader.Load(fs);
                    }
                }
                else
                {
                    string internalPath = $"pack://application:,,,/Langs/{langCode}.xaml";
                    newDict = new ResourceDictionary { Source = new Uri(internalPath, UriKind.RelativeOrAbsolute) };
                }

                if (newDict != null)
                {
                    var mergedDicts = Application.Current.Resources.MergedDictionaries;
                    ResourceDictionary? oldDict = mergedDicts.FirstOrDefault(d => d.Contains("Lang_Nav_General"));
                    if (oldDict != null) mergedDicts.Remove(oldDict);
                    mergedDicts.Add(newDict);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Language switch failed: {ex.Message}");
                Logger.Error($"Language switch failed: {ex.Message}");
            }
        }

        private void comboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboLang?.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag != null)
            {
                ApplyLanguage(selectedItem.Tag.ToString()!);
                Logger.Info($"Language switched to {selectedItem.Tag}");
            }
        }

        private void ShowToast(string message, string type = "info")
        {
            Dispatcher.Invoke(() =>
            {
                if (ToastPanel == null) return;

                string color, icon;
                switch (type)
                {
                    case "success":
                        color = "#16A34A"; icon = "\xE73E";
                        break;
                    case "warning":
                        color = "#EA580C"; icon = "\xE7BA";
                        break;
                    case "error":
                        color = "#DC2626"; icon = "\xE783";
                        break;
                    default:
                        color = "#2563EB"; icon = "\xE946";
                        break;
                }

                var toast = new Border
                {
                    Style = (Style)FindResource("ToastBorderStyle"),
                    RenderTransform = new TranslateTransform(),
                    Opacity = 0
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var iconBlock = new TextBlock
                {
                    Text = icon,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                Grid.SetColumn(iconBlock, 0);

                var textBlock = new TextBlock
                {
                    Text = message,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                    FontSize = 13,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetColumn(textBlock, 1);

                grid.Children.Add(iconBlock);
                grid.Children.Add(textBlock);
                toast.Child = grid;

                ToastPanel.Children.Add(toast);

                // Limit to 3 toasts
                while (ToastPanel.Children.Count > 3)
                    ToastPanel.Children.RemoveAt(0);

                // Animate in
                var inStoryboard = (Storyboard)FindResource("ToastInAnimation");
                Storyboard.SetTarget(inStoryboard, toast);
                inStoryboard.Begin();

                // Auto-dismiss after 3s
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    Dispatcher.Invoke(() =>
                    {
                        if (!ToastPanel.Children.Contains(toast)) return;
                        var outStoryboard = (Storyboard)FindResource("ToastOutAnimation");
                        Storyboard.SetTarget(outStoryboard, toast);
                        outStoryboard.Completed += (s, args) =>
                        {
                            if (ToastPanel.Children.Contains(toast))
                                ToastPanel.Children.Remove(toast);
                        };
                        outStoryboard.Begin();
                    });
                });
            });
        }

        // --- 导航 ---
        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavListBox == null || !IsLoaded) return;
            ShowPage(NavListBox.SelectedIndex);
        }

        private void ShowPage(int index)
        {
            if (PageGeneral == null || PagePPTOpt == null || PageAbout == null) return;

            PageGeneral.Visibility = Visibility.Collapsed;
            PagePPTOpt.Visibility = Visibility.Collapsed;
            PageAbout.Visibility = Visibility.Collapsed;

            switch (index)
            {
                case 0: PagePPTOpt.Visibility = Visibility.Visible; break;
                case 1: PageGeneral.Visibility = Visibility.Visible; break;
                case 2: PageAbout.Visibility = Visibility.Visible; break;
            }
        }

        // --- 无边框标题栏 ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                    return;
                }

                DragMove();
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void OnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        // --- 自启动 ---
        private void UpdateAutoStart(bool enable)
        {
            try
            {
                string appPath = Environment.ProcessPath;
                using (RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        if (enable) rk.SetValue("CWS", $"\"{appPath}\"");
                        else rk.DeleteValue("CWS", false);
                    }
                }
                ShowToast(enable ? "Auto-start enabled" : "Auto-start disabled", "success");
            }
            catch { ShowToast("Permission denied", "error"); }
        }

        private void chkRunAtStartup_Checked(object sender, RoutedEventArgs e) => UpdateAutoStart(true);
        private void chkRunAtStartup_Unchecked(object sender, RoutedEventArgs e) => UpdateAutoStart(false);

        private void StartupMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isFloating = rbStartupFloatingBall?.IsChecked == true;
            Properties.Settings.Default.StartAsFloating = isFloating;
            Properties.Settings.Default.Save();

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\CWS"))
                {
                    key?.SetValue("StartAsFloating", isFloating ? 1 : 0);
                }
            }
            catch { }
        }

        // --- 文件关联 ---
        private void btnSetPPT_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Associating with PowerPoint...", "info");
            FileAssociationScanner.AutoFixAssociation(false);
            ShowToast("Associated with PowerPoint", "success");
            Logger.Info("File association switched to PowerPoint (all PPT formats)");
        }

        private void btnSetWPS_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Associating with WPS...", "info");
            FileAssociationScanner.AutoFixAssociation(true);
            ShowToast("Associated with WPS Office", "success");
            Logger.Info("File association switched to WPS (all PPT formats)");
        }

        // --- 服务与进程管理 ---
        private void btnRestartPPTService_Click(object sender, RoutedEventArgs e)
        {
            RestartPPTService();
            ShowToast("PPTService restarted", "success");
            Logger.Info("PPTService restart requested");
        }

        private void btnCleanWPS_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Cleaning...", "info");
            Logger.Info("WPS cleanup started");
            Task.Run(() => {
                string[] procs = { "wps", "et", "wpp", "PPTService" };
                foreach (var name in procs)
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { p.Kill(); p.WaitForExit(1000); } catch { }
                    }
                }
                RestartPPTService();
                ShowToast("Cleanup completed", "success");
                Logger.Info("WPS cleanup completed");
            });
        }

        private void RestartPPTService()
        {
            try
            {
                foreach (var p in Process.GetProcessesByName("PPTService")) p.Kill();
                string servicePath = @"C:\Program Files (x86)\Seewo\PPTService\Main\PPTService.exe";
                if (File.Exists(servicePath)) Process.Start(servicePath);
            }
            catch { }
        }

        private void btnRestartExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tip = Application.Current.TryFindResource("Lang_Status_RestartingExp")?.ToString() ?? "Restarting Explorer...";
                string done = Application.Current.TryFindResource("Lang_Status_RestartDone")?.ToString() ?? "Restart Done!";
                ShowToast(tip, "info");
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                    process.WaitForExit();
                }
                Process.Start("explorer.exe");
                ShowToast(done, "success");
                Logger.Info("Explorer restarted for icon refresh");
            }
            catch (Exception ex) { ShowToast("Error: " + ex.Message, "error"); Logger.Error($"Explorer restart failed: {ex.Message}"); }
        }

        private void btnCleanIconCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tip = Application.Current.TryFindResource("Lang_Status_CleaningCache")?.ToString() ?? "Cleaning Cache...";
                string done = Application.Current.TryFindResource("Lang_Status_CleanDone")?.ToString() ?? "Clean Done!";
                ShowToast(tip, "info");
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                    process.WaitForExit();
                }
                string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IconCache.db");
                if (File.Exists(cachePath)) { try { File.Delete(cachePath); } catch { } }
                Process.Start("explorer.exe");
                ShowToast(done, "success");
                Logger.Info("Icon cache cleaned and explorer restarted");
            }
            catch (Exception ex) { ShowToast("Failed: " + ex.Message, "error"); Process.Start("explorer.exe"); Logger.Error($"Icon cache clean failed: {ex.Message}"); }
        }

        // --- PPT 监控 ---
        private void ChkMonitorPPT_Checked(object sender, RoutedEventArgs e)
        {
            _pptMonitorCts = new CancellationTokenSource();
            Task.Run(() => MonitorPptAsync(_pptMonitorCts.Token));
            ShowToast("Monitor started", "info");
        }

        private void ChkMonitorPPT_Unchecked(object sender, RoutedEventArgs e)
        {
            _pptMonitorCts?.Cancel();
            ShowToast("Monitor stopped", "info");
        }

        private async Task MonitorPptAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (Process.GetProcessesByName("POWERPNT").Length > 0) RestartPPTService();
                try { await Task.Delay(_pptMonitorIntervalMs, ct); } catch { break; }
            }
        }

        // --- 悬浮球 ---
        private void btnSwitchToFloating_Click(object sender, RoutedEventArgs e)
        {
            _isSwitchingToFloating = true;
            this.Hide();
            _floatingBall.Left = SystemParameters.WorkArea.Width - 120;
            _floatingBall.Top = SystemParameters.WorkArea.Height - 120;
            _floatingBall.Topmost = true;
            _floatingBall.Show();
            _isSwitchingToFloating = false;
            Logger.Info("Switched to floating ball mode");
        }

        private void RestoreFromFloatingBall()
        {
            _floatingBall.Hide();
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            Logger.Info("Restored main window from floating ball");
        }

        // --- 配置导入导出 ---
        private void btnExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Export Configuration",
                Filter = "CWS Config Files (*.cwsconfig)|*.cwsconfig|All Files (*.*)|*.*",
                DefaultExt = ".cwsconfig",
                FileName = "CWS_Config.cwsconfig"
            };
            if (dlg.ShowDialog() == true)
            {
                ConfigManager.ExportConfig(dlg.FileName);
                Logger.Info("Configuration exported");
                ShowToast("Config exported", "success");
            }
        }

        private void btnImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Import Configuration",
                Filter = "CWS Config Files (*.cwsconfig)|*.cwsconfig|All Files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                if (ConfigManager.ImportConfig(dlg.FileName))
                {
                    Logger.Info("Configuration imported");
                    ShowToast(Application.Current.TryFindResource("Lang_Log_ConfigImported")?.ToString() ?? "Config imported. Some changes may require restart.", "success");
                }
                else
                {
                    ShowToast("Import failed - invalid config file", "error");
                }
            }
        }

        // --- 自动更新检查 ---
        private string? _latestReleaseUrl = null;

        private async void btnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            btnCheckUpdate.IsEnabled = false;
            btnCheckUpdate.Content = "...";
            Logger.Info("Checking for updates...");

            var (hasUpdate, latestVersion, releaseUrl) = await UpdateChecker.CheckForUpdateAsync();

            btnCheckUpdate.IsEnabled = true;
            btnCheckUpdate.Content = Application.Current.TryFindResource("Lang_About_CheckUpdate")?.ToString() ?? "Check for Updates";

            if (hasUpdate && latestVersion != null)
            {
                string newVer = Application.Current.TryFindResource("Lang_About_NewVersion")?.ToString() ?? "New version available";
                txtUpdateStatus.Text = $"{newVer}: v{latestVersion}";
                txtUpdateStatus.Visibility = Visibility.Visible;
                _latestReleaseUrl = releaseUrl;
                Logger.Info($"Update available: v{latestVersion}");
            }
            else
            {
                string upToDate = Application.Current.TryFindResource("Lang_About_UpToDate")?.ToString() ?? "Up to date";
                txtUpdateStatus.Text = $"{upToDate} (v{UpdateChecker.CurrentVersion})";
                txtUpdateStatus.Visibility = Visibility.Visible;
                _latestReleaseUrl = null;
            }
        }

        private void txtUpdateStatus_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_latestReleaseUrl))
            {
                try { Process.Start(new ProcessStartInfo(_latestReleaseUrl) { UseShellExecute = true }); }
                catch { }
            }
        }

        // --- 诊断日志导出 ---
        private void btnExportLogs_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = Application.Current.TryFindResource("Lang_Btn_ExportLogs")?.ToString() ?? "Export Diagnostic Logs",
                Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*",
                DefaultExt = ".log",
                FileName = $"cws-diagnostics-{DateTime.Now:yyyy-MM-dd}.log"
            };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, Logger.ExportLogs());
                ShowToast(Application.Current.TryFindResource("Lang_Log_LogExported")?.ToString() ?? "Logs exported", "success");
                Logger.Info("Diagnostic logs exported");
            }
        }

        // --- 切换回经典界面 ---
        private void btnSwitchToClassic_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.UseModernUI = false;
            Properties.Settings.Default.Save();
            Logger.Info("Switching to classic UI");

            var classicWindow = new MainWindow();
            classicWindow.Show();

            _isSwitchingToFloating = true;
            this.Close();
        }

        // --- 窗口事件 ---
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (comboLang?.SelectedItem is ListBoxItem selected && selected.Tag != null)
            {
                ApplyLanguage(selected.Tag.ToString()!);
            }

            if (Properties.Settings.Default.StartAsFloating)
                rbStartupFloatingBall.IsChecked = true;
            else
                rbStartupMainWindow.IsChecked = true;

            if (Application.Current.Properties["StartMinimized"] is true)
            {
                Application.Current.Properties["StartMinimized"] = false;
                _isSwitchingToFloating = true;
                this.Hide();
                _floatingBall.Left = SystemParameters.WorkArea.Width - 120;
                _floatingBall.Top = SystemParameters.WorkArea.Height - 120;
                _floatingBall.Topmost = true;
                _floatingBall.Show();
                _isSwitchingToFloating = false;
                Logger.Info("Started in floating ball mode (auto)");
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isSwitchingToFloating) Application.Current.Shutdown();
            base.OnClosing(e);
        }
    }
}
