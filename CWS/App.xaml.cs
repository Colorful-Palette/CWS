using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Runtime.InteropServices;
using Microsoft.Win32;

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
            if (isAutoStart || startAsFloating)
            {
                this.Properties["StartMinimized"] = true;
            }

            base.OnStartup(e);
        }

        private void ActivateExistingWindow()
        {
            IntPtr hWnd = FindWindow(null, "CWS 控制中心");
            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}