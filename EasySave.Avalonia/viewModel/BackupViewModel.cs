using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using BackupApp.Models;
using BackupApp.Controllers;
using System.Threading.Tasks;
using BackupApp.Avalonia.Views;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using BackupApp.Data;
using System;
using System.Diagnostics;
using System.Windows.Input;

namespace BackupApp.ViewModels
{
    public class BackupViewModel : ReactiveObject
    {
        private readonly BackupController _controller;
        private readonly BackupRepository _repository;
        private readonly Dispatcher _dispatcher = Dispatcher.UIThread;
        public ObservableCollection<BackupJob> Jobs { get; }

        private BackupJob _selectedJob;
        public BackupJob SelectedJob
        {
            get => _selectedJob;
            set => this.RaiseAndSetIfChanged(ref _selectedJob, value);
        }

        public ReactiveCommand<Unit, Unit> LoadJobsCommand { get; }
        public ReactiveCommand<Unit, Unit> AddJobCommand { get; }
        public ReactiveCommand<Unit, Unit> RunJobCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteJobCommand { get; }

        public BackupViewModel()
        {
            _controller = new BackupController();
            _repository = new BackupRepository();
            Jobs = new ObservableCollection<BackupJob>();


            LoadJobsCommand = ReactiveCommand.Create(LoadJobs);
            AddJobCommand = ReactiveCommand.CreateFromTask(AddJobAsync);
            RunJobCommand = ReactiveCommand.CreateFromTask(RunJobAsync);
            DeleteJobCommand = ReactiveCommand.Create(DeleteJob);

            LoadJobs();
        }

        private void LoadJobs()
        {
            Jobs.Clear();
            var allJobs = _repository.GetAllBackupJobs();
            Debug.WriteLine($"Loaded {allJobs.Count} jobs from repository");

            foreach (var job in allJobs)
                Jobs.Add(job);
        }

        private async Task AddJobAsync()
        {
            var window = GetMainWindow();
            if (window == null) return;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var result = await AddEditBackupJobWindow.Show(window);
                if (result != null)
                {
                    _controller.AddJob(result);
                    Jobs.Add(result);
                }
            });
        }

        private async Task RunJobAsync()
        {
            if (SelectedJob != null)
            {
                await Task.Run(() => _controller.RunBackup(SelectedJob.Id));
                LoadJobs(); // Refresh the list
            }
        }

        private void DeleteJob()
        {
            if (SelectedJob != null)
            {
                _controller.DeleteJob(SelectedJob.Id);
                Jobs.Remove(SelectedJob);
            }
        }

        private Window GetMainWindow()
        {
            return (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        }
    }
}