using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CWS.Services
{
    public static class Logger
    {
        private static readonly object _lock = new();
        private static readonly string _logDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CWS", "logs");

        public static void Info(string message) => WriteLog("INFO", message);
        public static void Error(string message) => WriteLog("ERROR", message);

        private static void WriteLog(string level, string message)
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    Directory.CreateDirectory(_logDirectory);

                string logFile = Path.Combine(_logDirectory, $"cws-{DateTime.Now:yyyy-MM-dd}.log");
                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";

                lock (_lock)
                {
                    File.AppendAllText(logFile, line + Environment.NewLine);
                }

                CleanOldLogs();
            }
            catch { }
        }

        private static void CleanOldLogs()
        {
            try
            {
                DateTime cutoff = DateTime.Now.AddDays(-7);
                foreach (string file in Directory.GetFiles(_logDirectory, "cws-*.log"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch { }
        }

        public static string ExportLogs()
        {
            var sb = new StringBuilder();
            if (Directory.Exists(_logDirectory))
            {
                foreach (string file in Directory.GetFiles(_logDirectory, "*.log").OrderByDescending(f => f))
                {
                    sb.AppendLine($"=== {Path.GetFileName(file)} ===");
                    sb.AppendLine(File.ReadAllText(file));
                    sb.AppendLine();
                }
            }
            if (sb.Length == 0)
                sb.AppendLine("No logs available.");
            return sb.ToString();
        }
    }
}
