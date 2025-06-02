using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.IO;
using System.Threading.Tasks;
using BackupApp;
using BackupApp.Logging;
using BackupApp.Models;
using BackupApp.Services;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using BackupApp.Avalonia;

namespace BackupApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppConfig _config;
        private ILogger _logger;
        private string _selectedLanguage;
        private string _newPriorityExtension;
        private string _newEncryptionExtension;
        private bool _isJsonLogFormat = true;
        private bool _isEncryptionEnabled;
        private string _defaultEncryptionKey = string.Empty;
        private string _encryptionTestResult = string.Empty;
        private string _encryptionTestResultColor = "#2c3e50";

        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string>
        {
            "en-US", // English
            "fr-FR", // French
        };

        public ObservableCollection<string> PriorityExtensions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EncryptionExtensions { get; } = new ObservableCollection<string>();

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    // Notify the application that language has changed
                    OnLanguageChanged?.Invoke(this, value);
                }
            }
        }
        public event EventHandler<string> OnLanguageChanged;

        public string NewPriorityExtension
        {
            get => _newPriorityExtension;
            set => SetProperty(ref _newPriorityExtension, value);
        }

        public string NewEncryptionExtension
        {
            get => _newEncryptionExtension;
            set => SetProperty(ref _newEncryptionExtension, value);
        }

        public bool IsJsonLogFormat
        {
            get => _isJsonLogFormat;
            set
            {
                if (SetProperty(ref _isJsonLogFormat, value))
                {
                    OnPropertyChanged(nameof(IsXmlLogFormat));
                }
            }
        }

        public bool IsXmlLogFormat
        {
            get => !_isJsonLogFormat;
            set => IsJsonLogFormat = !value;
        }

        public bool IsEncryptionEnabled
        {
            get => _isEncryptionEnabled;
            set => SetProperty(ref _isEncryptionEnabled, value);
        }

        public string DefaultEncryptionKey
        {
            get => _defaultEncryptionKey;
            set => SetProperty(ref _defaultEncryptionKey, value);
        }
        private string _encryptionKey = string.Empty;
        public string EncryptionKey
        {
            get => _encryptionKey;
            set => SetProperty(ref _encryptionKey, value);
        }

        public string EncryptionTestResult
        {
            get => _encryptionTestResult;
            set => SetProperty(ref _encryptionTestResult, value);
        }

        public string EncryptionTestResultColor
        {
            get => _encryptionTestResultColor;
            set => SetProperty(ref _encryptionTestResultColor, value);
        }


        public ICommand AddPriorityExtensionCommand { get; }
        public ICommand RemovePriorityExtensionCommand { get; }
        public ICommand AddEncryptionExtensionCommand { get; }
        public ICommand RemoveEncryptionExtensionCommand { get; }
        public ICommand TestEncryptionCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            _config = AppConfig.Load();

            // Initialize logger
            _logger = new FileLogger(); // ou XmlFileLogger selon vos préférences

            // Initialize from config
            SelectedLanguage = _config.DefaultLanguage;
            IsJsonLogFormat = _config.DefaultLogFormat == LogFormat.Json;

            // Load extensions from config
            _config.PriorityExtensions.ForEach(ext => PriorityExtensions.Add(ext));

            // Initialize encryption settings from AppConfig
            LoadEncryptionSettings();

            // Commands
            AddPriorityExtensionCommand = new RelayCommand(AddPriorityExtension);
            RemovePriorityExtensionCommand = new RelayCommand<string>(RemovePriorityExtension);
            AddEncryptionExtensionCommand = new RelayCommand(AddEncryptionExtension);
            RemoveEncryptionExtensionCommand = new RelayCommand<string>(RemoveEncryptionExtension);
            TestEncryptionCommand = new AsyncRelayCommand(TestEncryptionAsync);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
        }

        private void LoadEncryptionSettings()
        {
            // Load from existing config
            IsEncryptionEnabled = _config.Encryption.IsEnabled;
            EncryptionKey = _config.Encryption.EncryptionKey; // Fixed: use EncryptionKey instead of DefaultEncryptionKey

            // Load existing extensions
            EncryptionExtensions.Clear();
            foreach (var ext in _config.Encryption.FileExtensions)
            {
                EncryptionExtensions.Add(ext);
            }

            // Add some default extensions if none exist
            if (EncryptionExtensions.Count == 0)
            {
                var defaultExtensions = new[] { ".docx", ".pdf", ".xlsx", ".txt", ".json" };
                foreach (var ext in defaultExtensions)
                {
                    EncryptionExtensions.Add(ext);
                }
            }
        }

        private void AddPriorityExtension()
        {
            if (!string.IsNullOrWhiteSpace(NewPriorityExtension))
            {
                var ext = NewPriorityExtension.ToLower().Trim();
                if (!ext.StartsWith(".")) ext = "." + ext;

                if (!PriorityExtensions.Contains(ext))
                {
                    PriorityExtensions.Add(ext);
                    NewPriorityExtension = string.Empty;
                }
            }
        }

        private void RemovePriorityExtension(string extension)
        {
            PriorityExtensions.Remove(extension);
        }

        private void AddEncryptionExtension()
        {
            if (!string.IsNullOrWhiteSpace(NewEncryptionExtension))
            {
                var ext = NewEncryptionExtension.ToLower().Trim();
                if (!ext.StartsWith(".")) ext = "." + ext;

                if (!EncryptionExtensions.Contains(ext))
                {
                    EncryptionExtensions.Add(ext);
                    NewEncryptionExtension = string.Empty;
                }
            }
        }

        private void RemoveEncryptionExtension(string extension)
        {
            EncryptionExtensions.Remove(extension);
        }

        private async Task TestEncryptionAsync()
        {
            ICryptoService cryptoService = null;
            try
            {
                EncryptionTestResult = "Testing encryption configuration...";
                EncryptionTestResultColor = "#f39c12";

                // First check: Encryption enabled
                if (!IsEncryptionEnabled)
                {
                    EncryptionTestResult = "⚠️ Encryption is not enabled";
                    EncryptionTestResultColor = "#f39c12";
                    return;
                }

                // Second check: Default encryption key
                if (string.IsNullOrEmpty(DefaultEncryptionKey))
                {
                    EncryptionTestResult = "❌ Default encryption key is not specified";
                    EncryptionTestResultColor = "#e74c3c";
                    return;
                }

                // Third check: File extensions
                if (EncryptionExtensions.Count == 0)
                {
                    EncryptionTestResult = "⚠️ No file extensions specified for encryption";
                    EncryptionTestResultColor = "#f39c12";
                    return;
                }

                // Fourth check: Create a CryptoService with current UI settings
                var currentCryptoConfig = new CryptoConfig
                {
                    IsEnabled = IsEncryptionEnabled,
                    EncryptionKey = DefaultEncryptionKey, // Fixed: use EncryptionKey instead of DefaultEncryptionKey
                    FileExtensions = new ObservableCollection<string>(EncryptionExtensions) // Fixed: convert to ObservableCollection
                };

                cryptoService = new CryptoService(_logger, currentCryptoConfig);

                // Test encryption with current settings
                var testResult = await cryptoService.TestEncryptionAsync(DefaultEncryptionKey);

                if (testResult.Success)
                {
                    EncryptionTestResult = $"✅ {testResult.Message}\n📁 Extensions: {string.Join(", ", EncryptionExtensions)}";
                    EncryptionTestResultColor = "#27ae60";
                }
                else
                {
                    EncryptionTestResult = $"❌ {testResult.Message}";
                    EncryptionTestResultColor = "#e74c3c";

                    // Log additional diagnostic information
                    _logger.LogError("EncryptionTest", $"Test failed: {testResult.Message}");
                    _logger.LogInfo("EncryptionTest", $"Key length: {DefaultEncryptionKey?.Length ?? 0}");
                    _logger.LogInfo("EncryptionTest", $"Extensions count: {EncryptionExtensions.Count}");

                    // Check if CryptoSoft.exe exists
                    var cryptoSoftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CryptoSoft.exe");
                    _logger.LogInfo("EncryptionTest", $"CryptoSoft.exe exists: {File.Exists(cryptoSoftPath)}");
                    _logger.LogInfo("EncryptionTest", $"CryptoSoft.exe path: {cryptoSoftPath}");
                }
            }
            catch (Exception ex)
            {
                EncryptionTestResult = $"❌ Test failed with exception: {ex.Message}";
                EncryptionTestResultColor = "#e74c3c";
                _logger.LogError("EncryptionTest", $"Exception during test: {ex.Message}");
                _logger.LogError("EncryptionTest", $"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Dispose the temporary crypto service
                if (cryptoService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private void SaveSettings()
        {
            try
            {
                // Update config with current settings
                _config.DefaultLanguage = SelectedLanguage;
                _config.DefaultLogFormat = IsJsonLogFormat ? LogFormat.Json : LogFormat.Xml;

                var languageService = new LanguageService();
                languageService.SetLanguage(SelectedLanguage);

                // Update extensions lists
                _config.PriorityExtensions.Clear();
                _config.PriorityExtensions.AddRange(PriorityExtensions);

                // Update crypto settings in AppConfig
                _config.Encryption.IsEnabled = IsEncryptionEnabled;
                _config.Encryption.EncryptionKey = EncryptionKey; // Fixed: use EncryptionKey instead of DefaultEncryptionKey
                _config.Encryption.FileExtensions.Clear();
                foreach (var ext in EncryptionExtensions) // Fixed: iterate through ObservableCollection
                {
                    _config.Encryption.FileExtensions.Add(ext);
                }

                // Save configuration
                _config.Save();

                ShowAlert("Settings saved successfully");

                // Clear test result after successful save
                EncryptionTestResult = string.Empty;
            }
            catch (Exception ex)
            {
                ShowError("Failed to save settings", ex);
            }
        }

        // Missing methods that need to be implemented
        protected virtual void ShowError(string message, Exception ex = null)
        {
            // You can implement this to show error messages to the user
            // For example, using a message box or notification system
            var errorMessage = ex != null ? $"{message}: {ex.Message}" : message;
            System.Diagnostics.Debug.WriteLine($"Error: {errorMessage}");

            // Update the test result to show the error
            EncryptionTestResult = $"❌ {errorMessage}";
            EncryptionTestResultColor = "#e74c3c";
        }

        protected virtual void ShowAlert(string message)
        {
            // You can implement this to show alert messages to the user
            System.Diagnostics.Debug.WriteLine($"Alert: {message}");

            // For now, we can show it in the test result temporarily
            EncryptionTestResult = $"✅ {message}";
            EncryptionTestResultColor = "#27ae60";

            // Clear after a delay (you might want to implement this differently)
            Task.Delay(3000).ContinueWith(_ => {
                EncryptionTestResult = string.Empty;
            });
        }
    }
}