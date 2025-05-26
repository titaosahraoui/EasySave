namespace BackupApp.Models
{
    public class BackupJob
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? SourcePath { get; set; }
        public string? TargetPath { get; set; }
        public BackupType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastRun { get; set; }
    }

    public enum BackupType
    {
        Full,
        Differential
    }
}