using BackupApp.Models;
using BackupApp.Data;
using BackupApp.Services;
using BackupApp.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BackupApp.ViewModels
{
    public class BackupViewModel : ViewModelBase
    {
        private readonly BackupRepository _repository;
        private readonly BackupService _backupService;
        private readonly LanguageService _languageService;

        private BackupJob _selectedJob;
        public ObservableCollection<BackupJob> Jobs { get; } = new();

        public BackupJob SelectedJob
        {
            get => _selectedJob;
            set => SetProperty(ref _selectedJob, value);
        }

        public ICommand LoadJobsCommand { get; }
        public ICommand RunJobCommand { get; }
        public ICommand DeleteJobCommand { get; }

        public BackupViewModel()
        {
            _repository = new BackupRepository();
            _backupService = new BackupService();
            _languageService = new LanguageService();

            LoadJobsCommand = new RelayCommand(LoadJobs);
            RunJobCommand = new AsyncRelayCommand(RunSelectedJob);
            DeleteJobCommand = new RelayCommand(DeleteSelectedJob);

            LoadJobs();
        }

        private void LoadJobs()
        {
            var jobs = _repository.GetAllBackupJobs();
            Jobs.Clear();
            foreach (var job in jobs)
            {
                Jobs.Add(job);
            }
        }

        public void RefreshBackupJobs()
        {
            var jobs = _repository.GetAllBackupJobs();
            Jobs.Clear();
            foreach (var job in jobs)
            {
                Jobs.Add(job);
            }
        }

        private async Task RunSelectedJob()
        {
            if (SelectedJob == null) return;

            SelectedJob.Status = "Active";
            OnPropertyChanged(nameof(SelectedJob));

            try
            {
                await Task.Run(() =>
                {
                    _backupService.PerformBackup(SelectedJob);
                    SelectedJob.Status = "Completed";
                    SelectedJob.LastRun = DateTime.Now;
                });
            }
            catch (Exception ex)
            {
                SelectedJob.Status = "Error";
                ShowError("Backup failed.", ex);
            }

            ExecuteOnUI(() =>
            {
                LoadJobs();
                OnPropertyChanged(nameof(SelectedJob));
            });
        }

        private void DeleteSelectedJob()
        {
            if (SelectedJob == null) return;

            _repository.DeleteBackupJob(SelectedJob.Id);
            LoadJobs();
        }
    }
}
