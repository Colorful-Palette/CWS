using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CWS
{
    public static class FileAssociationScanner
    {
        public enum AssociationCategory
        {
            Word,
            Excel,
            Pdf
        }

        public enum AssociationTarget
        {
            Office,
            Wps,
            Edge
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

        private const string OFFICE_PPT_EXE = @"C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE";
        private const string FTA_EXE_NAME = "SetUserFTA.exe";

        public static bool AutoFixAssociation(bool toWps)
        {
            if (!IsAdmin()) return false;

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

        public static (int success, int failed) ApplyAssociations(
            AssociationCategory category,
            AssociationTarget target,
            IEnumerable<string> extensions)
        {
            var normalized = extensions
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count == 0) return (0, 0);
            if (!IsAdmin()) return (0, normalized.Count);

            string ftaExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FTA_EXE_NAME);
            if (!File.Exists(ftaExePath)) return (0, normalized.Count);

            int success = 0;
            int failed = 0;

            foreach (var ext in normalized)
            {
                var candidates = GetProgIdCandidates(category, target, ext);
                if (candidates == null || candidates.Length == 0)
                {
                    failed++;
                    continue;
                }

                var progId = PickAvailableProgId(candidates);
                if (string.IsNullOrWhiteSpace(progId))
                {
                    failed++;
                    continue;
                }

                try
                {
                    RunSetUserFTA(ftaExePath, ext, progId);
                    success++;
                }
                catch
                {
                    failed++;
                }
            }

            if (success > 0)
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

            return (success, failed);
        }

        private static string NormalizeExtension(string ext)
        {
            string trimmed = ext.Trim().ToLowerInvariant();
            return trimmed.StartsWith(".") ? trimmed : $".{trimmed}";
        }

        private static string[]? GetProgIdCandidates(AssociationCategory category, AssociationTarget target, string extension)
        {
            return category switch
            {
                AssociationCategory.Word => GetWordProgIds(target, extension),
                AssociationCategory.Excel => GetExcelProgIds(target, extension),
                AssociationCategory.Pdf => GetPdfProgIds(target, extension),
                _ => null
            };
        }

        private static string[]? GetWordProgIds(AssociationTarget target, string ext)
        {
            if (target == AssociationTarget.Office)
            {
                return ext switch
                {
                    ".doc" => new[] { "Word.Document.8" },
                    ".docx" => new[] { "Word.Document.12" },
                    ".docm" => new[] { "Word.DocumentMacroEnabled.12" },
                    ".dot" => new[] { "Word.Template.8" },
                    ".dotx" => new[] { "Word.Template.12" },
                    ".dotm" => new[] { "Word.TemplateMacroEnabled.12" },
                    _ => null
                };
            }

            if (target == AssociationTarget.Wps)
            {
                return ext switch
                {
                    ".doc" => new[] { "KWPS.Document.8", "KWPS.Document.6", "WPS.Document.8" },
                    ".docx" => new[] { "KWPS.Document.12", "KWPS.Document.6", "WPS.Document.12" },
                    ".docm" => new[] { "KWPS.DocumentMacroEnabled.12", "KWPS.Document.6" },
                    ".dot" => new[] { "KWPS.Template.8", "KWPS.Template.6" },
                    ".dotx" => new[] { "KWPS.Template.12", "KWPS.Template.6" },
                    ".dotm" => new[] { "KWPS.TemplateMacroEnabled.12", "KWPS.Template.6" },
                    _ => null
                };
            }

            return null;
        }

        private static string[]? GetExcelProgIds(AssociationTarget target, string ext)
        {
            if (target == AssociationTarget.Office)
            {
                return ext switch
                {
                    ".xls" => new[] { "Excel.Sheet.8" },
                    ".xlsx" => new[] { "Excel.Sheet.12" },
                    ".xlsm" => new[] { "Excel.SheetMacroEnabled.12" },
                    ".xlt" => new[] { "Excel.Template.8" },
                    ".xltx" => new[] { "Excel.Template.12" },
                    ".xltm" => new[] { "Excel.TemplateMacroEnabled.12" },
                    _ => null
                };
            }

            if (target == AssociationTarget.Wps)
            {
                return ext switch
                {
                    ".xls" => new[] { "KET.Sheet.8", "KET.Sheet.6" },
                    ".xlsx" => new[] { "KET.Sheet.12", "KET.Sheet.6" },
                    ".xlsm" => new[] { "KET.SheetMacroEnabled.12", "KET.Sheet.6" },
                    ".xlt" => new[] { "KET.Template.8", "KET.Template.6" },
                    ".xltx" => new[] { "KET.Template.12", "KET.Template.6" },
                    ".xltm" => new[] { "KET.TemplateMacroEnabled.12", "KET.Template.6" },
                    _ => null
                };
            }

            return null;
        }

        private static string[]? GetPdfProgIds(AssociationTarget target, string ext)
        {
            if (ext != ".pdf") return null;

            return target switch
            {
                AssociationTarget.Edge => new[] { "MSEdgePDF", "AppXd4nrz8ff68srnhf9t5a8sbjyar1cr723" },
                AssociationTarget.Wps => new[] { "WPS.PDF.6", "KWPS.PDF.6", "WPP.PDF.6" },
                _ => null
            };
        }

        private static string PickAvailableProgId(IEnumerable<string> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (ProgIdExists(candidate)) return candidate;
            }

            return candidates.First();
        }

        private static bool ProgIdExists(string progId)
        {
            using var hkcr = Registry.ClassesRoot.OpenSubKey(progId);
            if (hkcr != null) return true;

            using var hkcu = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}");
            return hkcu != null;
        }

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

        private static void DefineProgIdPath(string progId, string exePath)
        {
            string keyPath = $@"Software\Classes\{progId}\shell\open\command";
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
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
