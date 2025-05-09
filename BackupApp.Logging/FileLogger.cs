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
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonEntry = JsonSerializer.Serialize(entry, options);

                File.AppendAllText(
                    GetDailyLogPath(),
                    jsonEntry + Environment.NewLine);
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
    }
}