using System;
using System.IO;
using System.Text;

namespace FortyOne.AudioSwitcher.Helpers
{
    /// <summary>
    /// Lightweight file logger under %LocalAppData%\AudioSwitcher\logs.
    /// </summary>
    public static class AppLog
    {
        private static readonly object Mutex = new object();
        private static string _logDir;

        public static void Info(string message)
        {
            Write("INFO", message, null);
        }

        public static void Warn(string message)
        {
            Write("WARN", message, null);
        }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", message, ex);
        }

        private static void Write(string level, string message, Exception ex)
        {
            try
            {
                EnsureDir();
                var line = new StringBuilder();
                line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                line.Append(" [").Append(level).Append("] ");
                line.Append(message);
                if (ex != null)
                {
                    line.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                    if (ex.StackTrace != null)
                        line.AppendLine().Append(ex.StackTrace);
                }
                line.AppendLine();

                var path = Path.Combine(_logDir, "app-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
                lock (Mutex)
                {
                    File.AppendAllText(path, line.ToString());
                }
            }
            catch
            {
                // Logging must never crash the app
            }
        }

        private static void EnsureDir()
        {
            if (_logDir != null)
                return;

            _logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AudioSwitcher",
                "logs");

            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
        }
    }
}
