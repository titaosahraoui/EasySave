using ReactiveUI;
using System;

namespace BackupApp.Models
{
    public class BackupJob : ReactiveObject
    {
        private int _id;
        private string? _name;
        private string? _sourcePath;
        private string? _targetPath;
        private BackupType _type;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _lastRun;
        private string _status = "Pending";
        public string Status
        {
            get => _status;
            set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public int Id
        {
            get => _id;
            set => this.RaiseAndSetIfChanged(ref _id, value);
        }

        public string? Name
        {
            get => _name;
            set => this.RaiseAndSetIfChanged(ref _name, value);
        }

        public string? SourcePath
        {
            get => _sourcePath;
            set => this.RaiseAndSetIfChanged(ref _sourcePath, value);
        }

        public string? TargetPath
        {
            get => _targetPath;
            set => this.RaiseAndSetIfChanged(ref _targetPath, value);
        }

        public BackupType Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => this.RaiseAndSetIfChanged(ref _createdAt, value);
        }

        public DateTime? LastRun
        {
            get => _lastRun;
            set => this.RaiseAndSetIfChanged(ref _lastRun, value);
        }
    }

    public enum BackupType
    {
        Full,
        Differential
    }
}