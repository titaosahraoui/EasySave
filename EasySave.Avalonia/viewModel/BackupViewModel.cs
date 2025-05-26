using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive;
using BackupApp.Models;
using BackupApp.Controllers;

namespace BackupApp.ViewModels
{
    public class BackupViewModel : ReactiveObject
    {
        private readonly BackupController _controller;
        public ObservableCollection<BackupJob> Jobs { get; set; }

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
            Jobs = new ObservableCollection<BackupJob>(_controller.GetAllJobs());

            LoadJobsCommand = ReactiveCommand.Create(LoadJobs);
            AddJobCommand = ReactiveCommand.Create(AddJob);
            RunJobCommand = ReactiveCommand.Create(RunJob);
            DeleteJobCommand = ReactiveCommand.Create(DeleteJob);
        }

        private void LoadJobs()
        {
            Jobs.Clear();
            foreach (var job in _controller.GetAllJobs())
                Jobs.Add(job);
        }

        private void AddJob()
        {
            var job = new BackupJob
            {
                Name = "New Job",
                SourcePath = @"C:\Source",
                TargetPath = @"C:\Backup",
                Type = BackupType.Full
            };

            _controller.AddJob(job);
            Jobs.Add(job);
        }

        private void RunJob()
        {
            if (SelectedJob != null)
                _controller.RunBackup(SelectedJob.Id);
        }

        private void DeleteJob()
        {
            if (SelectedJob != null)
            {
                _controller.DeleteJob(SelectedJob.Id);
                Jobs.Remove(SelectedJob);
            }
        }
    }
}
