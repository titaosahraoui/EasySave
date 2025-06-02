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
        private readonly NetworkMonitor _networkMonitor;
        private readonly RemoteConsoleService _remoteConsole;
        private int _maxParallelFileSizeKB = 1024; // Configurable

        public BackupService(BusinessSoftwareMonitor softwareMonitor, LogFormat logFormat = LogFormat.Json, CryptoConfig cryptoConfig = null)
        {
            // Load config once at the start
            var config = AppConfig.Load();

            _languageService = new LanguageService();
            _logger = logFormat == LogFormat.Json ? new FileLogger() : new XmlFileLogger();
            _stateManager = new FileStateManager();
            _networkMonitor = new NetworkMonitor();
            _networkMonitor.NetworkSpeedUpdated += OnNetworkSpeedChanged;

            _remoteConsole = new RemoteConsoleService(_stateManager, _logger);
            _ = _remoteConsole.StartAsync();
           
            // Initialize encryption - use provided config or create new
            _cryptoConfig = cryptoConfig ?? config.GetCryptoConfig();
            _cryptoService = new CryptoService(_logger, _cryptoConfig);
            _softwareMonitor = softwareMonitor;

            // Set priority extensions once using the loaded config
            _priorityQueue.SetPriorityExtensions(config.PriorityExtensions);

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

            // Get files and sort by priority (priority files first)
            var files = GetFilesToBackup(job.SourcePath)
                .OrderByDescending(f => _priorityQueue.IsPriorityFile(f))
                .ToArray();

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
                // Handle priority files
                bool isPriority = _priorityQueue.IsPriorityFile(file);
                if (isPriority)
                {
                    _priorityQueue.IncrementPriorityFiles();
                }
                else
                {
                    // Wait if there are any priority files pending
                    await _priorityQueue.WaitForNonPriorityFilesAsync(ct);
                }

                // Handle large files
                if (isLargeFile) await _largeFileSemaphore.WaitAsync(ct);

                await task.WaitIfPaused(ct);
                ct.ThrowIfCancellationRequested();

                UpdateCurrentFileState(job, state, file, destFile);
                await TransferFile(job, file, destFile, ct);
                updateState?.Invoke();
            }
            finally
            {
                if (isLargeFile) _largeFileSemaphore.Release();
                if (_priorityQueue.IsPriorityFile(file))
                {
                    _priorityQueue.DecrementPriorityFiles();
                }
            }
        }

        private async Task TransferFile(BackupJob job, string sourcePath, string destinationPath, CancellationToken cancellationToken)
        {
            // First copy the file normally
            await CopyFileWithRetry(sourcePath, destinationPath, cancellationToken);

            // Only check config.json setting for encryption (ignore job.EnableEncryption)
            if (_cryptoConfig.IsEnabled && _cryptoService.ShouldEncryptFile(destinationPath, _cryptoConfig))
            {
                await EncryptWithFileHandling(job, destinationPath);
            }
        }

        private async Task CopyFileWithRetry(string sourcePath, string destinationPath, CancellationToken ct, int retryCount = 3)
        {
            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));

                    using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous))
                    using (var destStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    {
                        await sourceStream.CopyToAsync(destStream, 81920, ct);
                    }

                    // Preserve timestamps
                    var fileInfo = new FileInfo(sourcePath);
                    File.SetLastWriteTime(destinationPath, fileInfo.LastWriteTime);
                    File.SetCreationTime(destinationPath, fileInfo.CreationTime);

                    return; // Success
                }
                catch (IOException) when (i < retryCount - 1)
                {
                    await Task.Delay(500 * (i + 1), ct); // Exponential backoff
                }
            }
            throw new IOException($"Failed to copy file after {retryCount} attempts: {sourcePath}");
        }

        private async Task EncryptWithFileHandling(BackupJob job, string filePath)
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                // 1. Make a working copy with retry logic
                await CopyFileWithRetry(filePath, tempFile, CancellationToken.None);

                // 2. Verify the copy was successful
                if (new FileInfo(tempFile).Length == 0)
                {
                    throw new Exception("Failed to create working copy - empty file");
                }

                // 3. Get the encryption key (use job-specific or default)
                string key = !string.IsNullOrEmpty(job.EncryptionKey)
                    ? job.EncryptionKey
                    : _cryptoConfig.EncryptionKey;

                if (string.IsNullOrEmpty(key))
                {
                    throw new Exception("No encryption key available");
                }

                // 4. Execute encryption with detailed logging
                _logger.LogInfo(job.Name, $"Starting encryption for: {filePath}");

                bool success = await _cryptoService.EncryptFileAsync(tempFile, key);
                if (!success)
                {
                    throw new Exception("Encryption process returned failure");
                }

                // 5. Verify encrypted file is different from original
                var originalBytes = await File.ReadAllBytesAsync(filePath);
                var encryptedBytes = await File.ReadAllBytesAsync(tempFile);
                if (originalBytes.SequenceEqual(encryptedBytes))
                {
                    throw new Exception("File unchanged after encryption");
                }

                // 6. Replace original with encrypted file
                await SafeFileReplace(filePath, tempFile);

                _logger.LogInfo(job.Name, $"Successfully encrypted: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(job.Name, $"Encryption failed for {filePath}: {ex.Message}");
                throw new Exception($"Encryption failed: {ex.Message}");
            }
            finally
            {
                SafeDelete(tempFile);
            }
        }

        private void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete file {path}: {ex.Message}");
            }
        }

        private async Task SafeFileReplace(string originalPath, string newPath)
        {
            string backupPath = originalPath + ".bak";
            int retryCount = 3;

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    // Create backup
                    if (File.Exists(originalPath))
                        File.Replace(newPath, originalPath, backupPath);
                    else
                        File.Move(newPath, originalPath);

                    // Clean up backup
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    return;
                }
                catch (IOException) when (i < retryCount - 1)
                {
                    await Task.Delay(500 * (i + 1));
                }
            }
            throw new IOException($"Failed to replace file after {retryCount} attempts: {originalPath}");
        }

        private void OnNetworkSpeedChanged(float downloadSpeed, float uploadSpeed)
        {
            // Adjust parallelism based on network conditions
            int maxParallelism = CalculateOptimalParallelism(downloadSpeed);

            // You could adjust _maxParallelFileSizeKB here too
            Debug.WriteLine($"Network speed: {downloadSpeed} Mbps - Setting max parallelism to {maxParallelism}");
        }

        private int CalculateOptimalParallelism(float downloadSpeedMbps)
        {
            if (downloadSpeedMbps > 50) return Environment.ProcessorCount * 2;
            if (downloadSpeedMbps > 20) return Environment.ProcessorCount;
            if (downloadSpeedMbps > 5) return Math.Max(2, Environment.ProcessorCount / 2);
            return 1; // Very slow connection
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
        private readonly SemaphoreSlim _priorityLock = new(1, 1);
        private int _pendingPriorityFiles = 0;

        public void SetPriorityExtensions(IEnumerable<string> extensions)
        {
            lock (_priorityExtensions)
            {
                _priorityExtensions.Clear();
                foreach (var ext in extensions)
                {
                    _priorityExtensions.Add(ext.StartsWith(".") ? ext : $".{ext}");
                }
            }
        }

        public bool IsPriorityFile(string filePath)
        {
            lock (_priorityExtensions)
            {
                return _priorityExtensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
            }
        }

        public async Task WaitForNonPriorityFilesAsync(CancellationToken ct)
        {
            while (_pendingPriorityFiles > 0)
            {
                await Task.Delay(100, ct);
                ct.ThrowIfCancellationRequested();
            }
        }

        public void IncrementPriorityFiles() => Interlocked.Increment(ref _pendingPriorityFiles);
        public void DecrementPriorityFiles() => Interlocked.Decrement(ref _pendingPriorityFiles);
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