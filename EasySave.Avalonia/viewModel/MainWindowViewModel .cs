using BackupApp.Models;
using BackupApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;


namespace BackupApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public BackupViewModel BackupVM { get; }

        // Commands
        public ICommand RunAllJobsCommand { get; }
        public ICommand PauseAllJobsCommand { get; }
        public ICommand ResumeAllJobsCommand { get; }
        public ICommand StopAllJobsCommand { get; }
        public ICommand NavigateToBackupJobsCommand { get; }
        public ICommand NavigateToSettingsCommand { get; }

        public MainWindowViewModel()
        {
            // Initialize the Backup ViewModel
            BackupVM = new BackupViewModel();
            CurrentView = BackupVM;
            // Initialize commands
            RunAllJobsCommand = new AsyncRelayCommand(RunAllJobs);
            PauseAllJobsCommand = new RelayCommand(PauseAllJobs);
            ResumeAllJobsCommand = new RelayCommand(ResumeAllJobs);
            StopAllJobsCommand = new RelayCommand(StopAllJobs);
            NavigateToBackupJobsCommand = new RelayCommand(NavigateToBackupJobs);
            NavigateToSettingsCommand = new RelayCommand(NavigateToSettings);
            // Set default view
            NavigateToBackupJobs();
        }

        private async Task RunAllJobs()
        {
            // Select all jobs and run them
            BackupVM.SelectedJobs = new List<BackupJob>(BackupVM.Jobs);
            await BackupVM.RunSelectedJob();
        }

        private void PauseAllJobs()
        {
            foreach (var job in BackupVM.Jobs)
            {
                BackupVM._backupService.PauseBackup(job.Id);
            }
        }

        private void ResumeAllJobs()
        {
            foreach (var job in BackupVM.Jobs)
            {
                BackupVM._backupService.ResumeBackup(job.Id);
            }
        }

        private void StopAllJobs()
        {
            foreach (var job in BackupVM.Jobs)
            {
                BackupVM._backupService.StopBackup(job.Id);
            }
        }

        private void NavigateToBackupJobs()
        {
            CurrentView = BackupVM;
        }
        private void NavigateToSettings()
        {
            CurrentView = new SettingsViewModel(); // You'll need to create this

        }
    }
}