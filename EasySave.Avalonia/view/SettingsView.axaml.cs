using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BackupApp.ViewModels;


namespace BackupApp.Avalonia.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = new SettingsViewModel();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}