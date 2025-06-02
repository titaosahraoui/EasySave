using BackupApp.Models;
using BackupApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using BackupApp.Avalonia;


namespace BackupApp.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {

        public string ApplicationTitle => Language.GetString("ApplicationTitle");
        public string NavigationText => Language.GetString("Navigation");
        public string BackupJobsText => Language.GetString("BackupJobs");
        public string SettingsText => Language.GetString("Settings");
        public string LogsText => Language.GetString("Logs");
        public string QuickActionsText => Language.GetString("QuickActions");
        public string RunAllJobsText => Language.GetString("RunAllJobs");
        public string PauseAllText => Language.GetString("PauseAll");
        public string ResumeAllText => Language.GetString("ResumeAll");
        public string StopAllText => Language.GetString("StopAll");
        private ViewModelBase _currentView;

        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }
        private readonly LanguageBinding _languageBinding;

        public LanguageBinding Language => _languageBinding;

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

            _languageBinding = new LanguageBinding();
            var settingsVM = new SettingsViewModel();
            settingsVM.OnLanguageChanged += OnLanguageChanged;
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

        private void OnLanguageChanged(object sender, string newLanguage)
        {
            // Update the language in LanguageBinding
            Language.GetString(newLanguage);

            // Notify all properties that they need to update
            OnPropertyChanged(nameof(ApplicationTitle));
            OnPropertyChanged(nameof(NavigationText));
            OnPropertyChanged(nameof(BackupJobsText));
            OnPropertyChanged(nameof(SettingsText));
            OnPropertyChanged(nameof(LogsText));
            OnPropertyChanged(nameof(QuickActionsText));
            OnPropertyChanged(nameof(RunAllJobsText));
            OnPropertyChanged(nameof(PauseAllText));
            OnPropertyChanged(nameof(ResumeAllText));
            OnPropertyChanged(nameof(StopAllText));
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