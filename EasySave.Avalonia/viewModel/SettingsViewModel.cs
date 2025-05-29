using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using BackupApp;
using BackupApp.Logging;

namespace BackupApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppConfig _config;
        private string _selectedLanguage;
        private string _newPriorityExtension;
        private string _newEncryptedExtension;
        private bool _isJsonLogFormat = true;

        public ObservableCollection<string> AvailableLanguages { get; } = new ObservableCollection<string>
        {
            "en-US", // English
            "fr-FR", // French
        };

        public ObservableCollection<string> PriorityExtensions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> EncryptedExtensions { get; } = new ObservableCollection<string>();

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value);
        }

        public string NewPriorityExtension
        {
            get => _newPriorityExtension;
            set => SetProperty(ref _newPriorityExtension, value);
        }

        public string NewEncryptedExtension
        {
            get => _newEncryptedExtension;
            set => SetProperty(ref _newEncryptedExtension, value);
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

        public ICommand AddPriorityExtensionCommand { get; }
        public ICommand RemovePriorityExtensionCommand { get; }
        public ICommand AddEncryptedExtensionCommand { get; }
        public ICommand RemoveEncryptedExtensionCommand { get; }
        public ICommand SaveSettingsCommand { get; }

        public SettingsViewModel()
        {
            _config = AppConfig.Load();

            // Initialize from config
            SelectedLanguage = _config.DefaultLanguage;
            IsJsonLogFormat = _config.DefaultLogFormat == LogFormat.Json;

            // Load extensions from config
            _config.PriorityExtensions.ForEach(ext => PriorityExtensions.Add(ext));
            _config.EncryptedExtensions.ForEach(ext => EncryptedExtensions.Add(ext));

            AddPriorityExtensionCommand = new RelayCommand(AddPriorityExtension);
            RemovePriorityExtensionCommand = new RelayCommand<string>(RemovePriorityExtension);
            AddEncryptedExtensionCommand = new RelayCommand(AddEncryptedExtension);
            RemoveEncryptedExtensionCommand = new RelayCommand<string>(RemoveEncryptedExtension);
            SaveSettingsCommand = new RelayCommand(SaveSettings);
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

        private void AddEncryptedExtension()
        {
            if (!string.IsNullOrWhiteSpace(NewEncryptedExtension))
            {
                var ext = NewEncryptedExtension.ToLower().Trim();
                if (!ext.StartsWith(".")) ext = "." + ext;

                if (!EncryptedExtensions.Contains(ext))
                {
                    EncryptedExtensions.Add(ext);
                    NewEncryptedExtension = string.Empty;
                }
            }
        }

        private void RemoveEncryptedExtension(string extension)
        {
            EncryptedExtensions.Remove(extension);
        }

        private void SaveSettings()
        {
            try
            {
                // Update config with current settings
                _config.DefaultLanguage = SelectedLanguage;
                _config.DefaultLogFormat = IsJsonLogFormat ? LogFormat.Json : LogFormat.Xml;

                // Update extensions lists
                _config.PriorityExtensions.Clear();
                _config.PriorityExtensions.AddRange(PriorityExtensions);

                _config.EncryptedExtensions.Clear();
                _config.EncryptedExtensions.AddRange(EncryptedExtensions);

                _config.Save();
                ShowAlert("Settings saved successfully");
            }
            catch (Exception ex)
            {
                ShowError("Failed to save settings", ex);
            }
        }
    }
}