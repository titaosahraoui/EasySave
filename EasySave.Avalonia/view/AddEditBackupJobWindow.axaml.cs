using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BackupApp.Models;
using BackupApp.ViewModels;
using System;
using System.Threading.Tasks;

namespace BackupApp.Avalonia.Views
{
    public partial class AddEditBackupJobWindow : Window
    {
        public AddEditBackupJobWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public static async Task<BackupJob?> ShowDialog(Window parent, BackupViewModel backupViewModel, BackupJob? existingJob = null)
        {
            var window = new AddEditBackupJobWindow();
            var viewModel = new AddEditBackupJobViewModel(window, backupViewModel, existingJob);
            window.DataContext = viewModel;

            // Setup event handlers
            viewModel.AlertRequested += (sender, message) =>
                ShowMessageBox(window, "Alert", message);
            viewModel.ErrorOccurred += (sender, ex) =>
                ShowMessageBox(window, "Error", ex.Message);

            return await window.ShowDialog<BackupJob?>(parent);
        }

        private static void ShowMessageBox(Window owner, string title, string message)
        {
            // Simple message box implementation
            var dialog = new Window
            {
                Title = title,
                Content = new TextBlock { Text = message, Margin = new Thickness(20) },
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog(owner);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}