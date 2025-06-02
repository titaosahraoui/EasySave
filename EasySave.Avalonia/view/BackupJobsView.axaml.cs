using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BackupApp.ViewModels;
using BackupApp.Views;

namespace BackupApp.Avalonia.Views
{
    public partial class BackupJobsView : UserControl
    {
        public BackupJobsView()
        {
            InitializeComponent();
            DataContext = new BackupViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnAddJobClick(object? sender, RoutedEventArgs e)
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;

            if (DataContext is not BackupViewModel viewModel) return;

            var result = await AddEditBackupJobWindow.ShowDialog(parentWindow, viewModel);

            // Always refresh after the dialog closes, regardless of result
            viewModel.RefreshBackupJobs();
        }
    }
}