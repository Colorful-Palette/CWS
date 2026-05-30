using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CWS.Services
{
    public static class UpdateChecker
    {
        public static readonly string CurrentVersion = ResolveCurrentVersion();

        private const string RepoOwner = "Colorful-Palette";
        private const string RepoName = "CWS";
        private const string ReleasesPageUrl = "https://github.com/Colorful-Palette/CWS/releases";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CWS-UpdateChecker/1.0");
        }

        private static string ResolveCurrentVersion()
        {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var normalized = info.Split('+').FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(normalized)) return normalized!;
            }

            var fileVer = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
            if (!string.IsNullOrWhiteSpace(fileVer)) return fileVer!;

            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }

        public static async Task<(bool hasUpdate, string? latestVersion, string? releaseUrl)> CheckForUpdateAsync()
        {
            try
            {
                string latestApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                string json = await _httpClient.GetStringAsync(latestApiUrl);
                using JsonDocument doc = JsonDocument.Parse(json);

                string? tagName = doc.RootElement.GetProperty("tag_name").GetString();
                string? htmlUrl = doc.RootElement.GetProperty("html_url").GetString();

                if (tagName == null) return (false, null, null);

                string latestVer = tagName.StartsWith("v") ? tagName[1..] : tagName;
                bool isNewer = IsNewerVersion(latestVer, CurrentVersion);

                return (isNewer, latestVer, string.IsNullOrWhiteSpace(htmlUrl) ? ReleasesPageUrl : htmlUrl);
            }
            catch
            {
                return (false, null, null);
            }
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            var latestParts = ExtractNumericParts(latest);
            var currentParts = ExtractNumericParts(current);

            int maxLen = Math.Max(latestParts.Length, currentParts.Length);
            for (int i = 0; i < maxLen; i++)
            {
                long l = i < latestParts.Length ? latestParts[i] : 0;
                long c = i < currentParts.Length ? currentParts[i] : 0;
                if (l > c) return true;
                if (l < c) return false;
            }

            return false;
        }

        private static long[] ExtractNumericParts(string versionText)
        {
            return Regex.Matches(versionText ?? string.Empty, @"\d+")
                .Select(m => long.TryParse(m.Value, out var n) ? n : 0)
                .ToArray();
        }
    }
}
