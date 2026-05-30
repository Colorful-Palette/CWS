using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CWS.Services
{
    public static class ConfigManager
    {
        public static void ExportConfig(string filePath)
        {
            var config = new Dictionary<string, object>
            {
                ["IsAutoStart"] = Properties.Settings.Default.IsAutoStart,
                ["BackgroundImagePath"] = Properties.Settings.Default.BackgroundImagePath,
                ["StartAsFloating"] = Properties.Settings.Default.StartAsFloating,
                ["PPTServiceExePath"] = Properties.Settings.Default.PPTServiceExePath,
                ["BgOpacity"] = Properties.Settings.Default.BgOpacity,
                ["AssocWordTarget"] = Properties.Settings.Default.AssocWordTarget,
                ["AssocExcelTarget"] = Properties.Settings.Default.AssocExcelTarget,
                ["AssocPdfTarget"] = Properties.Settings.Default.AssocPdfTarget,
                ["AssocWordSuffixes"] = Properties.Settings.Default.AssocWordSuffixes,
                ["AssocExcelSuffixes"] = Properties.Settings.Default.AssocExcelSuffixes,
                ["AssocPdfSuffixes"] = Properties.Settings.Default.AssocPdfSuffixes,
                ["LastInvokeUrl"] = Properties.Settings.Default.LastInvokeUrl,
                ["ThemePreset"] = Properties.Settings.Default.ThemePreset
            };
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        public static bool ImportConfig(string filePath)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (config == null) return false;

                if (config.TryGetValue("IsAutoStart", out var isAuto))
                    Properties.Settings.Default.IsAutoStart = isAuto.GetBoolean();
                if (config.TryGetValue("BackgroundImagePath", out var bgPath))
                    Properties.Settings.Default.BackgroundImagePath = bgPath.GetString() ?? "";
                if (config.TryGetValue("StartAsFloating", out var startFloat))
                    Properties.Settings.Default.StartAsFloating = startFloat.GetBoolean();
                if (config.TryGetValue("PPTServiceExePath", out var pptPath))
                    Properties.Settings.Default.PPTServiceExePath = pptPath.GetString() ?? "";
                if (config.TryGetValue("BgOpacity", out var opacity))
                    Properties.Settings.Default.BgOpacity = opacity.GetDouble();

                if (config.TryGetValue("AssocWordTarget", out var wordTarget) && wordTarget.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.AssocWordTarget = wordTarget.GetString() ?? "Office";
                if (config.TryGetValue("AssocExcelTarget", out var excelTarget) && excelTarget.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.AssocExcelTarget = excelTarget.GetString() ?? "Office";
                if (config.TryGetValue("AssocPdfTarget", out var pdfTarget) && pdfTarget.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.AssocPdfTarget = pdfTarget.GetString() ?? "Edge";
                if (config.TryGetValue("AssocWordSuffixes", out var wordSuffixes) && wordSuffixes.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.AssocWordSuffixes = wordSuffixes.GetString() ?? ".doc;.docx;.docm;.dot;.dotx;.dotm";
                if (config.TryGetValue("AssocExcelSuffixes", out var excelSuffixes) && excelSuffixes.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.AssocExcelSuffixes = excelSuffixes.GetString() ?? ".xls;.xlsx;.xlsm;.xlt;.xltx;.xltm";
                if (config.TryGetValue("AssocPdfSuffixes", out var pdfSuffixes) && pdfSuffixes.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.AssocPdfSuffixes = pdfSuffixes.GetString() ?? ".pdf";
                if (config.TryGetValue("LastInvokeUrl", out var lastUrl) && lastUrl.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.LastInvokeUrl = lastUrl.GetString() ?? "";
                if (config.TryGetValue("ThemePreset", out var themePreset) && themePreset.ValueKind == JsonValueKind.String)
                    Properties.Settings.Default.ThemePreset = themePreset.GetString() ?? "MonetWaterLilies";

                Properties.Settings.Default.Save();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
