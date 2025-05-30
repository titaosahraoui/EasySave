using System;
using System.Diagnostics;
using System.IO;
using BackupApp.Models;
using BackupApp.Logging;
using BackupApp.services;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace BackupApp.Services
{
    public class BackupService : IBackupService
    {
        private readonly LanguageService _languageService;
        private readonly ILogger _logger;
        private readonly IStateManager _stateManager;
        private readonly ConcurrentDictionary<int, BackupTask> _activeTasks = new();
        private readonly PriorityFileQueue _priorityQueue = new();
        private readonly SemaphoreSlim _largeFileSemaphore = new(1, 1);
        private readonly BusinessSoftwareMonitor _softwareMonitor;
        private int _maxParallelFileSizeKB = 1024; // Configurable

        public BackupService(BusinessSoftwareMonitor softwareMonitor, LogFormat logFormat = LogFormat.Json )
        {
            _languageService = new LanguageService();
            _logger = logFormat == LogFormat.Json ? new FileLogger() : new XmlFileLogger();
            _stateManager = new FileStateManager();
            _softwareMonitor = softwareMonitor;
            _softwareMonitor.SoftwareRunningChanged += (isRunning) =>
            {
                foreach (var task in _activeTasks.Values)
                {
                    if (isRunning) task.Pause();
                    else task.Resume();
                }
            };
        }

        public async Task PerformBackupAsync(BackupJob job, IProgress<BackupProgressReport> progress, CancellationToken cancellationToken)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            var allFiles = Directory.GetFiles(job.SourcePath, "*", SearchOption.AllDirectories);
            long totalSize = CalculateTotalSize(allFiles);
            long processedSize = 0;

            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(file);
                string destFile = GetDestinationPath(file, job.SourcePath, job.TargetPath);

                // Report progress before processing
                progress?.Report(new BackupProgressReport
                {
                    CurrentFile = Path.GetFileName(file),
                    ProgressPercentage = (double)processedSize / totalSize * 100
                });

                // Process the file (copy it)
                await TransferFile(file, destFile);

                // Update progress
                processedSize += fileInfo.Length;
                progress?.Report(new BackupProgressReport
                {
                    CurrentFile = Path.GetFileName(file),
                    ProgressPercentage = (double)processedSize / totalSize * 100
                });
            }
        }

        private async Task ProcessBackupJob(BackupJob job, BackupTask task, CancellationToken ct)
        {
            var state = InitializeBackupState(job);
            var files = GetFilesToBackup(job.SourcePath);
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(files, parallelOptions, async (file, innerCt) =>
            {
                await ProcessFile(job, file, task, state, innerCt);
            });

            FinalizeBackup(job, state, true);
        }

        private async Task ProcessFile(BackupJob job, string file, BackupTask task, BackupState state, CancellationToken ct)
        {
            var fileInfo = new FileInfo(file);
            bool isLargeFile = fileInfo.Length > _maxParallelFileSizeKB * 1024;
            string destFile = GetDestinationPath(file, job.SourcePath, job.TargetPath);

            try
            {
                if (isLargeFile) await _largeFileSemaphore.WaitAsync(ct);
                if (_priorityQueue.HasPriorityFiles() && !_priorityQueue.IsPriorityFile(file))
                {
                    await _priorityQueue.WaitForPriorityFiles(ct);
                }

                await task.WaitIfPaused(ct);
                ct.ThrowIfCancellationRequested();

                UpdateCurrentFileState(job, state, file, destFile);
                await TransferFile(file, GetDestinationPath(file, job.SourcePath, job.TargetPath));
                UpdateProgressState(job, state, fileInfo.Length);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(job.Name, $"Backup was cancelled during file transfer: {file}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(job.Name, $"{_languageService.GetString("FileCopyError")} {file}: {ex.Message}");
            }
            finally
            {
                if (isLargeFile) _largeFileSemaphore.Release();
            }
        }

        private string[] GetFilesToBackup(string sourcePath)
        {
            return Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
        }
        private async Task TransferFile(string sourcePath, string destinationPath)
        {
            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            using (var destinationStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
            {
                await sourceStream.CopyToAsync(destinationStream);
            }

            // Preserve original file timestamps
            var fileInfo = new FileInfo(sourcePath);
            File.SetLastWriteTime(destinationPath, fileInfo.LastWriteTime);
            File.SetCreationTime(destinationPath, fileInfo.CreationTime);
        }

        public void PauseBackup(int jobId)
        {
            if (_activeTasks.TryGetValue(jobId, out var task))
            {
                task.Pause();
            }
        }

        public void ResumeBackup(int jobId)
        {
            if (_activeTasks.TryGetValue(jobId, out var task))
            {
                task.Resume();
            }
        }

        public void StopBackup(int jobId)
        {
            if (_activeTasks.TryGetValue(jobId, out var task))
            {
                task.Stop();
            }
        }

        private BackupState InitializeBackupState(BackupJob job)
        {
            return new BackupState
            {
                BackupName = job.Name,
                Status = "Active",
                LastActionTimestamp = DateTime.Now,
                CurrentSourceFile = string.Empty,
                CurrentDestFile = string.Empty
            };
        }

        private void FinalizeBackup(BackupJob job, BackupState state, bool success)
        {
            job.LastRun = DateTime.Now;
            state.Status = success ? "Completed" : "Error";
            state.LastActionTimestamp = DateTime.Now;
            _stateManager.UpdateState(job.Name, state);
        }

        private void LogError(string context, string message)
        {
            Console.WriteLine(message);
            _logger.LogError(context, message);
        }

        private void LogWarning(string context, string message)
        {
            Console.WriteLine(message);
            _logger.LogWarning(context, message);
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

    public class BackupTask
    {
        private readonly SemaphoreSlim _pauseSemaphore = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private volatile TaskState _state = TaskState.Ready;

        public BackupJob Job { get; }
        public double Progress { get; private set; }
        public string CurrentFile { get; private set; }

        public BackupTask(BackupJob job)
        {
            Job = job;
        }

        public async Task WaitIfPaused(CancellationToken ct)
        {
            while (_state == TaskState.Paused)
            {
                await Task.Delay(100, ct);
            }
            ct.ThrowIfCancellationRequested();
        }

        public void Pause() => ChangeState(TaskState.Paused);
        public void Resume() => ChangeState(TaskState.Running);
        public void Stop() => _cts.Cancel();

        private void ChangeState(TaskState newState)
        {
            _state = newState;
            Job.Status = newState.ToString();
        }

        public enum TaskState { Ready, Running, Paused, Stopped }
    }

    public class PriorityFileQueue
    {
        private readonly HashSet<string> _priorityExtensions = new();
        private readonly SemaphoreSlim _prioritySemaphore = new(0, 1);

        public void SetPriorityExtensions(IEnumerable<string> extensions)
        {
            _priorityExtensions.Clear(); // Clear existing extensions
            foreach (var ext in extensions)
            {
                _priorityExtensions.Add(ext.StartsWith(".") ? ext : $".{ext}");
            }
        }

        public bool HasPriorityFiles() => _priorityExtensions.Count > 0;
        public bool IsPriorityFile(string filePath) =>
            _priorityExtensions.Contains(Path.GetExtension(filePath));

        public async Task WaitForPriorityFiles(CancellationToken ct)
        {
            while (HasPriorityFiles())
            {
                await Task.Delay(100, ct);
            }
        }
    }

    public class BusinessSoftwareMonitor
    {
        private readonly HashSet<string> _businessProcessNames;
        private readonly Timer _monitoringTimer;
        private bool _isRunning;

        public event Action<bool> SoftwareRunningChanged;

        public BusinessSoftwareMonitor(IEnumerable<string> processNames)
        {
            _businessProcessNames = new HashSet<string>(processNames, StringComparer.OrdinalIgnoreCase);
            _monitoringTimer = new Timer(CheckProcesses, null, 0, 1000);
        }

        private void CheckProcesses(object state)
        {
            bool anyRunning = Process.GetProcesses()
                .Any(p => _businessProcessNames.Contains(p.ProcessName));

            if (anyRunning != _isRunning)
            {
                _isRunning = anyRunning;
                SoftwareRunningChanged?.Invoke(_isRunning);
            }
        }
    }
    public class BackupProgressReport
    {
        public double ProgressPercentage { get; set; }
        public string CurrentFile { get; set; }
    }

}