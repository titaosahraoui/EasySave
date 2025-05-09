using System;

namespace BackupApp.Logging
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string BackupName { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public long FileSizeBytes { get; set; }
        public long TransferTimeMs { get; set; }
        public bool Success { get; set; }
        public string ActionType { get; set; } // "FileTransfer", "DirCreate", "Error"
    }
}