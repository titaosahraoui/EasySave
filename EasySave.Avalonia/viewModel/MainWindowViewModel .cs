using BackupApp.Models;
using BackupApp.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;

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

    public MainWindowViewModel()
    {
        // Initialize the Backup ViewModel
        BackupVM = new BackupViewModel();

        // Initialize commands
        RunAllJobsCommand = new AsyncRelayCommand(RunAllJobs);
        PauseAllJobsCommand = new RelayCommand(PauseAllJobs);
        ResumeAllJobsCommand = new RelayCommand(ResumeAllJobs);
        StopAllJobsCommand = new RelayCommand(StopAllJobs);
        NavigateToBackupJobsCommand = new RelayCommand(NavigateToBackupJobs);

        // Set default view
        NavigateToBackupJobs();
    }

    private async Task RunAllJobs()
    {
        if (CurrentView is BackupViewModel backupVM)
        {
            // Select all jobs and run them
            backupVM.SelectedJobs = new List<BackupJob>(backupVM.Jobs);
            await backupVM.RunSelectedJob();
        }
    }

    private void PauseAllJobs()
    {
        if (CurrentView is BackupViewModel backupVM)
        {
            foreach (var job in backupVM.Jobs)
            {
                backupVM._backupService.PauseBackup(job.Id);
            }
        }
    }

    private void ResumeAllJobs()
    {
        if (CurrentView is BackupViewModel backupVM)
        {
            foreach (var job in backupVM.Jobs)
            {
                backupVM._backupService.ResumeBackup(job.Id);
            }
        }
    }

    private void StopAllJobs()
    {
        if (CurrentView is BackupViewModel backupVM)
        {
            foreach (var job in backupVM.Jobs)
            {
                backupVM._backupService.StopBackup(job.Id);
            }
        }
    }

    private void NavigateToBackupJobs()
    {
        CurrentView = BackupVM;
    }
}