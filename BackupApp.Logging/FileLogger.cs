using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BackupApp.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();
        public LogFormat CurrentFormat { get; set; } = LogFormat.Json;

        public FileLogger()
        {
            // Use AppData/Roaming for proper file locations
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EasySave",
                "Logs");

            Directory.CreateDirectory(_logDirectory);
        }

        private string GetDailyLogPath()
        {
            return Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.json");
        }

        public void LogFileTransfer(string backupName, string sourcePath, string destPath,
                                  long fileSize, long transferTimeMs, bool success)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = backupName,
                SourcePath = sourcePath,
                DestinationPath = destPath,
                FileSizeBytes = fileSize,
                TransferTimeMs = success ? transferTimeMs : -1,
                Success = success,
                ActionType = "FileTransfer"
            };

            AppendLog(entry);
        }

        public void LogDirectoryCreation(string backupName, string path)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = backupName,
                SourcePath = path,
                ActionType = "DirCreate",
                Success = true
            };

            AppendLog(entry);
        }

        public void LogError(string backupName, string errorMessage)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = backupName,
                SourcePath = errorMessage,
                ActionType = "Error",
                Success = false
            };

            AppendLog(entry);
        }

        private void AppendLog(LogEntry entry)
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonEntry = JsonSerializer.Serialize(entry, options);
                    File.AppendAllText(
                        GetDailyLogPath(),
                        jsonEntry + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // Fallback logging to console if file logging fails
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to write log: {ex.Message}");
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{entry.ActionType}] {entry.BackupName}: {entry.SourcePath}");
                }
            }
        }

        public List<LogEntry> GetDailyLogs()
        {
            var logs = new List<LogEntry>();
            string path = GetDailyLogPath();

            if (File.Exists(path))
            {
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        logs.Add(JsonSerializer.Deserialize<LogEntry>(line));
                    }
                }
            }

            return logs;
        }

        public void LogInfo(string category, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = category,
                SourcePath = message,
                ActionType = "Info",
                Success = true
            };
            AppendLog(entry);
        }

        public void LogWarning(string category, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = category,
                SourcePath = message,
                ActionType = "Warning",
                Success = true
            };
            AppendLog(entry);
        }
    }
}