using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using BackupApp.Avalonia.Views;
using BackupApp.ViewModels;

namespace BackupApp.Avalonia
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();  // Must come first
            DataContext = new MainWindowViewModel();

            // Initialize main content
            //SetupNavigation();

            MainContent = this.FindControl<ContentControl>("MainContent");
            var backupJobsBtn = this.FindControl<Button>("BackupJobsButton");
            var settingsBtn = this.FindControl<Button>("SettingsButton");

            backupJobsBtn.Click += (s, e) => MainContent.Content = new BackupJobsView();
            settingsBtn.Click += (s, e) => MainContent.Content = new SettingsView();


        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            // These names must match x:Name in XAML
            MainContent = this.FindControl<ContentControl>("MainContent");
            BackupJobsButton = this.FindControl<Button>("BackupJobsButton");
            SettingsButton = this.FindControl<Button>("SettingsButton");
        }

        //private void SetupNavigation()
        //{
        //    // Find buttons using their classes instead of names
        //    var dashboardBtn = this.Find<Button>("DashboardButton");
        //    var backupJobsBtn = this.Find<Button>("BackupJobsButton");
        //    var settingsBtn = this.Find<Button>("SettingsButton");
        //    var logsBtn = this.Find<Button>("LogsButton");

        //    //dashboardBtn.Click += (s, e) => MainContent.Content = new DashboardView();
        //    backupJobsBtn.Click += (s, e) => MainContent.Content = new BackupJobsView();
        //    settingsBtn.Click += (s, e) => MainContent.Content = new SettingsView();
        //    //logsBtn.Click += (s, e) => MainContent.Content = new LogsView();
        //}

        //private async void OnAddJobClick(object? sender, RoutedEventArgs e)
        //{
        //    var addJobWindow = new AddEditBackupJobWindow();
        //    await addJobWindow.ShowDialog(this);
        //}
    }
}