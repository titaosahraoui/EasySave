using System;
using System.Diagnostics;
using System.IO;
using BackupApp.Models;
using BackupApp.Logging;

namespace BackupApp.Services
{
    public class BackupService
    {
        private readonly LanguageService _languageService;
        private readonly ILogger _logger;
        private readonly IStateManager _stateManager;

        public BackupService()
        {
            _languageService = new LanguageService();
            _logger = new FileLogger();
            _stateManager = new FileStateManager();
        }

        public void PerformBackup(BackupJob job)
        {
            var state = new BackupState
            {
                BackupName = job.Name,
                Status = "Active",
                LastActionTimestamp = DateTime.Now
            };

            try
            {
                Console.WriteLine(_languageService.GetString("StartingBackup") + job.Name);
                _logger.LogFileTransfer(job.Name, job.SourcePath, job.TargetPath, 0, 0, true);

                // Check paths
                if (!Directory.Exists(job.SourcePath))
                {
                    string error = _languageService.GetString("SourceNotFound");
                    Console.WriteLine(error);
                    _logger.LogError(job.Name, error);
                    return;
                }

                if (!Directory.Exists(job.TargetPath))
                {
                    Directory.CreateDirectory(job.TargetPath);
                    _logger.LogDirectoryCreation(job.Name, job.TargetPath);
                }

                // Initialize state
                var allFiles = Directory.GetFiles(job.SourcePath, "*", SearchOption.AllDirectories);
                state.TotalFiles = allFiles.Length;
                state.TotalSizeBytes = CalculateTotalSize(allFiles);
                _stateManager.UpdateState(job.Name, state);

                // Copy files
                CopyDirectory(job, state, job.SourcePath, job.TargetPath, job.Type == BackupType.Differential);

                // Update last run time and state
                job.LastRun = DateTime.Now;
                state.Status = "Completed";
                state.LastActionTimestamp = DateTime.Now;
                _stateManager.UpdateState(job.Name, state);

                Console.WriteLine(_languageService.GetString("BackupComplete") + job.Name);
            }
            catch (Exception ex)
            {
                state.Status = "Error";
                _stateManager.UpdateState(job.Name, state);

                string error = _languageService.GetString("BackupError") + ex.Message;
                Console.WriteLine(error);
                _logger.LogError(job.Name, error);
            }


        }
        private void CopyDirectory(BackupJob job, BackupState state, string sourceDir, string targetDir, bool differential)
        {
            var files = Directory.GetFiles(sourceDir);
            var directories = Directory.GetDirectories(sourceDir);
            DateTime? lastBackupTime = differential ? GetLastBackupTime(targetDir) : null;

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                string destFile = GetDestinationPath(file, sourceDir, targetDir);

                // Update current file in state
                state.CurrentSourceFile = file;
                state.CurrentDestFile = destFile;
                state.LastActionTimestamp = DateTime.Now;
                _stateManager.UpdateState(job.Name, state);

                // Differential backup check
                if (differential && File.Exists(destFile))
                {
                    FileInfo sourceInfo = new FileInfo(file);
                    FileInfo targetInfo = new FileInfo(destFile);

                    if (sourceInfo.LastWriteTime <= targetInfo.LastWriteTime &&
                        (!lastBackupTime.HasValue || targetInfo.LastWriteTime >= lastBackupTime))
                    {
                        state.FilesProcessed++;
                        continue;
                    }
                }

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    File.Copy(file, destFile, true);
                    stopwatch.Stop();

                    long fileSize = new FileInfo(file).Length;
                    _logger.LogFileTransfer(job.Name, file, destFile, fileSize, stopwatch.ElapsedMilliseconds, true);
                    Console.WriteLine($"{_languageService.GetString("CopyingFile")}: {file} → {destFile}");
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger.LogFileTransfer(job.Name, file, destFile, 0, -stopwatch.ElapsedMilliseconds, false);
                    Console.WriteLine($"{_languageService.GetString("CopyError")} {file}: {ex.Message}");
                }
                finally
                {
                    state.FilesProcessed++;
                    state.FilesRemaining = state.TotalFiles - state.FilesProcessed;
                    if (File.Exists(file))
                    {
                        state.SizeRemainingBytes -= new FileInfo(file).Length;
                    }
                    _stateManager.UpdateState(job.Name, state);
                }
            }

            foreach (var directory in directories)
            {
                string dirName = Path.GetFileName(directory);
                string destDir = GetDestinationPath(directory, sourceDir, targetDir);

                if (!Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                    _logger.LogDirectoryCreation(job.Name, destDir);
                    Console.WriteLine($"{_languageService.GetString("CreatingFolder")}: {destDir}");
                }

                CopyDirectory(job, state, directory, destDir, differential);
            }
        }

        private long CalculateTotalSize(string[] files)
        {
            long total = 0;
            foreach (var file in files)
            {
                try { total += new FileInfo(file).Length; }
                catch { /* Skip inaccessible files */ }
            }
            return total;
        }

        private DateTime? GetLastBackupTime(string targetDir)
        {
            try
            {
                return Directory.Exists(targetDir)
                    ? new DirectoryInfo(targetDir)
                        .GetFiles("*", SearchOption.AllDirectories)
                        .Max(f => (DateTime?)f.LastWriteTime)
                    : null;
            }
            catch
            {
                return null;
            }
        }
        private string GetDestinationPath(string sourcePath, string sourceRoot, string targetRoot)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            return Path.Combine(targetRoot, relativePath);
        }
    }
  
}