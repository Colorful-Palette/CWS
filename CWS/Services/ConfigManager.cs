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
                ["BgOpacity"] = Properties.Settings.Default.BgOpacity
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
