using BackupApp.Logging;
using System.Text.Json;
using BackupApp.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;

public class AppConfig
{
    public string DefaultLanguage { get; set; } = "en-US";
    public LogFormat DefaultLogFormat { get; set; } = LogFormat.Json;

    // Deprecated - Use Encryption.FileExtensions instead
    [Obsolete("Use Encryption.FileExtensions instead")]
    public List<string> EncryptedExtensions { get; set; } = new List<string>();

    public List<string> PriorityExtensions { get; set; } = new List<string>();
    public CryptoSettings Encryption { get; set; } = new CryptoSettings();

    private static string ConfigDirectory => @"C:\ProgramData\EasySave";
    private static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig Load()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(ConfigDirectory);

            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // Migration: Move old EncryptedExtensions to new location
                if (config.EncryptedExtensions.Count > 0 && config.Encryption.FileExtensions.Count == 0)
                {
                    config.Encryption.FileExtensions = config.EncryptedExtensions;
                    config.EncryptedExtensions.Clear();
                    config.Save(); // Save migrated config
                }

                return config;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load configuration: {ex.Message}");
        }

        return CreateDefaultConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, options));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save configuration: {ex.Message}");
            throw;
        }
    }

    public CryptoConfig GetCryptoConfig()
    {
        return new CryptoConfig
        {
            IsEnabled = Encryption.IsEnabled,
            EncryptionKey = Encryption.EncryptionKey,
            FileExtensions = new ObservableCollection<string>(Encryption.FileExtensions)
        };
    }

    public void UpdateFromCryptoConfig(CryptoConfig cryptoConfig)
    {
        if (cryptoConfig == null) return;

        Encryption.IsEnabled = cryptoConfig.IsEnabled;
        Encryption.EncryptionKey = cryptoConfig.EncryptionKey;
        Encryption.FileExtensions = cryptoConfig.FileExtensions?.ToList() ?? new List<string>();
    }

    private static AppConfig CreateDefaultConfig()
    {
        var config = new AppConfig
        {
            PriorityExtensions = new List<string> { ".pdf", ".docx" },
            Encryption = new CryptoSettings
            {
                IsEnabled = true, // Enable by default
                FileExtensions = new List<string> { ".txt", ".docx" },
                CryptoSoftPath = FindCryptoSoftPath(),
                EncryptionKey = "group2" // Set default key
            }
        };

        // Save the default config on first run
        config.Save();
        return config;
    }

    private static string FindCryptoSoftPath()
    {
        var paths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CryptoSoft", "CryptoSoft.exe")
        };

        return paths.FirstOrDefault(File.Exists) ?? string.Empty;
    }
}

public class CryptoSettings
{
    public bool IsEnabled { get; set; } = false;
    public string CryptoSoftPath { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty;
    public List<string> FileExtensions { get; set; } = new List<string>();
}

public static class TempFileManager
{
    public static readonly string CryptoSoftTempPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "EasySave",
        "CryptoSoftTemp");

    static TempFileManager()
    {
        // Ensure directory exists and has proper permissions
        Directory.CreateDirectory(CryptoSoftTempPath);
        File.SetAttributes(CryptoSoftTempPath, FileAttributes.Normal);
    }

    public static string GetTempFilePath(string extension = ".tmp")
    {
        return Path.Combine(CryptoSoftTempPath, $"{Guid.NewGuid()}{extension}");
    }

    public static void CleanTempFiles()
    {
        try
        {
            foreach (var file in Directory.GetFiles(CryptoSoftTempPath))
            {
                try { File.Delete(file); }
                catch { /* Ignore deletion errors */ }
            }
        }
        catch { /* Ignore directory access errors */ }
    }
}