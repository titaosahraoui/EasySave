using BackupApp.Models;
using BackupApp.Data;
using BackupApp.Services;
using BackupApp.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace BackupApp.ViewModels
{
    public class BackupViewModel : ViewModelBase
    {
        private readonly BackupRepository _repository;
        internal readonly BackupService _backupService;
        private readonly LanguageService _languageService;
        private static AppConfig _config;

        private BackupJob _selectedJob;
        public ObservableCollection<BackupJob> Jobs { get; } = new();

        private IList<BackupJob> _selectedJobs = new List<BackupJob>();
        public IList<BackupJob> SelectedJobs
        {
            get => _selectedJobs;
            set => SetProperty(ref _selectedJobs, value);
        }

        public BackupJob SelectedJob
        {
            get => _selectedJob;
            set => SetProperty(ref _selectedJob, value);
        }
        private double _overallProgress;
        public double OverallProgress
        {
            get => _overallProgress;
            set => SetProperty(ref _overallProgress, value);
        }

        private string _overallStatus;
        public string OverallStatus
        {
            get => _overallStatus;
            set => SetProperty(ref _overallStatus, value);
        }

        public ICommand LoadJobsCommand { get; }
        public ICommand RunJobCommand { get; }
        public ICommand DeleteJobCommand { get; }

        public BackupViewModel()
        {

            var monitoredApps = new[] { "Word", "notepad" }; // replace with actual process names you want to monitor
            var softwareMonitor = new BusinessSoftwareMonitor(monitoredApps);

            _repository = new BackupRepository();
            _backupService = new BackupService(softwareMonitor);
            _languageService = new LanguageService();

            LoadJobsCommand = new RelayCommand(LoadJobs);
            RunJobCommand = new AsyncRelayCommand(RunSelectedJob);
            DeleteJobCommand = new RelayCommand(DeleteSelectedJob);

            LoadJobs();
        }

        internal void LoadJobs()
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

        internal async Task RunSelectedJob()
        {
            // Determine which jobs to run
            var jobsToRun = SelectedJobs?.Count > 0 ? SelectedJobs :
                            SelectedJob != null ? new List<BackupJob> { SelectedJob } :
                            null;

            if (jobsToRun == null || jobsToRun.Count == 0)
            {
                ShowAlert("No jobs selected");
                return;
            }

            OverallStatus = $"Running {jobsToRun.Count} job(s)";
            OverallProgress = 0;

            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource())
                {
                    var tasks = new List<Task>();
                    int completedJobs = 0;

                    foreach (var job in jobsToRun)
                    {
                        var currentJob = job; // Capture for closure
                        currentJob.Status = "Active";
                        currentJob.Progress = 0;
                        currentJob.CurrentFile = string.Empty;
                        OnPropertyChanged(nameof(SelectedJob));

                        var progress = new Progress<BackupProgressReport>(report =>
                        {
 
                            currentJob.Progress = Math.Round(report.ProgressPercentage, 2);
                            currentJob.CurrentFile = report.CurrentFile;
                            OnPropertyChanged(nameof(SelectedJob));

                            double totalProgress = Math.Round(jobsToRun.Sum(j => j.Progress) / jobsToRun.Count, 2);
                            OverallProgress = totalProgress;
                            OverallStatus = $"{completedJobs + 1}/{jobsToRun.Count} jobs - {currentJob.Name}";
                        });

                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await _backupService.PerformBackupAsync(currentJob, progress, cancellationTokenSource.Token);
                                currentJob.Status = "Completed";
                                currentJob.LastRun = DateTime.Now;
                            }
                            catch (OperationCanceledException)
                            {
                                currentJob.Status = "Cancelled";
                            }
                            catch (Exception)
                            {
                                currentJob.Status = "Error";
                            }
                            finally
                            {
                                Interlocked.Increment(ref completedJobs);
                                currentJob.Progress = 0;
                                currentJob.CurrentFile = string.Empty;
                            }
                        }));
                    }

                    await Task.WhenAll(tasks);
                    OverallStatus = "All jobs completed";
                }
            }
            catch (Exception ex)
            {
                OverallStatus = "Error occurred during backup";
                ShowError("Backup failed", ex);
            }
            finally
            {
                ExecuteOnUI(() =>
                {
                    LoadJobs();
                    OnPropertyChanged(nameof(SelectedJob));
                });
            }
        }

        private void DeleteSelectedJob()
        {
            if (SelectedJob == null) return;

            _repository.DeleteBackupJob(SelectedJob.Id);
            LoadJobs();
        }
    }
}
