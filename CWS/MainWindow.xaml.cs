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

            if (FindResource("WindowOnLoadStoryboard") is Storyboard sb)
            {
                sb.Begin();
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

        // 2. 關聯修復邏輯
        private void btnSetPPT_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在掃描並關聯...");
            FileAssociationScanner.AutoFixAssociation(false);
            SetStatus("已關聯 PowerPoint");
        }

        private void btnSetWPS_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在掃描並關聯...");
            FileAssociationScanner.AutoFixAssociation(true);
            SetStatus("已關聯 WPS Office");
        }

        // 3. 服務清理與重啟邏輯
        private void btnRestartPPTService_Click(object sender, RoutedEventArgs e)
        {
            RestartPPTService();
            SetStatus("服務已嘗試重啟");
        }

        private void btnCleanWPS_Click(object sender, RoutedEventArgs e)
        {
            SetStatus("正在清理...");
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
        }

        private void RestoreFromFloatingBall()
        {
            _floatingBall.Hide();
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
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
            }
            catch (Exception ex) { SetStatus("Error: " + ex.Message); }
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
            }
            catch (Exception ex) { SetStatus("Failed: " + ex.Message); Process.Start("explorer.exe"); }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;
        private void OnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isSwitchingToFloating) Application.Current.Shutdown();
            base.OnClosing(e);
        }
    }
}