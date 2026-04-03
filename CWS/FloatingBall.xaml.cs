using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CWS
{
    public partial class FloatingBall : Window
    {
        private bool _isEdgeHidden = false;
        private readonly double _visibleWidth = 15;
        private readonly double _edgeThreshold = 30;
        private DispatcherTimer _hideTimer;

        public FloatingBall()
        {
            InitializeComponent();
            CreateContextMenu();

            _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            _hideTimer.Tick += (s, e) => {
                _hideTimer.Stop();
                CheckAndHideToEdge();
            };

            this.MouseEnter += (s, e) => ShowFromEdge();
            this.MouseLeave += (s, e) => StartHideTimerIfNearEdge();
            this.Topmost = true;
        }

        private async void RootGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (MainEllipse != null)
            {
                var oldStroke = MainEllipse.Stroke;
                MainEllipse.Stroke = Brushes.White;
                await Task.Delay(80);
                MainEllipse.Stroke = oldStroke;
            }

            if (e.ClickCount == 2)
            {
                RestoreToMain();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                _hideTimer.Stop();
                this.BeginAnimation(Window.LeftProperty, null);

                try
                {
                    this.DragMove();
                }
                catch { }
                HandleAfterDrag();
            }
        }

        private void HandleAfterDrag()
        {
            double screenWidth = SystemParameters.WorkArea.Width;

            bool isNearEdge = this.Left < _edgeThreshold || (this.Left + this.Width) > (screenWidth - _edgeThreshold);

            if (!isNearEdge)
            {
                _hideTimer.Stop();
                _isEdgeHidden = false;
                this.Opacity = 1.0;
                this.BeginAnimation(Window.LeftProperty, null);
            }
            else
            {
                _hideTimer.Start();
            }
        }

        private void StartHideTimerIfNearEdge()
        {
            if (_isEdgeHidden) return;

            double screenWidth = SystemParameters.WorkArea.Width;
            bool isNearEdge = this.Left < _edgeThreshold || (this.Left + this.Width) > (screenWidth - _edgeThreshold);

            if (isNearEdge) _hideTimer.Start();
            else { _hideTimer.Stop(); _isEdgeHidden = false; }
        }

        private void ShowFromEdge()
        {
            _hideTimer.Stop();

            double screenWidth = SystemParameters.WorkArea.Width;
            bool isNearEdge = this.Left < _edgeThreshold || (this.Left + this.Width) > (screenWidth - _edgeThreshold);
            if (!isNearEdge)
            {
                _isEdgeHidden = false;
                this.Opacity = 1.0;
                this.BeginAnimation(Window.LeftProperty, null);
                return;
            }

            if (!_isEdgeHidden) return;

            double targetLeft = (this.Left < 0) ? 0 : screenWidth - this.Width;
            AnimateMove(targetLeft);
            _isEdgeHidden = false;
            this.Opacity = 1.0;
        }

        private void CheckAndHideToEdge()
        {
            double screenWidth = SystemParameters.WorkArea.Width;
            double targetLeft = this.Left;
            bool shouldHide = false;

            if (this.Left < _edgeThreshold)
            {
                targetLeft = -this.Width + _visibleWidth;
                shouldHide = true;
            }
            else if (this.Left + this.Width > screenWidth - _edgeThreshold)
            {
                targetLeft = screenWidth - _visibleWidth;
                shouldHide = true;
            }

            if (shouldHide)
            {
                AnimateMove(targetLeft);
                _isEdgeHidden = true;
                this.Opacity = 0.7;
            }
        }

        private void AnimateMove(double targetLeft)
        {
            DoubleAnimation anim = new DoubleAnimation
            {
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(Window.LeftProperty, anim);
        }

        private void RestoreToMain()
        {
            if (Application.Current.MainWindow is MainWindow main)
            {
                main.Show();
                main.WindowState = WindowState.Normal;
                main.Activate();
                this.Hide();
            }
        }

        private void CreateContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            MenuItem showItem = new MenuItem();
            showItem.SetResourceReference(MenuItem.HeaderProperty, "Lang_Menu_ShowMain");
            showItem.Click += (s, e) => RestoreToMain();

            MenuItem autoStartItem = new MenuItem { IsCheckable = true, IsChecked = Properties.Settings.Default.IsAutoStart };
            autoStartItem.SetResourceReference(MenuItem.HeaderProperty, "Lang_Menu_AutoStart");
            autoStartItem.Click += (s, e) => UpdateAutoStartFromBall(autoStartItem.IsChecked);

            MenuItem pptSwitchItem = new MenuItem();
            pptSwitchItem.SetResourceReference(MenuItem.HeaderProperty, "Lang_Menu_PPTSwitch");

            MenuItem toWps = new MenuItem();
            toWps.SetResourceReference(MenuItem.HeaderProperty, "Lang_Menu_SetWPS");
            toWps.Click += (s, e) => Task.Run(() => FileAssociationScanner.AutoFixAssociation(true));

            MenuItem toOffice = new MenuItem();
            toOffice.SetResourceReference(MenuItem.HeaderProperty, "Lang_Menu_SetOffice");
            toOffice.Click += (s, e) => Task.Run(() => FileAssociationScanner.AutoFixAssociation(false));

            pptSwitchItem.Items.Add(toWps);
            pptSwitchItem.Items.Add(toOffice);

            MenuItem exitItem = new MenuItem();
            exitItem.SetResourceReference(MenuItem.HeaderProperty, "Lang_Menu_Exit");
            exitItem.Click += (s, e) => Application.Current.Shutdown();

            menu.Items.Add(showItem);
            menu.Items.Add(autoStartItem);
            menu.Items.Add(pptSwitchItem);
            menu.Items.Add(exitItem);

            this.ContextMenu = menu;
        }

        private void UpdateAutoStartFromBall(bool enable)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                string? appPath = currentProcess.MainModule?.FileName;
                if (string.IsNullOrEmpty(appPath)) return;
                using (var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        if (enable) rk.SetValue("CWS", $"\"{appPath}\" --autostart");
                        else rk.DeleteValue("CWS", false);
                    }
                }
                Properties.Settings.Default.IsAutoStart = enable;
                Properties.Settings.Default.Save();
                if (Application.Current.MainWindow is MainWindow main)
                {
                    var chk = main.FindName("chkRunAtStartup") as CheckBox;
                    if (chk != null) chk.IsChecked = enable;
                }
            }
            catch (Exception ex) { MessageBox.Show($"同步自啟動失敗: {ex.Message}"); }
        }
    }
}