using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BackupApp.Avalonia.Views;
using BackupApp.Controllers;
using BackupApp.ViewModels;
using BackupApp.Views;

namespace BackupApp.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Create the main window first
                var mainWindow = new MainWindow();


                // Set the controller as DataContext
                mainWindow.DataContext = new MainWindowViewModel(); ;

                // Assign the main window
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}