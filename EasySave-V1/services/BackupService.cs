using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BackupApp.Models;
using BackupApp.Logging;
using BackupApp.services;

namespace BackupApp.Services
{
    public class BackupService : IBackupService
    {
        private readonly LanguageService _languageService;
        private readonly ILogger _logger;
        private readonly IStateManager _stateManager;
        private readonly ICryptoService _cryptoService;
        private readonly CryptoConfig _cryptoConfig;
        private readonly ConcurrentDictionary<int, BackupTask> _activeTasks = new();
        private readonly PriorityFileQueue _priorityQueue = new();
        private readonly SemaphoreSlim _largeFileSemaphore = new(1, 1);
        private readonly BusinessSoftwareMonitor _softwareMonitor;
        private int _maxParallelFileSizeKB = 1024; // Configurable

        public BackupService(BusinessSoftwareMonitor softwareMonitor, LogFormat logFormat = LogFormat.Json, CryptoConfig cryptoConfig = null)
        {
            _languageService = new LanguageService();
            _logger = logFormat == LogFormat.Json ? new FileLogger() : new XmlFileLogger();
            _stateManager = new FileStateManager();
            _cryptoConfig = cryptoConfig ?? new CryptoConfig();
            _cryptoService = new CryptoService(_logger, _cryptoConfig);
            _softwareMonitor = softwareMonitor;

            var priorityExtensions = AppConfig.Load().PriorityExtensions;
            _priorityQueue.SetPriorityExtensions(priorityExtensions);

            _softwareMonitor.SoftwareRunningChanged += (isRunning) =>
            {
                foreach (var task in _activeTasks.Values)
                {
                    if (isRunning) task.Pause();
                    else task.Resume();
                }
            };
        }

        public async Task PerformBackupAsync(BackupJob job, IProgress<BackupProgressReport> progress, CancellationToken externalToken)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            var task = new BackupTask(job);
            if (!_activeTasks.TryAdd(job.Id, task))
            {
                throw new InvalidOperationException($"A backup task for job {job.Id} already exists");
            }

            try
            {
                // Create linked token source
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, task.CancellationToken);
                var token = linkedCts.Token;

                var allFiles = Directory.GetFiles(job.SourcePath, "*", SearchOption.AllDirectories);
                long totalSize = CalculateTotalSize(allFiles);
                long processedSize = 0;

                // Initialize backup state
                var state = new BackupState
                {
                    BackupName = job.Name,
                    Status = "Active",
                    LastActionTimestamp = DateTime.Now,
                    CurrentSourceFile = string.Empty,
                    CurrentDestFile = string.Empty,
                    TotalFiles = allFiles.Length,
                    TotalSizeBytes = totalSize,
                    FilesRemaining = allFiles.Length,
                    SizeRemainingBytes = totalSize
                };
                _stateManager.UpdateState(job.Name, state);

                // Either process files sequentially or in parallel
                if (job.Type == BackupType.Full && allFiles.Length > 100) // Example condition for parallel
                {
                    await ProcessBackupJob(job, task, token, progress, totalSize);
                }
                else
                {
                    DateTime? lastBackupTime = job.Type == BackupType.Differential ? GetLastBackupTime(job.TargetPath) : null;

                    foreach (var file in allFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        await task.WaitIfPaused(token);

                        string destFile = GetDestinationPath(file, job.SourcePath, job.TargetPath);

                        // Differential backup check
                        if (job.Type == BackupType.Differential && ShouldSkipFile(file, destFile, lastBackupTime))
                        {
                            state.FilesProcessed++;
                            state.FilesRemaining--;
                            continue;
                        }

                        var fileInfo = new FileInfo(file);

                        progress?.Report(new BackupProgressReport
                        {
                            CurrentFile = Path.GetFileName(file),
                            ProgressPercentage = (double)processedSize / totalSize * 100
                        });

                        UpdateCurrentFileState(job, state, file, destFile);
                        await TransferFile(job, file, destFile, token);
                        UpdateProgressState(job, state, fileInfo.Length);

                        processedSize += fileInfo.Length;
                        progress?.Report(new BackupProgressReport
                        {
                            CurrentFile = Path.GetFileName(file),
                            ProgressPercentage = (double)processedSize / totalSize * 100
                        });
                    }
                }

                job.Status = "Completed";
                job.LastRun = DateTime.Now;
                state.Status = "Completed";
                state.LastActionTimestamp = DateTime.Now;
                _stateManager.UpdateState(job.Name, state);
            }
            catch (OperationCanceledException)
            {
                job.Status = "Cancelled";
                throw;
            }
            catch (Exception ex)
            {
                job.Status = "Error";
                _logger.LogError(job.Name, $"{_languageService.GetString("BackupError")}: {ex.Message}");
                throw;
            }
            finally
            {
                _activeTasks.TryRemove(job.Id, out _);
            }
        }

        private async Task ProcessBackupJob(BackupJob job, BackupTask task, CancellationToken ct, IProgress<BackupProgressReport> progress, long totalSize)
        {
            var state = new BackupState
            {
                BackupName = job.Name,
                Status = "Active",
                LastActionTimestamp = DateTime.Now,
                CurrentSourceFile = string.Empty,
                CurrentDestFile = string.Empty
            };

            var files = GetFilesToBackup(job.SourcePath);
            DateTime? lastBackupTime = job.Type == BackupType.Differential ? GetLastBackupTime(job.TargetPath) : null;

            // Create counters for thread-safe progress tracking
            long processedFiles = 0;
            long processedSize = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(files, parallelOptions, async (file, innerCt) =>
            {
                var fileInfo = new FileInfo(file);
                string destFile = GetDestinationPath(file, job.SourcePath, job.TargetPath);

                // Differential backup check
                if (job.Type == BackupType.Differential && ShouldSkipFile(file, destFile, lastBackupTime))
                {
                    // Thread-safe increment
                    Interlocked.Increment(ref processedFiles);
                    return;
                }

                progress?.Report(new BackupProgressReport
                {
                    CurrentFile = Path.GetFileName(file),
                    ProgressPercentage = (double)Interlocked.Read(ref processedSize) / totalSize * 100
                });

                await ProcessFile(job, file, task, state, innerCt, () =>
                {
                    // Update state after file processing
                    state.FilesProcessed = (int)Interlocked.Increment(ref processedFiles);
                    state.FilesRemaining = files.Length - state.FilesProcessed;
                    state.SizeRemainingBytes = totalSize - Interlocked.Add(ref processedSize, fileInfo.Length);
                    _stateManager.UpdateState(job.Name, state);
                });

                progress?.Report(new BackupProgressReport
                {
                    CurrentFile = Path.GetFileName(file),
                    ProgressPercentage = (double)Interlocked.Read(ref processedSize) / totalSize * 100
                });
            });

            FinalizeBackup(job, state, true);
        }

        private async Task ProcessFile(BackupJob job, string file, BackupTask task, BackupState state, CancellationToken ct, Action updateState)
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
                await TransferFile(job, file, destFile, ct);

                // Call the state update callback
                updateState?.Invoke();
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

        private async Task TransferFile(BackupJob job, string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            // Create directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

            try
            {
                using var sourceStream = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);

                using var destinationStream = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);

                await sourceStream.CopyToAsync(destinationStream, 81920, cancellationToken);

                // Preserve original file timestamps
                var fileInfo = new FileInfo(sourcePath);
                File.SetLastWriteTime(destinationPath, fileInfo.LastWriteTime);
                File.SetCreationTime(destinationPath, fileInfo.CreationTime);

                // Encrypt file if needed
                if (job.EnableEncryption && _cryptoService.ShouldEncryptFile(destinationPath, _cryptoConfig))
                {
                    string encryptionKey = !string.IsNullOrEmpty(job.EncryptionKey)
                        ? job.EncryptionKey
                        : _cryptoConfig.EncryptionKey;
                    Console.WriteLine("ENCRYPTION TRIGGERED");

                    if (string.IsNullOrEmpty(encryptionKey))
                    {
                        throw new InvalidOperationException("No encryption key provided");
                    }

                    bool encryptionSuccess = await _cryptoService.EncryptFileAsync(destinationPath, encryptionKey);
                    if (!encryptionSuccess)
                    {
                        throw new Exception($"Failed to encrypt file: {destinationPath}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Clean up partially copied file
                if (File.Exists(destinationPath))
                {
                    try { File.Delete(destinationPath); } catch { }
                }
                throw;
            }
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
                _activeTasks.TryRemove(jobId, out _);
            }
        }

        #region Utility Methods

        private string[] GetFilesToBackup(string sourcePath)
        {
            return Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
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

        private void FinalizeBackup(BackupJob job, BackupState state, bool success)
        {
            job.LastRun = DateTime.Now;
            state.Status = success ? "Completed" : "Error";
            state.LastActionTimestamp = DateTime.Now;
            _stateManager.UpdateState(job.Name, state);
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

        private bool ShouldSkipFile(string sourceFile, string destFile, DateTime? lastBackupTime)
        {
            if (!File.Exists(destFile)) return false;

            FileInfo sourceInfo = new FileInfo(sourceFile);
            FileInfo destInfo = new FileInfo(destFile);

            return sourceInfo.LastWriteTime <= destInfo.LastWriteTime &&
                  (!lastBackupTime.HasValue || destInfo.LastWriteTime >= lastBackupTime);
        }

        private string GetDestinationPath(string sourcePath, string sourceRoot, string targetRoot)
        {
            string relativePath = Path.GetRelativePath(sourceRoot, sourcePath);
            return Path.Combine(targetRoot, relativePath);
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

        #endregion
    }

    public class BackupTask
    {
        private readonly SemaphoreSlim _pauseLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private volatile bool _isPaused;
        private volatile bool _isStopped;

        public BackupJob Job { get; }
        public CancellationToken CancellationToken => _cts.Token;

        public BackupTask(BackupJob job)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
        }

        public async Task WaitIfPaused(CancellationToken ct)
        {
            if (_isPaused && !_isStopped)
            {
                await _pauseLock.WaitAsync(ct);
                _pauseLock.Release();
                ct.ThrowIfCancellationRequested();
            }
        }

        public void Pause()
        {
            if (!_isPaused && !_isStopped)
            {
                _isPaused = true;
                _pauseLock.WaitAsync(); // This will block the task
                Job.Status = "Paused";
            }
        }

        public void Resume()
        {
            if (_isPaused && !_isStopped)
            {
                _isPaused = false;
                _pauseLock.Release();
                Job.Status = "Running";
            }
        }

        public void Stop()
        {
            if (!_isStopped)
            {
                _isStopped = true;
                _isPaused = false;
                _cts.Cancel();
                try { _pauseLock.Release(); } catch { } // Unblock if paused
                Job.Status = "Stopped";
            }
        }
    }

    public class PriorityFileQueue
    {
        private readonly HashSet<string> _priorityExtensions = new();
        private readonly SemaphoreSlim _prioritySemaphore = new(0, 1);

        public void SetPriorityExtensions(IEnumerable<string> extensions)
        {
            _priorityExtensions.Clear();
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