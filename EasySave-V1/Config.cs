using BackupApp.Logging;
using System.Text.Json;
using BackupApp.Models;

public class AppConfig
{
    public string DefaultLanguage { get; set; } = "en-US";
    public LogFormat DefaultLogFormat { get; set; } = LogFormat.Json;
    public List<string> PriorityExtensions { get; set; } = new List<string>();
    public List<string> EncryptedExtensions { get; set; } = new List<string>();

    // Encryption settings
    public CryptoSettings Encryption { get; set; } = new CryptoSettings();

    private static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.config");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);

                // Ensure encryption settings are initialized
                config.Encryption ??= new CryptoSettings();

                return config;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load configuration: {ex.Message}");
        }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    public CryptoConfig GetCryptoConfig()
    {
        return new CryptoConfig
        {
            IsEnabled = Encryption.IsEnabled,
            EncryptionKey = Encryption.EncryptionKey, // Use single consistent property
            FileExtensions = new System.Collections.ObjectModel.ObservableCollection<string>(Encryption.FileExtensions)
        };
    }

    public void UpdateFromCryptoConfig(CryptoConfig cryptoConfig)
    {
        Encryption.IsEnabled = cryptoConfig.IsEnabled;
        Encryption.EncryptionKey = cryptoConfig.EncryptionKey;
        Encryption.FileExtensions = new List<string>(cryptoConfig.FileExtensions);
    }
}

public class CryptoSettings
{
    public bool IsEnabled { get; set; } = false;
    public string CryptoSoftPath { get; set; } = string.Empty;
    public string EncryptionKey { get; set; } = string.Empty; // Keep only this
    public List<string> FileExtensions { get; set; } = new List<string>();
}
