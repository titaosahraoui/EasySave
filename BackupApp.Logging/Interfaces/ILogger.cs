using System;
using System.Collections.Generic;

namespace BackupApp.Logging
{
    public interface ILogger
    {
        void LogFileTransfer(string backupName, string sourcePath, string destPath,
                           long fileSize, long transferTimeMs, bool success);
        void LogDirectoryCreation(string backupName, string path);
        void LogError(string backupName, string errorMessage);
        List<LogEntry> GetDailyLogs();
    }
}