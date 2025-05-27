namespace BackupApp.Logging
{
    public enum LogFormat
    {
        Json,
        Xml
    }

    public interface ILogger
    {
        LogFormat CurrentFormat { get; set; }
        void LogFileTransfer(string backupName, string sourcePath, string destPath,
                           long fileSize, long transferTimeMs, bool success);
        void LogDirectoryCreation(string backupName, string path);
        void LogError(string backupName, string errorMessage);
        List<LogEntry> GetDailyLogs();
    }
}