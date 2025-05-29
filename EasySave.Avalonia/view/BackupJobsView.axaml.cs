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

            // Use the static ShowDialog method we created
            var result = await AddEditBackupJobWindow.ShowDialog(parentWindow);

            // If a job was created/edited, refresh the list
            if (result != null && DataContext is BackupViewModel viewModel)
            {
                viewModel.RefreshBackupJobs();
            }
        }
    }
}