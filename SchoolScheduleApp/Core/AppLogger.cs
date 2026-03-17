using System;
using System.IO;

namespace SchoolScheduleApp.Core
{
    public static class AppLogger
    {
        private static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        private static readonly object SyncRoot = new();

        public static void LogInfo(string message)
        {
            Write("INFO", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex == null ? message : $"{message}\n{ex}";
            Write("ERROR", fullMessage);
        }

        private static void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level}: {message}";
            try
            {
                lock (SyncRoot)
                {
                    File.AppendAllLines(LogPath, new[] { line });
                }
            }
            catch
            {
                // Logging should never crash the app.
            }
        }
    }
}
