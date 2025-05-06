using System;
using System.IO;
using BackupApp.Models;

namespace BackupApp.Services
{
    public class BackupService
    {
        private readonly LanguageService _languageService;

        public BackupService()
        {
            _languageService = new LanguageService();
        }

        public void PerformBackup(BackupJob job)
        {
            try
            {
                Console.WriteLine(_languageService.GetString("StartingBackup") + job.Name);

                // Check paths
                if (!Directory.Exists(job.SourcePath))
                {
                    Console.WriteLine(_languageService.GetString("SourceNotFound"));
                    return;
                }

                if (!Directory.Exists(job.TargetPath))
                {
                    Directory.CreateDirectory(job.TargetPath);
                }

                // Copy files
                CopyDirectory(job.SourcePath, job.TargetPath, job.Type == BackupType.Differential);

                // Update last run time
                job.LastRun = DateTime.Now;
                Console.WriteLine(_languageService.GetString("BackupComplete") + job.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine(_languageService.GetString("BackupError") + ex.Message);
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir, bool differential)
        {
            // Get all files and subdirectories
            var files = Directory.GetFiles(sourceDir);
            var directories = Directory.GetDirectories(sourceDir);

            // Handle differential backup
            DateTime? lastBackupTime = differential ? GetLastBackupTime(targetDir) : null;

            // Copy all files
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName);

                // For differential backup, only copy if file is new or modified
                if (differential && File.Exists(destFile))
                {
                    FileInfo sourceInfo = new FileInfo(file);
                    FileInfo targetInfo = new FileInfo(destFile);

                    // Skip if file hasn't changed since last backup
                    if (sourceInfo.LastWriteTime <= targetInfo.LastWriteTime &&
                        (!lastBackupTime.HasValue || targetInfo.LastWriteTime >= lastBackupTime))
                    {
                        continue;
                    }
                }

                try
                {
                    File.Copy(file, destFile, true); // Overwrite if exists
                    Console.WriteLine($"{_languageService.GetString("CopyingFile")}: {file} → {destFile}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{_languageService.GetString("CopyError")} {file}: {ex.Message}");
                }
            }

            // Recursively copy subdirectories
            foreach (var directory in directories)
            {
                string dirName = Path.GetFileName(directory);
                string destDir = Path.Combine(targetDir, dirName);

                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    Console.WriteLine($"{_languageService.GetString("CreatingFolder")}: {destDir}");
                }

                CopyDirectory(directory, destDir, differential);
            }
        }

        private DateTime? GetLastBackupTime(string targetDir)
        {
            try
            {
                // Check if target directory exists and has files
                if (!Directory.Exists(targetDir) || !Directory.GetFiles(targetDir).Any())
                {
                    return null; // No previous backup
                }

                // Get the most recent file modification time in target directory
                return new DirectoryInfo(targetDir)
                    .GetFiles("*", SearchOption.AllDirectories)
                    .Max(f => f.LastWriteTime);
            }
            catch
            {
                return null;
            }
        }
    }
}