using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using CWS.Services;
using Brush = System.Windows.Media.Brush;

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

            txtVersionDisplay.Text = $"CWS {UpdateChecker.CurrentVersion}";
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
                    Foreground = GetBrush("TextPrimaryBrush", "#1E293B"),
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
            if (PageGeneral == null || PagePPTOpt == null || PageAssociationCenter == null || PageThemeUrl == null || PageAbout == null) return;

            PageGeneral.Visibility = Visibility.Collapsed;
            PagePPTOpt.Visibility = Visibility.Collapsed;
            PageAssociationCenter.Visibility = Visibility.Collapsed;
            PageThemeUrl.Visibility = Visibility.Collapsed;
            PageAbout.Visibility = Visibility.Collapsed;

            switch (index)
            {
                case 0: PagePPTOpt.Visibility = Visibility.Visible; break;
                case 1: PageGeneral.Visibility = Visibility.Visible; break;
                case 2: PageAssociationCenter.Visibility = Visibility.Visible; break;
                case 3: PageThemeUrl.Visibility = Visibility.Visible; break;
                case 4: PageAbout.Visibility = Visibility.Visible; break;
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
        private static readonly string[] WordSuffixDefaults = { ".doc", ".docx", ".docm", ".dot", ".dotx", ".dotm" };
        private static readonly string[] ExcelSuffixDefaults = { ".xls", ".xlsx", ".xlsm", ".xlt", ".xltx", ".xltm" };
        private static readonly string[] PdfSuffixDefaults = { ".pdf" };

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

        private void btnSetPptQuickMenu_Click(object sender, RoutedEventArgs e)
        {
            ToggleQuickMenu(PptQuickMenu);
        }

        private void btnSetPptOfficeQuick_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Associating with PowerPoint...", "info");
            FileAssociationScanner.AutoFixAssociation(false);
            ShowToast("Associated with PowerPoint", "success");
            PptQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetPptWpsQuick_Click(object sender, RoutedEventArgs e)
        {
            ShowToast("Associating with WPS...", "info");
            FileAssociationScanner.AutoFixAssociation(true);
            ShowToast("Associated with WPS Office", "success");
            PptQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetWordQuick_Click(object sender, RoutedEventArgs e)
        {
            ToggleQuickMenu(WordQuickMenu);
        }

        private void btnSetExcelQuick_Click(object sender, RoutedEventArgs e)
        {
            ToggleQuickMenu(ExcelQuickMenu);
        }

        private void btnSetPdfQuick_Click(object sender, RoutedEventArgs e)
        {
            ToggleQuickMenu(PdfQuickMenu);
        }

        private void btnSetWordOfficeQuick_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickAssociation(FileAssociationScanner.AssociationCategory.Word, FileAssociationScanner.AssociationTarget.Office, WordSuffixDefaults, "Word");
            WordQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetWordWpsQuick_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickAssociation(FileAssociationScanner.AssociationCategory.Word, FileAssociationScanner.AssociationTarget.Wps, WordSuffixDefaults, "Word");
            WordQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetExcelOfficeQuick_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickAssociation(FileAssociationScanner.AssociationCategory.Excel, FileAssociationScanner.AssociationTarget.Office, ExcelSuffixDefaults, "Excel");
            ExcelQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetExcelWpsQuick_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickAssociation(FileAssociationScanner.AssociationCategory.Excel, FileAssociationScanner.AssociationTarget.Wps, ExcelSuffixDefaults, "Excel");
            ExcelQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetPdfEdgeQuick_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickAssociation(FileAssociationScanner.AssociationCategory.Pdf, FileAssociationScanner.AssociationTarget.Edge, PdfSuffixDefaults, "PDF");
            PdfQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void btnSetPdfWpsQuick_Click(object sender, RoutedEventArgs e)
        {
            ApplyQuickAssociation(FileAssociationScanner.AssociationCategory.Pdf, FileAssociationScanner.AssociationTarget.Wps, PdfSuffixDefaults, "PDF");
            PdfQuickMenu.Visibility = Visibility.Collapsed;
        }

        private void ToggleQuickMenu(Border target)
        {
            bool openTarget = target.Visibility != Visibility.Visible;

            PptQuickMenu.Visibility = Visibility.Collapsed;
            WordQuickMenu.Visibility = Visibility.Collapsed;
            ExcelQuickMenu.Visibility = Visibility.Collapsed;
            PdfQuickMenu.Visibility = Visibility.Collapsed;

            target.Visibility = openTarget ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyQuickAssociation(
            FileAssociationScanner.AssociationCategory category,
            FileAssociationScanner.AssociationTarget target,
            IEnumerable<string> suffixes,
            string label)
        {
            if (!FileAssociationScanner.IsAdmin())
            {
                ShowToast("请以管理员身份运行后再应用关联", "error");
                return;
            }

            var result = FileAssociationScanner.ApplyAssociations(category, target, suffixes);
            ShowToast(result.failed == 0 ? $"{label} 关联已切换" : $"{label} 切换部分失败：{result.failed}", result.failed == 0 ? "success" : "warning");
        }

        private void btnApplyAssociations_Click(object sender, RoutedEventArgs e)
        {
            if (!FileAssociationScanner.IsAdmin())
            {
                ShowToast("请以管理员身份运行后再应用关联", "error");
                return;
            }

            SaveAssociationSettings();

            var wordTarget = ParseTargetFromCombo(cmbWordTarget, FileAssociationScanner.AssociationTarget.Office);
            var excelTarget = ParseTargetFromCombo(cmbExcelTarget, FileAssociationScanner.AssociationTarget.Office);
            var pdfTarget = ParseTargetFromCombo(cmbPdfTarget, FileAssociationScanner.AssociationTarget.Edge);

            int success = 0;
            int failed = 0;

            var wordResult = FileAssociationScanner.ApplyAssociations(
                FileAssociationScanner.AssociationCategory.Word,
                wordTarget,
                GetSelectedWordSuffixes());
            success += wordResult.success;
            failed += wordResult.failed;

            var excelResult = FileAssociationScanner.ApplyAssociations(
                FileAssociationScanner.AssociationCategory.Excel,
                excelTarget,
                GetSelectedExcelSuffixes());
            success += excelResult.success;
            failed += excelResult.failed;

            var pdfResult = FileAssociationScanner.ApplyAssociations(
                FileAssociationScanner.AssociationCategory.Pdf,
                pdfTarget,
                GetSelectedPdfSuffixes());
            success += pdfResult.success;
            failed += pdfResult.failed;

            if (failed == 0)
            {
                ShowToast($"关联应用完成（{success} 项）", "success");
            }
            else
            {
                ShowToast($"关联完成：成功 {success}，失败 {failed}", "warning");
            }

            Logger.Info($"Apply associations result: success={success}, failed={failed}");
        }

        private FileAssociationScanner.AssociationTarget ParseTargetFromCombo(ComboBox comboBox, FileAssociationScanner.AssociationTarget fallback)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return tag switch
                {
                    "Office" => FileAssociationScanner.AssociationTarget.Office,
                    "WPS" => FileAssociationScanner.AssociationTarget.Wps,
                    "Edge" => FileAssociationScanner.AssociationTarget.Edge,
                    _ => fallback
                };
            }

            return fallback;
        }

        private void WordTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                SetSelectedComboByTag(cmbWordTarget, tag, "Office");
                UpdateAssociationTargetButtonsUI();
            }
        }

        private void ExcelTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                SetSelectedComboByTag(cmbExcelTarget, tag, "Office");
                UpdateAssociationTargetButtonsUI();
            }
        }

        private void PdfTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string tag)
            {
                SetSelectedComboByTag(cmbPdfTarget, tag, "Edge");
                UpdateAssociationTargetButtonsUI();
            }
        }

        private void UpdateAssociationTargetButtonsUI()
        {
            string wordTarget = (cmbWordTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Office";
            string excelTarget = (cmbExcelTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Office";
            string pdfTarget = (cmbPdfTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Edge";

            ApplyTargetButtonState(btnWordOfficeTarget, wordTarget == "Office");
            ApplyTargetButtonState(btnWordWpsTarget, wordTarget == "WPS");

            ApplyTargetButtonState(btnExcelOfficeTarget, excelTarget == "Office");
            ApplyTargetButtonState(btnExcelWpsTarget, excelTarget == "WPS");

            ApplyTargetButtonState(btnPdfEdgeTarget, pdfTarget == "Edge");
            ApplyTargetButtonState(btnPdfWpsTarget, pdfTarget == "WPS");
        }

        private void ApplyTargetButtonState(Button button, bool isSelected)
        {
            button.Background = isSelected
                ? GetBrush("AccentLightBrush", "#EFF6FF")
                : GetBrush("CardBackgroundBrush", "#FFFFFF");

            button.BorderBrush = isSelected
                ? GetBrush("AccentBrush", "#2563EB")
                : GetBrush("BorderLightBrush", "#E2E8F0");

            button.Foreground = isSelected
                ? GetBrush("AccentBrush", "#2563EB")
                : GetBrush("TextPrimaryBrush", "#1E293B");
        }

        private IEnumerable<string> GetSelectedWordSuffixes()
        {
            var map = new Dictionary<string, bool>
            {
                [".doc"] = chkWordDoc.IsChecked == true,
                [".docx"] = chkWordDocx.IsChecked == true,
                [".docm"] = chkWordDocm.IsChecked == true,
                [".dot"] = chkWordDot.IsChecked == true,
                [".dotx"] = chkWordDotx.IsChecked == true,
                [".dotm"] = chkWordDotm.IsChecked == true
            };

            return map.Where(x => x.Value).Select(x => x.Key);
        }

        private IEnumerable<string> GetSelectedExcelSuffixes()
        {
            var map = new Dictionary<string, bool>
            {
                [".xls"] = chkExcelXls.IsChecked == true,
                [".xlsx"] = chkExcelXlsx.IsChecked == true,
                [".xlsm"] = chkExcelXlsm.IsChecked == true,
                [".xlt"] = chkExcelXlt.IsChecked == true,
                [".xltx"] = chkExcelXltx.IsChecked == true,
                [".xltm"] = chkExcelXltm.IsChecked == true
            };

            return map.Where(x => x.Value).Select(x => x.Key);
        }

        private IEnumerable<string> GetSelectedPdfSuffixes()
        {
            if (chkPdfPdf.IsChecked == true) return PdfSuffixDefaults;
            return Array.Empty<string>();
        }

        private static string JoinSuffixes(IEnumerable<string> values)
        {
            return string.Join(";", values.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        private static HashSet<string> ParseSuffixes(string raw, IEnumerable<string> fallback)
        {
            var parsed = (raw ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant())
                .Where(x => x.StartsWith("."))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (parsed.Count > 0) return parsed;
            return fallback.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private void SetSelectedComboByTag(ComboBox comboBox, string tag, string fallbackTag)
        {
            var targetTag = string.IsNullOrWhiteSpace(tag) ? fallbackTag : tag;
            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if ((item.Tag?.ToString() ?? string.Empty).Equals(targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            foreach (var item in comboBox.Items.OfType<ComboBoxItem>())
            {
                if ((item.Tag?.ToString() ?? string.Empty).Equals(fallbackTag, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void SetSelectedListBoxByTag(ListBox listBox, string tag, string fallbackTag)
        {
            var targetTag = string.IsNullOrWhiteSpace(tag) ? fallbackTag : tag;
            foreach (var item in listBox.Items.OfType<ListBoxItem>())
            {
                if ((item.Tag?.ToString() ?? string.Empty).Equals(targetTag, StringComparison.OrdinalIgnoreCase))
                {
                    listBox.SelectedItem = item;
                    return;
                }
            }

            foreach (var item in listBox.Items.OfType<ListBoxItem>())
            {
                if ((item.Tag?.ToString() ?? string.Empty).Equals(fallbackTag, StringComparison.OrdinalIgnoreCase))
                {
                    listBox.SelectedItem = item;
                    return;
                }
            }
        }

        private void LoadAssociationSettings()
        {
            SetSelectedComboByTag(cmbWordTarget, Properties.Settings.Default.AssocWordTarget, "Office");
            SetSelectedComboByTag(cmbExcelTarget, Properties.Settings.Default.AssocExcelTarget, "Office");
            SetSelectedComboByTag(cmbPdfTarget, Properties.Settings.Default.AssocPdfTarget, "Edge");

            var wordSet = ParseSuffixes(Properties.Settings.Default.AssocWordSuffixes, WordSuffixDefaults);
            chkWordDoc.IsChecked = wordSet.Contains(".doc");
            chkWordDocx.IsChecked = wordSet.Contains(".docx");
            chkWordDocm.IsChecked = wordSet.Contains(".docm");
            chkWordDot.IsChecked = wordSet.Contains(".dot");
            chkWordDotx.IsChecked = wordSet.Contains(".dotx");
            chkWordDotm.IsChecked = wordSet.Contains(".dotm");

            var excelSet = ParseSuffixes(Properties.Settings.Default.AssocExcelSuffixes, ExcelSuffixDefaults);
            chkExcelXls.IsChecked = excelSet.Contains(".xls");
            chkExcelXlsx.IsChecked = excelSet.Contains(".xlsx");
            chkExcelXlsm.IsChecked = excelSet.Contains(".xlsm");
            chkExcelXlt.IsChecked = excelSet.Contains(".xlt");
            chkExcelXltx.IsChecked = excelSet.Contains(".xltx");
            chkExcelXltm.IsChecked = excelSet.Contains(".xltm");

            var pdfSet = ParseSuffixes(Properties.Settings.Default.AssocPdfSuffixes, PdfSuffixDefaults);
            chkPdfPdf.IsChecked = pdfSet.Contains(".pdf");

            UpdateAssociationTargetButtonsUI();
        }

        private void SaveAssociationSettings()
        {
            Properties.Settings.Default.AssocWordTarget = (cmbWordTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Office";
            Properties.Settings.Default.AssocExcelTarget = (cmbExcelTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Office";
            Properties.Settings.Default.AssocPdfTarget = (cmbPdfTarget.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Edge";

            Properties.Settings.Default.AssocWordSuffixes = JoinSuffixes(GetSelectedWordSuffixes());
            Properties.Settings.Default.AssocExcelSuffixes = JoinSuffixes(GetSelectedExcelSuffixes());
            Properties.Settings.Default.AssocPdfSuffixes = JoinSuffixes(GetSelectedPdfSuffixes());
            Properties.Settings.Default.Save();
        }

        private bool _isThemeInitializing = false;
        private const string DefaultThemePreset = "MonetWaterLilies";

        private void ThemePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isThemeInitializing) return;
            ApplyThemeAndPersist();
        }

        private void ApplyThemeAndPersist()
        {
            string preset = (lstThemePreset?.SelectedItem as ListBoxItem)?.Tag?.ToString() ?? DefaultThemePreset;
            ApplyTheme(preset);
            Properties.Settings.Default.ThemePreset = preset;
            Properties.Settings.Default.Save();
        }

        private void ApplyTheme(string preset)
        {
            var accent = GetPresetAccent(preset);
            var surface = GetPresetSurface(preset);

            SetBrush("AccentBrush", accent.Accent);
            SetBrush("AccentHoverBrush", accent.Hover);
            SetBrush("AccentPressedBrush", accent.Pressed);
            SetBrush("AccentLightBrush", accent.Light);
            SetBrush("TextOnAccentBrush", accent.TextOnAccent);

            SetBrush("AppBackgroundBrush", surface.App);
            SetBrush("SidebarBackgroundBrush", surface.Sidebar);
            SetBrush("CardBackgroundBrush", surface.Card);
            SetBrush("TitleBarBackgroundBrush", surface.Title);
            SetBrush("TextPrimaryBrush", surface.TextPrimary);
            SetBrush("TextSecondaryBrush", surface.TextSecondary);
            SetBrush("BorderLightBrush", surface.BorderLight);
            SetBrush("BorderCardBrush", surface.BorderCard);
            SetBrush("DividerBrush", surface.Divider);
            SetBrush("WindowFrameBorderBrush", surface.FrameBorder);
            SetBrush("SubtlePanelBrush", surface.SubtlePanel);
            SetBrush("DangerSurfaceBrush", surface.DangerSurface);
            SetBrush("DangerBorderBrush", surface.DangerBorder);
            SetBrush("DangerTextBrush", surface.DangerText);

            RootBackground.BorderBrush = GetBrush("WindowFrameBorderBrush", "#E0E4E8");
            UpdateAssociationTargetButtonsUI();
        }

        private (string Accent, string Hover, string Pressed, string Light, string TextOnAccent) GetPresetAccent(string preset)
        {
            return preset switch
            {
                "MonetSunrise" => ("#E38B4E", "#CC7740", "#A95E2F", "#FBEDE2", "#FFFFFF"),
                "MonetGarden" => ("#6FA67A", "#5D9468", "#4D7E58", "#EAF4EC", "#FFFFFF"),
                "MonetTwilight" => ("#7B78B8", "#6A67A6", "#56538C", "#EEEDFA", "#FFFFFF"),
                _ => ("#5B8FA8", "#4A7C95", "#3D677D", "#E8F1F4", "#FFFFFF")
            };
        }

        private (
            string App,
            string Sidebar,
            string Card,
            string Title,
            string TextPrimary,
            string TextSecondary,
            string BorderLight,
            string BorderCard,
            string Divider,
            string FrameBorder,
            string SubtlePanel,
            string DangerSurface,
            string DangerBorder,
            string DangerText) GetPresetSurface(string preset)
        {
            return preset switch
            {
                "MonetSunrise" => ("#F7F2ED", "#FFF9F4", "#FFFFFF", "#FFF9F4", "#2D241E", "#7C6959", "#EADACB", "#EFDCCF", "#EADACB", "#DFC9B6", "#FAF3EC", "#FFF1EE", "#F5C6BF", "#C2524A"),
                "MonetGarden" => ("#EEF4EF", "#F7FCF8", "#FFFFFF", "#F7FCF8", "#1F2B22", "#5D7263", "#D2E2D6", "#D8E8DC", "#D2E2D6", "#C6D8CB", "#F1F8F3", "#FEF2F2", "#FECACA", "#DC2626"),
                "MonetTwilight" => ("#F1F1F8", "#F8F8FD", "#FFFFFF", "#F8F8FD", "#262739", "#666883", "#D8D9EA", "#DEE0EF", "#D8D9EA", "#CBCDE0", "#F3F4FB", "#FEF2F2", "#FECACA", "#DC2626"),
                _ => ("#EEF3F5", "#F7FAFB", "#FFFFFF", "#F7FAFB", "#1F2F38", "#5B7280", "#D8E4EA", "#DDE8EE", "#D8E4EA", "#CAD9E1", "#F1F6F8", "#FEF2F2", "#FECACA", "#DC2626")
            };
        }

        private Brush GetBrush(string key, string fallbackHex)
        {
            if (TryFindResource(key) is Brush brush) return brush;
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(fallbackHex));
        }

        private void SetBrush(string key, string colorHex)
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);

            if (Resources[key] is SolidColorBrush existing)
            {
                if (existing.IsFrozen)
                {
                    var mutable = existing.Clone();
                    mutable.Color = color;
                    Resources[key] = mutable;
                }
                else
                {
                    existing.Color = color;
                }
            }
            else
            {
                Resources[key] = new SolidColorBrush(color);
            }
        }

        private void btnInvokeUrl_Click(object sender, RoutedEventArgs e)
        {
            string raw = (txtInvokeUrl.Text ?? string.Empty).Trim();
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                ShowToast("URL 无效", "error");
                return;
            }

            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme == "javascript" || scheme == "data")
            {
                ShowToast("不支持该协议", "error");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(raw) { UseShellExecute = true });
                Properties.Settings.Default.LastInvokeUrl = raw;
                Properties.Settings.Default.Save();
                ShowToast("URL 调用成功", "success");
            }
            catch (Exception ex)
            {
                ShowToast($"URL 调用失败: {ex.Message}", "error");
            }
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
                Title = Application.Current.TryFindResource("Lang_Dialog_ExportConfigTitle")?.ToString() ?? "Export Configuration",
                Filter = Application.Current.TryFindResource("Lang_Dialog_ConfigFileFilter")?.ToString() ?? "CWS Config Files (*.cwsconfig)|*.cwsconfig|All Files (*.*)|*.*",
                DefaultExt = ".cwsconfig",
                FileName = Application.Current.TryFindResource("Lang_Dialog_ConfigDefaultFileName")?.ToString() ?? "CWS_Config.cwsconfig"
            };
            if (dlg.ShowDialog() == true)
            {
                ConfigManager.ExportConfig(dlg.FileName);
                Logger.Info("Configuration exported");
                ShowToast(Application.Current.TryFindResource("Lang_Status_ConfigExported")?.ToString() ?? "Config exported", "success");
            }
        }

        private void btnImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = Application.Current.TryFindResource("Lang_Dialog_ImportConfigTitle")?.ToString() ?? "Import Configuration",
                Filter = Application.Current.TryFindResource("Lang_Dialog_ConfigFileFilter")?.ToString() ?? "CWS Config Files (*.cwsconfig)|*.cwsconfig|All Files (*.*)|*.*"
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
                    ShowToast(Application.Current.TryFindResource("Lang_Status_ConfigImportFailed")?.ToString() ?? "Import failed - invalid config file", "error");
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
                txtUpdateStatus.Text = $"{upToDate} ({UpdateChecker.CurrentVersion})";
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

            LoadAssociationSettings();

            _isThemeInitializing = true;
            string preset = string.IsNullOrWhiteSpace(Properties.Settings.Default.ThemePreset)
                ? DefaultThemePreset
                : Properties.Settings.Default.ThemePreset;
            SetSelectedListBoxByTag(lstThemePreset, preset, DefaultThemePreset);
            ApplyTheme(preset);
            _isThemeInitializing = false;

            txtInvokeUrl.Text = Properties.Settings.Default.LastInvokeUrl;

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
