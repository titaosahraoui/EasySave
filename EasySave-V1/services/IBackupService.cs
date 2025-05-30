using BackupApp.Models;
using BackupApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupApp.services
{
    public interface IBackupService
    {
        Task PerformBackupAsync(BackupJob job, IProgress<BackupProgressReport> progress, CancellationToken cancellationToken);
        void PauseBackup(int jobId);
        void ResumeBackup(int jobId);
        void StopBackup(int jobId);
    }
}
