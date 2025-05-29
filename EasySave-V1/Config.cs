using BackupApp.Logging;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BackupApp
{
    public class AppConfig
    {
        public string DefaultLanguage { get; set; } = "en-US";
        public LogFormat DefaultLogFormat { get; set; } = LogFormat.Json;
        public List<string> PriorityExtensions { get; set; } = new List<string>();
        public List<string> EncryptedExtensions { get; set; } = new List<string>();

        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "EasySave",
            "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
            }
            catch { }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}