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

        public BackupService(LogFormat logFormat = LogFormat.Json)
        {
            _languageService = new LanguageService();
            _logger = logFormat == LogFormat.Json ? new FileLogger() : new XmlFileLogger();
            _stateManager = new FileStateManager();
        }

        public void PerformBackup(BackupJob job)
        {
            if (job == null)
            {
                string nullError = _languageService.GetString("JobNullError");
                Console.WriteLine(nullError);
                _logger.LogError("System", nullError);
                return;
            }

            var state = new BackupState
            {
                BackupName = job.Name,
                Status = "Active",
                LastActionTimestamp = DateTime.Now,
                CurrentSourceFile = string.Empty,
                CurrentDestFile = string.Empty
            };

            try
            {
                // Log backup start
                Console.WriteLine($"{_languageService.GetString("StartingBackup")} {job.Name} ({job.Type})");
                _logger.LogFileTransfer(job.Name, job.SourcePath, job.TargetPath, 0, 0, true);

                // Validate paths
                if (!Directory.Exists(job.SourcePath))
                {
                    string error = $"{_languageService.GetString("SourceNotFound")}: {job.SourcePath}";
                    Console.WriteLine(error);
                    _logger.LogError(job.Name, error);
                    state.Status = "Error";
                    _stateManager.UpdateState(job.Name, state);
                    return;
                }

                // Ensure target directory exists
                if (!Directory.Exists(job.TargetPath))
                {
                    try
                    {
                        Directory.CreateDirectory(job.TargetPath);
                        _logger.LogDirectoryCreation(job.Name, job.TargetPath);
                        Console.WriteLine($"{_languageService.GetString("CreatedDirectory")}: {job.TargetPath}");
                    }
                    catch (Exception ex)
                    {
                        string error = $"{_languageService.GetString("DirCreateError")}: {job.TargetPath} - {ex.Message}";
                        Console.WriteLine(error);
                        _logger.LogError(job.Name, error);
                        state.Status = "Error";
                        _stateManager.UpdateState(job.Name, state);
                        return;
                    }
                }

                // Get files and update state
                var allFiles = Directory.GetFiles(job.SourcePath, "*", SearchOption.AllDirectories);
                state.TotalFiles = allFiles.Length;
                state.TotalSizeBytes = CalculateTotalSize(allFiles);
                state.FilesRemaining = state.TotalFiles;
                state.SizeRemainingBytes = state.TotalSizeBytes;
                _stateManager.UpdateState(job.Name, state);

                // Execute backup based on type
                var stopwatch = Stopwatch.StartNew();
                if (job.Type == BackupType.Full)
                {
                    ExecuteFullBackup(job, state, allFiles);
                }
                else
                {
                    ExecuteDifferentialBackup(job, state, allFiles);
                }
                stopwatch.Stop();

                // Finalize backup
                job.LastRun = DateTime.Now;
                state.Status = "Completed";
                state.LastActionTimestamp = DateTime.Now;
                
                _stateManager.UpdateState(job.Name, state);

                Console.WriteLine($"{_languageService.GetString("BackupComplete")} {job.Name}");
                Console.WriteLine($"{_languageService.GetString("BackupStats")} {state.TotalFiles} files, {FormatSize(state.TotalSizeBytes)}, {stopwatch.Elapsed.TotalSeconds:0.00}s");
            }
            catch (Exception ex)
            {
                state.Status = "Error";
                _stateManager.UpdateState(job.Name, state);

                string error = $"{_languageService.GetString("BackupError")} {job.Name}: {ex.Message}";
                Console.WriteLine(error);
                _logger.LogError(job.Name, error);
            }
        }
        private void ExecuteFullBackup(BackupJob job, BackupState state, string[] allFiles)
        {
            foreach (var file in allFiles)
            {
                try
                {
                    string destFile = GetDestinationPath(file, job.SourcePath, job.TargetPath);
                    UpdateCurrentFileState(job, state, file, destFile);

                    var fileStopwatch = Stopwatch.StartNew();
                    File.Copy(file, destFile, true);
                    fileStopwatch.Stop();

                    long fileSize = new FileInfo(file).Length;
                    _logger.LogFileTransfer(job.Name, file, destFile, fileSize, fileStopwatch.ElapsedMilliseconds, true);
                    UpdateProgressState(job, state, fileSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError(job.Name, $"{_languageService.GetString("FileCopyError")} {file}: {ex.Message}");
                }
            }
        }

        private void ExecuteDifferentialBackup(BackupJob job, BackupState state, string[] allFiles)
        {
            DateTime? lastBackupTime = GetLastBackupTime(job.TargetPath);

            foreach (var file in allFiles)
            {
                string destFile = GetDestinationPath(file, job.SourcePath, job.TargetPath);
                UpdateCurrentFileState(job, state, file, destFile);

                if (ShouldSkipFile(file, destFile, lastBackupTime))
                {
                    state.FilesProcessed++;
                    state.FilesRemaining--;
                    continue;
                }

                try
                {
                    var fileStopwatch = Stopwatch.StartNew();
                    File.Copy(file, destFile, true);
                    fileStopwatch.Stop();

                    long fileSize = new FileInfo(file).Length;
                    _logger.LogFileTransfer(job.Name, file, destFile, fileSize, fileStopwatch.ElapsedMilliseconds, true);
                    UpdateProgressState(job, state, fileSize);
                }
                catch (Exception ex)
                {
                    _logger.LogError(job.Name, $"{_languageService.GetString("FileCopyError")} {file}: {ex.Message}");
                }
            }
        }

        private void UpdateCurrentFileState(BackupJob job, BackupState state, string sourceFile, string destFile)
        {
            state.CurrentSourceFile = sourceFile;
            state.CurrentDestFile = destFile;
            state.LastActionTimestamp = DateTime.Now;
            _stateManager.UpdateState(job.Name, state);
        }

        private void UpdateProgressState(BackupJob job, BackupState state, long fileSize)
        {
            state.FilesProcessed++;
            state.FilesRemaining--;
            state.SizeRemainingBytes -= fileSize;
            _stateManager.UpdateState(job.Name, state);
        }

        private bool ShouldSkipFile(string sourceFile, string destFile, DateTime? lastBackupTime)
        {
            if (!File.Exists(destFile)) return false;

            FileInfo sourceInfo = new FileInfo(sourceFile);
            FileInfo destInfo = new FileInfo(destFile);

            return sourceInfo.LastWriteTime <= destInfo.LastWriteTime &&
                  (!lastBackupTime.HasValue || destInfo.LastWriteTime >= lastBackupTime);
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            while (bytes >= 1024 && order < sizes.Length - 1)
            {
                order++;
                bytes /= 1024;
            }
            return $"{bytes:0.##} {sizes[order]}";
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