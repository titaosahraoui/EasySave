using ReactiveUI;
using BackupApp.Views;
using System;
using System.Reactive;

namespace BackupApp.ViewModels
{
    public class MainWindowViewModel : ReactiveObject
    {
        private ViewModelBase _currentView;
        public ViewModelBase CurrentView
        {
            get => _currentView;
            set => this.RaiseAndSetIfChanged(ref _currentView, value);
        }

        public ReactiveCommand<Unit, Unit> NavigateToDashboardCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToBackupJobsCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToSettingsCommand { get; }
        public ReactiveCommand<Unit, Unit> NavigateToLogsCommand { get; }

        public MainWindowViewModel()
        {
            // Initialize commands
            //NavigateToDashboardCommand = ReactiveCommand.Create(NavigateToDashboard);
            NavigateToBackupJobsCommand = ReactiveCommand.Create(NavigateToBackupJobs);
            //NavigateToSettingsCommand = ReactiveCommand.Create(NavigateToSettings);
            //NavigateToLogsCommand = ReactiveCommand.Create(NavigateToLogs);

            // Set default view
            NavigateToBackupJobs();
        }

        //private void NavigateToDashboard()
        //{
        //    CurrentView = new DashboardViewModel();
        //}

        private void NavigateToBackupJobs()
        {
            CurrentView = new BackupViewModel();
        }

        //private void NavigateToSettings()
        //{
        //    CurrentView = new SettingsViewModel();
        //}

        //private void NavigateToLogs()
        //{
        //    CurrentView = new LogsViewModel();
        //}
    }
}