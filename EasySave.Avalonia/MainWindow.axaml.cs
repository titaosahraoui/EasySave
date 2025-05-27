using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Threading;
using BackupApp.Avalonia.Views;
using BackupApp.ViewModels;



namespace BackupApp.Avalonia
{
    public partial class MainWindow : Window
    {
        public MainWindow()
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
            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var addJobWindow = new AddEditBackupJobWindow();
                await addJobWindow.ShowDialog(this);
            });
        }
    }
}