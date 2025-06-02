using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BackupApp.Models;
using BackupApp.Logging;

namespace BackupApp.Services
{
    public interface ICryptoService
    {
        Task<bool> EncryptFileAsync(string filePath, string key);
        bool ShouldEncryptFile(string filePath, CryptoConfig config);
        int GetQueueCount();
        Task<(bool Success, string Message)> TestEncryptionAsync(string testKey);
    }

    public class CryptoService : ICryptoService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly CryptoConfig _config;
        private readonly string _cryptoSoftPath;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<EncryptionRequest> _encryptionQueue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _processingTask;
        private volatile bool _isDisposed = false;

        public CryptoService(ILogger logger, CryptoConfig config)
        {
            _logger = logger;
            _config = config;

            // Chemin vers l'exécutable CryptoSoft
            _cryptoSoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");

            // Vérifier si l'exécutable existe
            if (!File.Exists(_cryptoSoftPath))
            {
                _logger.LogError("CryptoSoft", $"CryptoSoft.exe introuvable à {_cryptoSoftPath}");
            }
            else
            {
                _logger.LogInfo("CryptoSoft", $"CryptoSoft.exe trouvé à {_cryptoSoftPath}");
            }

            // Initialiser le sémaphore pour autoriser une seule instance à la fois
            _semaphore = new SemaphoreSlim(1, 1);
            _encryptionQueue = new ConcurrentQueue<EncryptionRequest>();
            _cancellationTokenSource = new CancellationTokenSource();

            // Démarrer la tâche de traitement de la file d'attente
            _processingTask = Task.Run(ProcessQueueAsync, _cancellationTokenSource.Token);
        }

        public async Task<bool> EncryptFileAsync(string filePath, string key)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(CryptoService));
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    _logger.LogError("CryptoSoft", $"File not found: {filePath}");
                    return false;
                }

                if (string.IsNullOrEmpty(key))
                {
                    _logger.LogError("CryptoSoft", "Encryption key is empty");
                    return false;
                }

                // Créer une copie temporaire du fichier pour le chiffrement
                string tempFilePath = filePath + ".temp_for_encryption";
                File.Copy(filePath, tempFilePath, true);

                var request = new EncryptionRequest
                {
                    SourcePath = filePath,
                    TempFilePath = tempFilePath,
                    Key = key,
                    CompletionSource = new TaskCompletionSource<long>()
                };

                _encryptionQueue.Enqueue(request);
                _logger.LogInfo("CryptoSoft", $"Demande de chiffrement ajoutée à la file d'attente: {filePath}");

                long result = await request.CompletionSource.Task;

                if (result == -1)
                {
                    _logger.LogError("CryptoSoft", $"Encryption failed for {filePath}");
                    return false;
                }

                _logger.LogInfo("CryptoSoft", $"File encrypted: {filePath} (Time: {result}ms)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("CryptoSoft", $"Exception during encryption of {filePath}: {ex.Message}");
                return false;
            }
        }

        public bool ShouldEncryptFile(string filePath, CryptoConfig config)
        {
            if (!config.IsEnabled || config.FileExtensions.Count == 0)
                return false;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return config.FileExtensions.Any(ext =>
                ext.ToLowerInvariant() == extension ||
                ext.ToLowerInvariant() == extension.TrimStart('.'));
        }

        public int GetQueueCount()
        {
            return _encryptionQueue.Count;
        }

        /// <summary>
        /// Test the encryption configuration with detailed feedback
        /// </summary>
        public async Task<(bool Success, string Message)> TestEncryptionAsync(string testKey)
        {
            try
            {
                // First, check if CryptoSoft.exe exists
                if (!File.Exists(_cryptoSoftPath))
                {
                    return (false, $"CryptoSoft.exe not found at: {_cryptoSoftPath}");
                }

                // Validate the key
                if (string.IsNullOrEmpty(testKey))
                {
                    return (false, "Encryption key cannot be empty");
                }

                // Create a temporary test file
                var tempDir = Path.GetTempPath();
                var testFile = Path.Combine(tempDir, $"backup_test_{Guid.NewGuid():N}.txt");
                var testContent = "This is a test file for encryption validation.";

                _logger.LogInfo("CryptoSoft", $"Fichier de test créé: {testFile}");

                try
                {
                    await File.WriteAllTextAsync(testFile, testContent);

                    // Read original content for comparison
                    var originalBytes = await File.ReadAllBytesAsync(testFile);

                    // Test the encryption process
                    long result = await ExecuteEncryption(testFile, testKey);

                    if (result == -1)
                    {
                        return (false, "Encryption process failed. Check CryptoSoft configuration and key validity.");
                    }

                    // Check if file was modified (encrypted)
                    if (!File.Exists(testFile))
                    {
                        return (false, "Test file disappeared during encryption process");
                    }

                    // Read the file after encryption
                    var encryptedBytes = await File.ReadAllBytesAsync(testFile);

                    if (originalBytes.SequenceEqual(encryptedBytes))
                    {
                        return (false, "File was not properly encrypted (content unchanged)");
                    }

                    // Test decryption (XOR with same key should restore original)
                    long decryptResult = await ExecuteEncryption(testFile, testKey);

                    if (decryptResult == -1)
                    {
                        return (false, "Decryption test failed");
                    }

                    // Read decrypted content
                    var decryptedBytes = await File.ReadAllBytesAsync(testFile);

                    if (!originalBytes.SequenceEqual(decryptedBytes))
                    {
                        return (false, "Decryption test failed - content not restored to original");
                    }

                    return (true, $"Encryption test successful! Encrypt: {result}ms, Decrypt: {decryptResult}ms");
                }
                finally
                {
                    // Clean up test file
                    if (File.Exists(testFile))
                    {
                        try { File.Delete(testFile); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("CryptoSoft", $"Test encryption failed: {ex.Message}");
                return (false, $"Test failed with exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Traite la file d'attente des demandes de chiffrement de manière séquentielle
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_encryptionQueue.TryDequeue(out EncryptionRequest request))
                    {
                        _logger.LogInfo("CryptoSoft", $"Traitement de la demande: {request.SourcePath}");

                        // Acquérir le sémaphore pour s'assurer qu'une seule instance de CryptoSoft s'exécute
                        await _semaphore.WaitAsync(_cancellationTokenSource.Token);

                        try
                        {
                            long result = await ExecuteEncryption(request.TempFilePath, request.Key);

                            if (result != -1)
                            {
                                // Replace the original file with the encrypted temp file
                                File.Replace(request.TempFilePath, request.SourcePath, null);
                                _logger.LogInfo("CryptoSoft", $"Fichier chiffré: {request.SourcePath}");
                            }
                            else
                            {
                                // Nettoyer le fichier temporaire en cas d'échec
                                if (File.Exists(request.TempFilePath))
                                {
                                    try { File.Delete(request.TempFilePath); } catch { }
                                }
                            }

                            request.CompletionSource.SetResult(result);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("CryptoSoft", $"Erreur lors du chiffrement: {ex.Message}");

                            // Nettoyer le fichier temporaire en cas d'exception
                            if (File.Exists(request.TempFilePath))
                            {
                                try { File.Delete(request.TempFilePath); } catch { }
                            }

                            request.CompletionSource.SetResult(-1);
                        }
                        finally
                        {
                            _semaphore.Release();
                        }
                    }
                    else
                    {
                        // Attendre un peu avant de vérifier à nouveau la file d'attente
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Arrêt normal demandé
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError("CryptoSoft", $"Erreur dans le processus de traitement de la file d'attente: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Exécute réellement le chiffrement avec CryptoSoft
        /// </summary>
        private async Task<long> ExecuteEncryption(string filePath, string key)
        {
            try
            {
                _logger.LogInfo("CryptoSoft", $"Exécution du chiffrement: {filePath}");

                // Vérifier si l'exécutable existe
                if (!File.Exists(_cryptoSoftPath))
                {
                    _logger.LogError("CryptoSoft", $"CryptoSoft.exe introuvable à {_cryptoSoftPath}");
                    return -1;
                }

                // Vérifier si le fichier source existe
                if (!File.Exists(filePath))
                {
                    _logger.LogError("CryptoSoft", $"Fichier source introuvable: {filePath}");
                    return -1;
                }

                // Vérifier si CryptoSoft est déjà en cours d'exécution
                if (IsCryptoSoftRunning())
                {
                    _logger.LogInfo("CryptoSoft", "CryptoSoft est déjà en cours d'exécution. Attente...");
                    await WaitForCryptoSoftToFinish();
                }

                // Créer le processus pour exécuter CryptoSoft
                using (Process process = new Process())
                {
                    // CryptoSoft attend seulement 2 arguments : fichier et clé
                    process.StartInfo.FileName = _cryptoSoftPath;
                    process.StartInfo.Arguments = $"\"{filePath}\" \"{key}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;

                    _logger.LogInfo("CryptoSoft", $"Commande: {_cryptoSoftPath} \"{filePath}\" \"[KEY_HIDDEN]\"");

                    // Démarrer le processus et mesurer le temps
                    var stopwatch = Stopwatch.StartNew();

                    if (!process.Start())
                    {
                        _logger.LogError("CryptoSoft", "Failed to start CryptoSoft process");
                        return -1;
                    }

                    // Lire la sortie
                    string output = await process.StandardOutput.ReadToEndAsync();
                    string error = await process.StandardError.ReadToEndAsync();

                    // Attendre la fin du processus avec timeout
                    bool finished = await ProcessExtensions.WaitForExitAsync(process, TimeSpan.FromMinutes(5));
                    stopwatch.Stop();

                    if (!finished)
                    {
                        _logger.LogError("CryptoSoft", "CryptoSoft process timed out after 5 minutes");
                        try { process.Kill(); } catch { }
                        return -1;
                    }

                    _logger.LogInfo("CryptoSoft", $"Chiffrement terminé en {stopwatch.ElapsedMilliseconds}ms, code de sortie: {process.ExitCode}");

                    if (!string.IsNullOrEmpty(output))
                    {
                        _logger.LogInfo("CryptoSoft", $"Sortie standard: {output}");
                    }

                    if (!string.IsNullOrEmpty(error))
                    {
                        _logger.LogError("CryptoSoft", $"Erreur standard: {error}");
                    }

                    // Vérifier le code de sortie
                    // CryptoSoft retourne le temps d'exécution comme code de sortie, ou -99 en cas d'erreur
                    if (process.ExitCode == -99)
                    {
                        _logger.LogError("CryptoSoft", $"CryptoSoft a retourné une erreur (code {process.ExitCode})");
                        return -1;
                    }

                    // Le code de sortie contient le temps d'exécution selon votre implémentation
                    return process.ExitCode >= 0 ? process.ExitCode : stopwatch.ElapsedMilliseconds;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("CryptoSoft", $"Exception lors du chiffrement: {ex.Message}");
                _logger.LogError("CryptoSoft", $"Stack trace: {ex.StackTrace}");
                return -1;
            }
        }

        /// <summary>
        /// Vérifie si CryptoSoft est déjà en cours d'exécution
        /// </summary>
        private bool IsCryptoSoftRunning()
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("CryptoSoft");
                return processes.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("CryptoSoft", $"Erreur lors de la vérification des processus: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Attend que tous les processus CryptoSoft se terminent
        /// </summary>
        private async Task WaitForCryptoSoftToFinish()
        {
            int maxWaitTime = 30000; // 30 secondes maximum
            int waitInterval = 500;   // Vérifier toutes les 500ms
            int totalWaitTime = 0;

            while (totalWaitTime < maxWaitTime)
            {
                if (!IsCryptoSoftRunning())
                {
                    _logger.LogInfo("CryptoSoft", "CryptoSoft n'est plus en cours d'exécution.");
                    return;
                }

                await Task.Delay(waitInterval);
                totalWaitTime += waitInterval;
            }

            _logger.LogWarning("CryptoSoft", "Timeout: CryptoSoft semble toujours en cours d'exécution après 30 secondes.");
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;

                // Annuler le token pour arrêter le traitement
                _cancellationTokenSource?.Cancel();

                // Attendre que la tâche de traitement se termine
                try
                {
                    _processingTask?.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException)
                {
                    // Ignorer les exceptions d'annulation
                }

                // Nettoyer les ressources
                _semaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
                _processingTask?.Dispose();
            }
        }

        /// <summary>
        /// Classe pour représenter une demande de chiffrement
        /// </summary>
        private class EncryptionRequest
        {
            public string SourcePath { get; set; }
            public string TempFilePath { get; set; }
            public string Key { get; set; }
            public TaskCompletionSource<long> CompletionSource { get; set; }
        }
    }

    /// <summary>
    /// Extension methods for Process class - must be in a static non-generic class
    /// </summary>
    public static class ProcessExtensions
    {
        /// <summary>
        /// Extension to WaitForExitAsync with timeout support for older .NET versions
        /// </summary>
        public static async Task<bool> WaitForExitAsync(this Process process, TimeSpan timeout)
        {
            using (var cts = new CancellationTokenSource(timeout))
            {
                try
                {
                    await Task.Run(() => process.WaitForExit(), cts.Token);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }
    }
}