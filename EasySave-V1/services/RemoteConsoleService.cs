// RemoteConsoleService.cs
using BackupApp.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BackupApp.Services
{
    public class RemoteConsoleService : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly IStateManager _stateManager;
        private readonly ILogger _logger;
        private readonly int _port;

        public RemoteConsoleService(IStateManager stateManager, ILogger logger, int port = 4242)
        {
            _stateManager = stateManager;
            _logger = logger;
            _port = port;
            _listener = new TcpListener(IPAddress.Any, port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            _logger.LogInfo("RemoteConsole", $"Remote console service started on port {_port}");

            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    _clients.Add(client);
                    _ = HandleClientAsync(client, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("RemoteConsole", $"Service error: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        // Send current state to client
                        var states = _stateManager.GetAllStates();
                        var json = JsonSerializer.Serialize(states);
                        var data = Encoding.UTF8.GetBytes(json + "\n");
                        await stream.WriteAsync(data, 0, data.Length, ct);

                        // Wait before next update
                        await Task.Delay(1000, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("RemoteConsole", $"Client handling error: {ex.Message}");
            }
            finally
            {
                _clients.Remove(client);
            }
        }

        public void BroadcastCommand(string command)
        {
            var data = Encoding.UTF8.GetBytes($"COMMAND:{command}\n");
            foreach (var client in _clients.ToArray())
            {
                if (client.Connected)
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch { /* ignore disconnected clients */ }
                }
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            foreach (var client in _clients)
            {
                client.Dispose();
            }
            _listener.Stop();
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }
    }
}