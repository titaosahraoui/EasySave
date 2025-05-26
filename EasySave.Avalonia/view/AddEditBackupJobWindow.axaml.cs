using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BackupApp.Models;
using BackupApp.ViewModels;

namespace BackupApp.Avalonia.Views
{
    public partial class AddEditBackupJobWindow : Window
    {
        public AddEditBackupJobWindow()
        {
            InitializeComponent();
            DataContext = new AddEditBackupJobViewModel();
        }

        public AddEditBackupJobWindow(BackupJob existingJob)
        {
            InitializeComponent();
            DataContext = new AddEditBackupJobViewModel(existingJob);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}