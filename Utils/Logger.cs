using System;
using System.IO;

namespace SteamGuard
{
    /// <summary>
    /// Простой логгер в файл
    /// </summary>
    public static class AppLogger
    {
        private static readonly string _logDirectory;
        private static readonly string _logFile;
        private static readonly object _lock = new object();

        static AppLogger()
        {
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(_logDirectory);
            _logFile = Path.Combine(_logDirectory, $"app_{DateTime.Now:yyyyMMdd}.log");
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message, Exception? ex = null)
        {
            Write("ERROR", ex != null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message);
        }

        public static void Debug(string message)
        {
            Write("DEBUG", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var line = $"[{timestamp}] [{level}] {message}";

                lock (_lock)
                {
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                }
            }
            catch
            {
                // Не блокируем приложение если логирование не работает
            }
        }

        public static string LogFilePath => _logFile;
    }
}
