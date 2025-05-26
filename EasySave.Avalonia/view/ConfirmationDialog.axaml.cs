using Avalonia.Controls;
using Avalonia.Interactivity;

namespace BackupApp.Views
{
    public partial class ConfirmationDialog : Window
    {
        public ConfirmationDialog()
        {
            InitializeComponent();
        }

        public ConfirmationDialog(string title, string message)
        {
            InitializeComponent();
            Title = title;
            this.FindControl<TextBlock>("MessageText").Text = message;
        }

        private void YesClick(object sender, RoutedEventArgs e)
        {
            Close(true);
        }

        private void NoClick(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}