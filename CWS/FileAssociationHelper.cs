using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CWS
{
    public static class FileAssociationScanner
    {
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        // Office 路径
        private const string OFFICE_PPT_EXE = @"C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE";
        private const string FTA_EXE_NAME = "SetUserFTA.exe";
        public static bool AutoFixAssociation(bool toWps)
        {
            if (!IsAdmin()) return false;

            // SetUserFTA 
            string ftaExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FTA_EXE_NAME);

            if (!File.Exists(ftaExePath))
            {
                Debug.WriteLine("错误：找不到 SetUserFTA.exe，请确保它在程序根目录。");
                return false;
            }

            try
            {
                if (toWps)
                {
                    // 切换到 WPS ( WPS ProgID  版本2019)
                    RunSetUserFTA(ftaExePath, ".pptx", "WPP.PPTX.6");
                    RunSetUserFTA(ftaExePath, ".ppt", "WPP.PPT.6");
                    RunSetUserFTA(ftaExePath, ".ppsx", "WPP.PPSX.6");
                    RunSetUserFTA(ftaExePath, ".pps", "WPP.PPS.6");
                    RunSetUserFTA(ftaExePath, ".potx", "WPP.POTX.6");
                    RunSetUserFTA(ftaExePath, ".pot", "WPP.POT.6");
                    RunSetUserFTA(ftaExePath, ".pptm", "WPP.PPTM.6");
                    RunSetUserFTA(ftaExePath, ".ppsm", "WPP.PPSM.6");
                    RunSetUserFTA(ftaExePath, ".potm", "WPP.POTM.6");
                }
                else
                {
                    // 切换到 PowerPoint
                    DefineProgIdPath("PowerPoint.Show.12", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.8", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.14", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.7", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.20", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.4", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.13", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.15", OFFICE_PPT_EXE);
                    DefineProgIdPath("PowerPoint.Show.21", OFFICE_PPT_EXE);
                    RunSetUserFTA(ftaExePath, ".pptx", "PowerPoint.Show.12");
                    RunSetUserFTA(ftaExePath, ".ppt", "PowerPoint.Show.8");
                    RunSetUserFTA(ftaExePath, ".ppsx", "PowerPoint.Show.14");
                    RunSetUserFTA(ftaExePath, ".pps", "PowerPoint.Show.7");
                    RunSetUserFTA(ftaExePath, ".potx", "PowerPoint.Show.20");
                    RunSetUserFTA(ftaExePath, ".pot", "PowerPoint.Show.4");
                    RunSetUserFTA(ftaExePath, ".pptm", "PowerPoint.Show.13");
                    RunSetUserFTA(ftaExePath, ".ppsm", "PowerPoint.Show.15");
                    RunSetUserFTA(ftaExePath, ".potm", "PowerPoint.Show.21");
                }
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CWS 切换异常: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 执行 SetUserFTA 指令
        /// </summary>
        private static void RunSetUserFTA(string swaPath, string extension, string progId)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = swaPath,
                Arguments = $"{extension} {progId}",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using (Process p = Process.Start(psi))
            {
                p?.WaitForExit();
            }
        }

        /// <summary>
        /// 在注册表中定义 ProgID 的打开路径（解决空壳文件夹问题）
        /// </summary>
        private static void DefineProgIdPath(string progId, string exePath)
        {
            string keyPath = $@"Software\Classes\{progId}\shell\open\command";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                // 写入标准启动指令，包含引号和占位符
                key.SetValue("", $"\"{exePath}\" /n \"%1\"");
            }
        }

        public static bool IsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}