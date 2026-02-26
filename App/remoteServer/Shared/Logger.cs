using System;
using System.IO;

namespace Shared.Utils
{
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly object _lock = new object();

        static Logger()
        {
            try
            {
                string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDir);
                // Rolling/append đơn giản vào server.log
                LogFilePath = Path.Combine(logDir, "server.log");
            }
            catch
            {
                LogFilePath = "server.log"; // Fallback
            }
        }

        public static void Log(string message)
        {
            lock (_lock)
            {
                try
                {
                    string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                    File.AppendAllText(LogFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // Tạm thời ẩn exception nếu không ghi được log
                }
            }
        }
    }
}
