using System;
using System.Collections.Generic;

namespace BackupApp.Logging
{
    public interface IStateManager
    {
        void UpdateState(string backupName, BackupState state);
        BackupState GetCurrentState(string backupName);
        List<BackupState> GetAllStates();
    }

    public class BackupState
    {
        public string? BackupName { get; set; }
        public DateTime LastActionTimestamp { get; set; }
        public string? Status { get; set; } // "Active", "Inactive", "Completed", "Error"
        public int TotalFiles { get; set; }
        public long TotalSizeBytes { get; set; }
        public int FilesProcessed { get; set; }
        public int FilesRemaining { get; set; }
        public long SizeRemainingBytes { get; set; }
        public string? CurrentSourceFile { get; set; }
        public string? CurrentDestFile { get; set; }
        public double ProgressPercentage =>
            TotalFiles > 0 ? (FilesProcessed * 100.0) / TotalFiles : 0;
    }
}