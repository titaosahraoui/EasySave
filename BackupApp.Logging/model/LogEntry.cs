namespace BackupApp.Logging
{
    public class LogEntry
    {
        // Core fields (maintain backward compatibility)
        public DateTime Timestamp { get; set; }
        public string BackupName { get; set; }
        public string SourcePath { get; set; }  // Can be file path or message
        public string DestinationPath { get; set; }
        public long FileSizeBytes { get; set; }
        public long TransferTimeMs { get; set; }
        public bool Success { get; set; }
        public string ActionType { get; set; }

        // New structured fields
        public string Operation { get; set; }      // e.g., "Encryption", "FileCopy"
        public string Status { get; set; }         // e.g., "Started", "Completed", "Failed"
        public string Details { get; set; }        // JSON-formatted additional data
    }
}