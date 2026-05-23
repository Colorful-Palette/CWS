using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CWS.Services
{
    public static class UpdateChecker
    {
        public const string CurrentVersion = "0.4.0";

        private const string RepoOwner = "Colorful-Palette";
        private const string RepoName = "CWS";

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        static UpdateChecker()
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CWS-UpdateChecker/1.0");
        }

        public static async Task<(bool hasUpdate, string? latestVersion, string? releaseUrl)> CheckForUpdateAsync()
        {
            try
            {
                string url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                string json = await _httpClient.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(json);

                string? tagName = doc.RootElement.GetProperty("tag_name").GetString();
                string? htmlUrl = doc.RootElement.GetProperty("html_url").GetString();

                if (tagName == null) return (false, null, null);

                string latestVer = tagName.StartsWith("v") ? tagName[1..] : tagName;
                bool isNewer = IsNewerVersion(latestVer, CurrentVersion);

                return (isNewer, latestVer, htmlUrl);
            }
            catch
            {
                return (false, null, null);
            }
        }

        private static bool IsNewerVersion(string latest, string current)
        {
            if (Version.TryParse(latest, out var latestVer) &&
                Version.TryParse(current, out var currentVer))
            {
                return latestVer > currentVer;
            }
            return !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
        }
    }
}
