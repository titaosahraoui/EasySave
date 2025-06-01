using ReactiveUI;
using System.Collections.ObjectModel;

namespace BackupApp.Models
{
    public class CryptoConfig : ReactiveObject
    {
        private bool _isEnabled;
        private string _encryptionKey = string.Empty;
        private ObservableCollection<string> _fileExtensions = new();

        public bool IsEnabled
        {
            get => _isEnabled;
            set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
        }

        // CryptoSoftPath n'est plus nécessaire car on utilise la référence directe
        // Supprimé : CryptoSoftPath

        public string EncryptionKey
        {
            get => _encryptionKey;
            set => this.RaiseAndSetIfChanged(ref _encryptionKey, value);
        }

        public ObservableCollection<string> FileExtensions
        {
            get => _fileExtensions;
            set => this.RaiseAndSetIfChanged(ref _fileExtensions, value);
        }
    }
}