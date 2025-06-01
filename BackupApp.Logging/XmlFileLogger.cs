using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace BackupApp.Logging
{
    public class XmlFileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly object _lock = new object();
        public LogFormat CurrentFormat { get; set; } = LogFormat.Xml;

        public XmlFileLogger()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EasySave",
                "Logs");

            Directory.CreateDirectory(_logDirectory);
        }

        private string GetDailyLogPath()
        {
            return Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.xml");
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

        public void LogWarning(string backupName, string warningMessage)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = backupName,
                SourcePath = warningMessage,
                ActionType = "Warning",
                Success = true
            };

            AppendLog(entry);
        }

        public void LogInfo(string backupName, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                BackupName = backupName,
                SourcePath = message,
                ActionType = "Info",
                Success = true
            };

            AppendLog(entry);
        }

        private void AppendLog(LogEntry entry)
        {
            lock (_lock)
            {
                try
                {
                    var serializer = new XmlSerializer(typeof(LogEntry));
                    using (var writer = new StreamWriter(GetDailyLogPath(), true))
                    {
                        serializer.Serialize(writer, entry);
                        writer.WriteLine(); // Add newline between entries
                    }
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
                try
                {
                    var serializer = new XmlSerializer(typeof(LogEntry));
                    using (var reader = new StreamReader(path))
                    {
                        while (reader.Peek() > 0)
                        {
                            logs.Add((LogEntry)serializer.Deserialize(reader));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Failed to read logs: {ex.Message}");
                }
            }

            return logs;
        }
    }
}