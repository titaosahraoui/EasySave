using BackupApp.Data;
using BackupApp.Models;
using BackupApp.Services;

namespace BackupApp.Controllers
{


    public class BackupController
    {
        private readonly BackupRepository _repository;
        private readonly BackupService _backupService;

        public BackupController()
        {
            var monitoredApps = new[] { "Word", "Excel", "notepad" }; // replace with actual process names you want to monitor
            var softwareMonitor = new BusinessSoftwareMonitor(monitoredApps);
            _repository = new BackupRepository();
            _backupService = new BackupService(softwareMonitor);
        }

        public List<BackupJob> GetAllJobs() => _repository.GetAllBackupJobs();

        public void AddJob(BackupJob job) => _repository.AddBackupJob(job);

        public void UpdateJob(BackupJob job) => _repository.UpdateBackupJob(job);

        public void DeleteJob(int id) => _repository.DeleteBackupJob(id);

        //public void RunBackup(int jobId)
        //{
        //    var job = _repository.GetBackupJob(jobId);
        //    if (job != null)
        //    {
        //        _backupService.PerformBackup(job);
        //    }
        //}
    }

}
