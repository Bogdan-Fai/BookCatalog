using System;
using System.IO;
using System.Threading;

namespace BookCatalog
{
    public static class Logger
    {
        private static readonly string LogFilePath = "sync.log";
        private static readonly object LockObject = new object();

        public static void Log(string message)
        {
            LogInternal("INFO", message);
        }

        public static void LogError(string message, Exception? ex = null)
        {
            var errorMessage = message;
            if (ex != null)
            {
                errorMessage += $": {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" (Inner: {ex.InnerException.Message})";
                }
            }
            LogInternal("ERROR", errorMessage);
        }

        public static void LogWarning(string message)
        {
            LogInternal("WARNING", message);
        }

        public static void LogSuccess(string message)
        {
            LogInternal("SUCCESS", message);
        }

        public static void LogOperation(string operation, int recordsProcessed, int successCount, int errorCount)
        {
            var message = $"{operation} completed. Total: {recordsProcessed}, Success: {successCount}, Errors: {errorCount}";
            LogInternal("OPERATION", message);
        }

        private static void LogInternal(string level, string message)
        {
            lock (LockObject)
            {
                try
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var logEntry = $"[{timestamp}] [{level}] {message}\n";
                    
                    Console.WriteLine(logEntry.Trim()); // Также выводим в консоль
                    
                    File.AppendAllText(LogFilePath, logEntry);
                }
                catch (Exception ex)
                {
                    // Если не можем записать в лог, выводим в консоль как последнее средство
                    Console.WriteLine($"[FALLBACK] Failed to write to log: {ex.Message}");
                }
            }
        }

        public static void ClearLog()
        {
            lock (LockObject)
            {
                try
                {
                    if (File.Exists(LogFilePath))
                    {
                        File.Delete(LogFilePath);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to clear log: {ex.Message}");
                }
            }
        }

        public static string GetLogContent()
        {
            lock (LockObject)
            {
                try
                {
                    return File.Exists(LogFilePath) ? File.ReadAllText(LogFilePath) : string.Empty;
                }
                catch (Exception ex)
                {
                    LogError("Failed to read log content", ex);
                    return string.Empty;
                }
            }
        }
    }
}