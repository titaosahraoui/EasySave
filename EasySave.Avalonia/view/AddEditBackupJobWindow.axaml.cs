using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BackupApp.Models;
using BackupApp.ViewModels;
using System.Threading.Tasks;
using Avalonia;

namespace BackupApp.Avalonia.Views
{
    public partial class AddEditBackupJobWindow : Window
    {
        public AddEditBackupJobWindow()
        {
            InitializeComponent();
            DataContext = new AddEditBackupJobViewModel(this);
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public static async Task<BackupJob> Show(Window owner, BackupJob existingJob = null)
        {
            var window = new AddEditBackupJobWindow();
            window.DataContext = new AddEditBackupJobViewModel(window)
            {
                CurrentJob = existingJob ?? new BackupJob()
            };

            await window.ShowDialog(owner);
            return window.DataContext is AddEditBackupJobViewModel vm ? vm.CurrentJob : null;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}