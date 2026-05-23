using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using CWS.Services;

namespace CWS
{
    public partial class App : Application
    {
        // 全域唯一的 Mutex ID
        private static Mutex? _mutex = null;
        private const string AppGuid = "CWS-Assistant-Unique-Mutex-99123";

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        private const int SW_RESTORE = 9;

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("CWS application starting");

            _mutex = new Mutex(true, AppGuid, out bool createdNew);

            if (!createdNew)
            {
                ActivateExistingWindow();
                Environment.Exit(0);
                return;
            }
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            Directory.SetCurrentDirectory(baseDirectory);

            bool isAutoStart = e.Args.Contains("--autostart");
            bool startAsFloating = false;
            try
            {
                using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\CWS"))
                {
                    if (key != null)
                    {
                        startAsFloating = (int)key.GetValue("StartAsFloating", 0) == 1;
                    }
                }
            }
            catch { /* 忽略讀取錯誤 */ }

            // 也從 Properties.Settings 讀取
            if (!startAsFloating)
            {
                try { startAsFloating = CWS.Properties.Settings.Default.StartAsFloating; }
                catch { }
            }

            if (isAutoStart || startAsFloating)
            {
                this.Properties["StartMinimized"] = true;
                Logger.Info("Starting in minimized/floating mode");
            }

            // 根據設定選擇啟動窗口
            bool useModern = false;
            try { useModern = CWS.Properties.Settings.Default.UseModernUI; } catch { }

            Window mainWindow;
            if (useModern)
            {
                mainWindow = new ModernWindow();
                Logger.Info("Starting with Material Design UI");
            }
            else
            {
                mainWindow = new MainWindow();
            }
            mainWindow.Show();
        }

        private void ActivateExistingWindow()
        {
            IntPtr hWnd = FindWindow(null, "CWS 控制中心");
            if (hWnd == IntPtr.Zero)
                hWnd = FindWindow(null, "CWS Control Center");
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Info("CWS application exiting");
            if (_mutex != null)
            {
                try { _mutex.ReleaseMutex(); } catch { }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}