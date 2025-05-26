using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using BackupApp.Models;
using Avalonia.Controls.ApplicationLifetimes;

namespace BackupApp.ViewModels
{
    public class AddEditBackupJobViewModel : ReactiveObject
    {
        private readonly BackupJob _originalJob;

        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string TargetPath { get; set; }
        public int SelectedBackupTypeIndex { get; set; }
        public List<string> BackupTypes { get; } = new() { "Full", "Differential" };

        public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseTargetCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand { get; }

        public AddEditBackupJobViewModel(BackupJob existingJob = null)
        {
            _originalJob = existingJob;

            if (existingJob != null)
            {
                Name = existingJob.Name;
                SourcePath = existingJob.SourcePath;
                TargetPath = existingJob.TargetPath;
                SelectedBackupTypeIndex = existingJob.Type == BackupType.Full ? 0 : 1;
            }

            BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSource);
            BrowseTargetCommand = ReactiveCommand.CreateFromTask(BrowseTarget);
            SaveCommand = ReactiveCommand.Create(Save);
            CancelCommand = ReactiveCommand.Create(Cancel);
        }

        private async Task BrowseSource()
        {
            var dialog = new OpenFolderDialog { Title = "Select Source Directory" };
            var mainWindow = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var result = await dialog.ShowAsync(mainWindow);
            if (!string.IsNullOrEmpty(result))
            {
                SourcePath = result;
                this.RaisePropertyChanged(nameof(SourcePath));
            }
        }

        private async Task BrowseTarget()
        {
            var dialog = new OpenFolderDialog { Title = "Select Target Directory" };
            var mainWindow = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var result = await dialog.ShowAsync(mainWindow);
            if (!string.IsNullOrEmpty(result))
            {
                TargetPath = result;
                this.RaisePropertyChanged(nameof(TargetPath));
            }
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(Name))
                throw new ArgumentException("Name is required");
            if (string.IsNullOrWhiteSpace(SourcePath))
                throw new ArgumentException("Source path is required");
            if (string.IsNullOrWhiteSpace(TargetPath))
                throw new ArgumentException("Target path is required");
            if (!Directory.Exists(SourcePath))
                throw new DirectoryNotFoundException("Source directory does not exist");

            var result = new BackupJob
            {
                Id = _originalJob?.Id ?? 0,
                Name = Name,
                SourcePath = SourcePath,
                TargetPath = TargetPath,
                Type = SelectedBackupTypeIndex == 0 ? BackupType.Full : BackupType.Differential
            };

            var window = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (window != null)
            {
                window.Close(result);
            }
        }

        private void Cancel()
        {
            var window = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (window != null)
            {
                window.Close(null);
            }
        }
    }
}
