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
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using CWS.Services;

namespace CWS
{
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _pptMonitorCts;
        private readonly int _pptMonitorIntervalMs = 5000;
        private FloatingBall _floatingBall;
        private bool _isSwitchingToFloating = false;

        public MainWindow()
        {
            InitializeComponent();

            // 初始化懸浮球
            _floatingBall = new FloatingBall();
            _floatingBall.MouseDoubleClick += (s, e) => RestoreFromFloatingBall();

            // 應用背景
            ApplySavedBackground();
        }

        // --- 語言管理 (優先本地外部文件) ---

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
                Debug.WriteLine($"切換語言失敗: {ex.Message}");
                Logger.Error($"Language switch failed: {ex.Message}");
            }
        }

        private void comboLang_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (comboLang?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                ApplyLanguage(selectedItem.Tag.ToString()!);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (comboLang?.SelectedItem is ComboBoxItem selected && selected.Tag != null)
            {
                ApplyLanguage(selected.Tag.ToString()!);
            }

            // 初始化启动模式 RadioButton
            if (Properties.Settings.Default.StartAsFloating)
                rbStartupFloatingBall.IsChecked = true;
            else
                rbStartupMainWindow.IsChecked = true;

            if (FindResource("WindowOnLoadStoryboard") is Storyboard sb)
            {
                sb.Begin();
            }

            // 處理啟動時直接進入懸浮球模式
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

        // --- 背景管理 ---

        private void ApplySavedBackground()
        {
            try
            {
                string? bgPath = Properties.Settings.Default.BackgroundImagePath;
                if (!string.IsNullOrEmpty(bgPath) && File.Exists(bgPath))
                {
                    ApplyBackground(bgPath);
                }
            }
            catch { }
        }

        private void ApplyBackground(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(path);
                bitmap.EndInit();

                ImageBrush brush = new ImageBrush(bitmap)
                {
                    Stretch = Stretch.UniformToFill,
                    AlignmentX = AlignmentX.Center,
                    AlignmentY = AlignmentY.Center,
                    Opacity = sldBgOpacity?.Value ?? Properties.Settings.Default.BgOpacity
                };

                RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
                MainBorder.Background = brush;
            }
            catch { RemoveBackground(); }
        }

        private void btnBrowseBackground_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Title = "選擇背景圖片",
                Filter = "圖片文件|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedPath = openFileDialog.FileName;
                BgEditorWindow editorWindow = new BgEditorWindow(selectedPath) { Owner = this };

                if (editorWindow.ShowDialog() == true && editorWindow.ResultBrush != null)
                {
                    ImageBrush finalBrush = editorWindow.ResultBrush;
                    finalBrush.Opacity = sldBgOpacity?.Value ?? 0.3;
                    MainBorder.Background = finalBrush;

                    Properties.Settings.Default.BackgroundImagePath = selectedPath;
                    Properties.Settings.Default.Save();
                }
            }
        }

        private void sldBgOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MainBorder?.Background is ImageBrush brush)
            {
                brush.Opacity = e.NewValue;
                if (txtOpacityPercent != null) txtOpacityPercent.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void btnRemoveBackground_Click(object sender, RoutedEventArgs e)
        {
            RemoveBackground();
            Properties.Settings.Default.BackgroundImagePath = "";
            Properties.Settings.Default.Save();
        }

        private void RemoveBackground()
        {
            MainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#05101E"));
        }

        // --- 視窗交互與導覽 ---

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed) this.DragMove();
        }

        private void SetStatus(string text)
        {
            Dispatcher.Invoke(() => { if (txtStatus != null) txtStatus.Text = text; });
        }

        private void Nav_General_Click(object sender, RoutedEventArgs e) => ShowPage(PageGeneral);
        private void Nav_PPTOpt_Click(object sender, RoutedEventArgs e) => ShowPage(PagePPTOpt);
        private void Nav_About_Click(object sender, RoutedEventArgs e) => ShowPage(PageAbout);

        private void ShowPage(StackPanel activePage)
        {
            if (PageGeneral == null || PagePPTOpt == null || PageAbout == null) return;
            PageGeneral.Visibility = Visibility.Collapsed;
            PagePPTOpt.Visibility = Visibility.Collapsed;
            PageAbout.Visibility = Visibility.Collapsed;
            if (activePage != null) activePage.Visibility = Visibility.Visible;
        }

        // 1. 自啟動邏輯
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
                SetStatus(enable ? "自啟動已開啟" : "自啟動已取消");
            }
            catch { SetStatus("權限不足"); }
        }

        private void chkRunAtStartup_Checked(object sender, RoutedEventArgs e) => UpdateAutoStart(true);
        private void chkRunAtStartup_Unchecked(object sender, RoutedEventArgs e) => UpdateAutoStart(false);

        private void StartupMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isFloating = rbStartupFloatingBall?.IsChecked == true;
            Properties.Settings.Default.StartAsFloating = isFloating;
            Properties.Settings.Default.Save();

            // 同步到註冊表
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\CWS"))
                {
                    key?.SetValue("StartAsFloating", isFloating ? 1 : 0);
                }
            }
            catch { }
        }

        // 2. 關聯修復邏輯
        private void btnSetPPT_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在掃描並關聯...");
            FileAssociationScanner.AutoFixAssociation(false);
            SetStatus("已關聯 PowerPoint");
            Logger.Info("File association switched to PowerPoint (all PPT formats)");
        }

        private void btnSetWPS_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在掃描並關聯...");
            FileAssociationScanner.AutoFixAssociation(true);
            SetStatus("已關聯 WPS Office");
            Logger.Info("File association switched to WPS (all PPT formats)");
        }

        // 3. 服務清理與重啟邏輯
        private void btnRestartPPTService_Click(object sender, RoutedEventArgs e)
        {
            RestartPPTService();
            SetStatus("服務已嘗試重啟");
            Logger.Info("PPTService restart requested");
        }

        private void btnCleanWPS_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在清理...");
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
                SetStatus("清理完成");
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

        // 4. PPT 監控邏輯
        private void ChkMonitorPPT_Checked(object sender, RoutedEventArgs e)
        {
            _pptMonitorCts = new CancellationTokenSource();
            Task.Run(() => MonitorPptAsync(_pptMonitorCts.Token));
            SetStatus("監控已啟動");
        }

        private void ChkMonitorPPT_Unchecked(object sender, RoutedEventArgs e)
        {
            _pptMonitorCts?.Cancel();
            SetStatus("監控已停止");
        }

        private async Task MonitorPptAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (Process.GetProcessesByName("POWERPNT").Length > 0) RestartPPTService();
                try { await Task.Delay(_pptMonitorIntervalMs, ct); } catch { break; }
            }
        }

        // 5. 懸浮球與系統工具
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

        private void btnRestartExplorer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tip = Application.Current.TryFindResource("Lang_Status_RestartingExp")?.ToString() ?? "Restarting Explorer...";
                string done = Application.Current.TryFindResource("Lang_Status_RestartDone")?.ToString() ?? "Restart Done!";
                SetStatus(tip);
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                    process.WaitForExit();
                }
                Process.Start("explorer.exe");
                SetStatus(done);
                Logger.Info("Explorer restarted for icon refresh");
            }
            catch (Exception ex) { SetStatus("Error: " + ex.Message); Logger.Error($"Explorer restart failed: {ex.Message}"); }
        }

        private void btnCleanIconCache_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string tip = Application.Current.TryFindResource("Lang_Status_CleaningCache")?.ToString() ?? "Cleaning Cache...";
                string done = Application.Current.TryFindResource("Lang_Status_CleanDone")?.ToString() ?? "Clean Done!";
                SetStatus(tip);
                foreach (var process in Process.GetProcessesByName("explorer"))
                {
                    process.Kill();
                    process.WaitForExit();
                }
                string cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IconCache.db");
                if (File.Exists(cachePath)) { try { File.Delete(cachePath); } catch { } }
                Process.Start("explorer.exe");
                SetStatus(done);
                Logger.Info("Icon cache cleaned and explorer restarted");
            }
            catch (Exception ex) { SetStatus("Failed: " + ex.Message); Process.Start("explorer.exe"); Logger.Error($"Icon cache clean failed: {ex.Message}"); }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void OnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        // --- 診斷日誌導出 ---
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
                SetStatus(Application.Current.TryFindResource("Lang_Log_LogExported")?.ToString() ?? "Logs exported");
                Logger.Info("Diagnostic logs exported");
            }
        }

        // --- 配置導入導出 ---
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
                SetStatus("Config exported");
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
                    SetStatus(Application.Current.TryFindResource("Lang_Log_ConfigImported")?.ToString() ?? "Config imported. Some changes may require restart.");
                }
                else
                {
                    SetStatus("Import failed - invalid config file");
                }
            }
        }

        // --- 自動更新檢查 ---
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

        // --- 切換到 Material Design 界面 ---
        private void btnSwitchToModern_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.UseModernUI = true;
            Properties.Settings.Default.Save();
            Logger.Info("Switching to Material Design UI");

            var modernWindow = new ModernWindow();
            modernWindow.Show();

            _isSwitchingToFloating = true;
            this.Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isSwitchingToFloating) Application.Current.Shutdown();
            base.OnClosing(e);
        }
    }
}