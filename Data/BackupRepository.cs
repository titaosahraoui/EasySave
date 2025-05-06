using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BackupApp.Models;

namespace BackupApp.Data
{
    public class BackupRepository
    {
        private const string DataFileName = "backupJobs.json";
        private List<BackupJob> _backupJobs;
        private readonly object _lock = new object();

        public BackupRepository()
        {
            LoadBackupJobs();
        }

        public void AddBackupJob(BackupJob job)
        {
            lock (_lock)
            {
                // Assign the next available ID (1-5)
                job.Id = _backupJobs.Any() ? _backupJobs.Max(j => j.Id) + 1 : 1;

                // Ensure we don't exceed 5 jobs
                if (_backupJobs.Count >= 5)
                {
                    throw new InvalidOperationException("Maximum of 5 backup jobs reached");
                }

                _backupJobs.Add(job);
                SaveBackupJobs();
            }
        }

        public BackupJob GetBackupJob(int id)
        {
            return _backupJobs.FirstOrDefault(j => j.Id == id);
        }

        public List<BackupJob> GetAllBackupJobs()
        {
            return new List<BackupJob>(_backupJobs);
        }

        public void UpdateBackupJob(BackupJob job)
        {
            lock (_lock)
            {
                var existingJob = _backupJobs.FirstOrDefault(j => j.Id == job.Id);
                if (existingJob != null)
                {
                    existingJob.Name = job.Name;
                    existingJob.SourcePath = job.SourcePath;
                    existingJob.TargetPath = job.TargetPath;
                    existingJob.Type = job.Type;
                    SaveBackupJobs();
                }
            }
        }

        public void DeleteBackupJob(int id)
        {
            lock (_lock)
            {
                var jobToRemove = _backupJobs.FirstOrDefault(j => j.Id == id);
                if (jobToRemove != null)
                {
                    _backupJobs.Remove(jobToRemove);
                    SaveBackupJobs();
                }
            }
        }

        private void LoadBackupJobs()
        {
            try
            {
                if (File.Exists(DataFileName))
                {
                    var json = File.ReadAllText(DataFileName);
                    _backupJobs = System.Text.Json.JsonSerializer.Deserialize<List<BackupJob>>(json)
                                 ?? new List<BackupJob>();
                }
                else
                {
                    _backupJobs = new List<BackupJob>();
                }
            }
            catch
            {
                _backupJobs = new List<BackupJob>();
            }
        }

        private void SaveBackupJobs()
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                var json = System.Text.Json.JsonSerializer.Serialize(_backupJobs, options);
                File.WriteAllText(DataFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving backup jobs: {ex.Message}");
            }
        }
    }
}