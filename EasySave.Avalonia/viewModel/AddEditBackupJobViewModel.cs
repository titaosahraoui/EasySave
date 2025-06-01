using BackupApp.Models;
using BackupApp.Data;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System;
using System.IO;
using ReactiveUI;

namespace BackupApp.ViewModels
{
    public class AddEditBackupJobViewModel : ViewModelBase
    {
        private readonly Window _window;
        private readonly BackupRepository _repository;
        private readonly BackupViewModel _backupViewModel;
        private BackupJob _currentJob = new();

        public string WindowTitle => CurrentJob.Id == 0 ? "Add Backup Job" : "Edit Backup Job";

        public BackupJob CurrentJob
        {
            get => _currentJob;
            set => SetProperty(ref _currentJob, value);
        }

        public ObservableCollection<BackupType> BackupTypes { get; } = new()
        {
            BackupType.Full,
            BackupType.Differential
        };

        public ICommand BrowseSourceCommand { get; }
        public ICommand BrowseTargetCommand { get; }
        public RelayCommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public AddEditBackupJobViewModel(Window window, BackupJob existingJob = null)
        {
            _window = window;
            _repository = new BackupRepository();
            _backupViewModel = new BackupViewModel();

            if (existingJob != null)
            {
                CurrentJob = new BackupJob
                {
                    Id = existingJob.Id,
                    Name = existingJob.Name,
                    SourcePath = existingJob.SourcePath,
                    TargetPath = existingJob.TargetPath,
                    Type = existingJob.Type,
                    CreatedAt = existingJob.CreatedAt,
                    LastRun = existingJob.LastRun
                };
            }

            BrowseSourceCommand = new AsyncRelayCommand(BrowseSource);
            BrowseTargetCommand = new AsyncRelayCommand(BrowseTarget);
            SaveCommand = new RelayCommand(Save, CanSave);
            CancelCommand = new RelayCommand(Cancel);

            // Hook into property changes to re-evaluate SaveCommand
            CurrentJob.PropertyChanged += (_, __) => SaveCommand.RaiseCanExecuteChanged();
        }

        private bool CanSave() =>
            !string.IsNullOrWhiteSpace(CurrentJob.Name) &&
            !string.IsNullOrWhiteSpace(CurrentJob.SourcePath) &&
            !string.IsNullOrWhiteSpace(CurrentJob.TargetPath) &&
            Directory.Exists(CurrentJob.SourcePath) &&
            !CurrentJob.SourcePath.Equals(CurrentJob.TargetPath, StringComparison.OrdinalIgnoreCase);

        private async Task BrowseSource()
        {
            try
            {
                var result = await _window.StorageProvider.OpenFolderPickerAsync(new()
                {
                    Title = "Select Source Directory",
                    AllowMultiple = false
                });

                if (result.Count > 0 && result[0].TryGetLocalPath() is string path)
                {
                    CurrentJob.SourcePath = path;
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to select source directory", ex);
            }
        }

        private async Task BrowseTarget()
        {
            try
            {
                var result = await _window.StorageProvider.OpenFolderPickerAsync(new()
                {
                    Title = "Select Target Directory",
                    AllowMultiple = false
                });

                if (result.Count > 0 && result[0].TryGetLocalPath() is string path)
                {
                    CurrentJob.TargetPath = path;
                }
            }
            catch (Exception ex)
            {
                ShowError("Failed to select target directory", ex);
            }
        }

        private void Save()
        {
            try
            {
                if (!Directory.Exists(CurrentJob.SourcePath))
                {
                    ShowError("Source directory does not exist");
                    return;
                }

                if (CurrentJob.SourcePath.Equals(CurrentJob.TargetPath, StringComparison.OrdinalIgnoreCase))
                {
                    ShowError("Source and target paths cannot be the same");
                    return;
                }

                if (!Directory.Exists(CurrentJob.TargetPath))
                {
                    try
                    {
                        Directory.CreateDirectory(CurrentJob.TargetPath);
                    }
                    catch (Exception ex)
                    {
                        ShowError("Failed to create target directory", ex);
                        return;
                    }
                }

                if (CurrentJob.Id == 0)
                {
                    _repository.AddBackupJob(CurrentJob);
                    ShowAlert("Backup job created successfully");
                }
                else
                {
                    _repository.UpdateBackupJob(CurrentJob);
                    ShowAlert("Backup job updated successfully");
                }
               
                _window.Close(CurrentJob);
            }
            catch (Exception ex)
            {
                ShowError("Failed to save backup job", ex);
            }
        }

        private void Cancel() => _window.Close(null);
    }
}
