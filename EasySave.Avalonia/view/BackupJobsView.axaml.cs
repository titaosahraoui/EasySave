using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BackupApp.Avalonia.Views;
using BackupApp.ViewModels;


namespace BackupApp.Avalonia.Views
{
    public partial class BackupJobsView : UserControl
    {
        private readonly BackupViewModel _viewModel;

        public BackupJobsView()
        {
            InitializeComponent();
            _viewModel = new BackupViewModel();
            DataContext = _viewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnAddJobClick(object? sender, RoutedEventArgs e)
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow == null) return;

            var result = await AddEditBackupJobWindow.ShowDialog(parentWindow, _viewModel);
            _viewModel.RefreshBackupJobs();
        }
    }
}