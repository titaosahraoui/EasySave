using Avalonia.Controls;
using BackupApp.Models;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using System;
using Avalonia.Platform.Storage;
using System.Collections.ObjectModel;

public class AddEditBackupJobViewModel : ReactiveObject
{
    private readonly Window _window;
    private BackupJob _currentJob = new();

    public BackupJob CurrentJob
    {
        get => _currentJob;
        set => this.RaiseAndSetIfChanged(ref _currentJob, value);
    }
    public ObservableCollection<BackupType> BackupTypes { get; } = new ObservableCollection<BackupType>
    {
        BackupType.Full,
        BackupType.Differential
    };

    public ReactiveCommand<Unit, Unit> BrowseSourceCommand { get; } = null!;
    public ReactiveCommand<Unit, Unit> BrowseTargetCommand { get; } = null!;
    public ReactiveCommand<Unit, Unit> SaveCommand { get; } = null!;
    public ReactiveCommand<Unit, Unit> CancelCommand { get; } = null!;

    public AddEditBackupJobViewModel(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));

        BrowseSourceCommand = ReactiveCommand.CreateFromTask(BrowseSource);
        BrowseTargetCommand = ReactiveCommand.CreateFromTask(BrowseTarget);
        SaveCommand = ReactiveCommand.Create(Save);
        CancelCommand = ReactiveCommand.Create(Cancel);


    }

    private async Task BrowseSource()
    {
        var result = await _window.StorageProvider.OpenFolderPickerAsync(new()
        {
            Title = "Select Source Directory",
            AllowMultiple = false
        });

        if (result.Count > 0 && result[0] is IStorageFolder folder && folder.Path != null)
        {
            CurrentJob.SourcePath = folder.Path.LocalPath;
        }
    }

    private async Task BrowseTarget()
    {
        var result = await _window.StorageProvider.OpenFolderPickerAsync(new()
        {
            Title = "Select Target Directory",
            AllowMultiple = false
        });

        if (result.Count > 0 && result[0] is IStorageFolder folder && !string.IsNullOrEmpty(folder.Path.LocalPath))
        {
            CurrentJob.TargetPath = folder.Path.LocalPath;
        }
    }

    private void Save() => _window.Close(CurrentJob);
    private void Cancel() => _window.Close(null);
}