// MainWindow.xaml.cs
using BackupApp.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace RemoteBackupMonitor
{
    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly DispatcherTimer _timer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            ConnectToServer();

            _worker.DoWork += Worker_DoWork;
            _worker.RunWorkerCompleted += Worker_RunWorkerCompleted;

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void ConnectToServer()
        {
            try
            {
                _client = new TcpClient("localhost", 4242);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Connection failed: {ex.Message}");
                Close();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (!_worker.IsBusy)
            {
                _worker.RunWorkerAsync();
            }
        }

        private void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                var stream = _client.GetStream();
                var buffer = new byte[1024];
                var message = new StringBuilder();

                while (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    message.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));
                }

                if (message.Length > 0)
                {
                    e.Result = message.ToString();
                }
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        private void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
                return;
            }

            if (e.Result is string json)
            {
                try
                {
                    var states = JsonSerializer.Deserialize<List<BackupState>>(json);
                    BackupStatesGrid.ItemsSource = states;
                }
                catch { /* ignore parse errors */ }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _client?.Dispose();
            base.OnClosed(e);
        }
    }
}