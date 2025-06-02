// NetworkMonitor.cs
using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace BackupApp.Services
{
    public class NetworkMonitor : IDisposable
    {
        private readonly Timer _timer;
        private NetworkInterface _primaryInterface;
        private long _lastBytesReceived;
        private long _lastBytesSent;
        private float _currentDownloadSpeed;
        private float _currentUploadSpeed;
        private int _samplingInterval = 1000; // ms

        public event Action<float, float> NetworkSpeedUpdated;

        public NetworkMonitor()
        {
            IdentifyPrimaryInterface();
            _timer = new Timer(UpdateNetworkStats, null, 0, _samplingInterval);
        }

        private void IdentifyPrimaryInterface()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus == OperationalStatus.Up &&
                    nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    _primaryInterface = nic;
                    _lastBytesReceived = nic.GetIPv4Statistics().BytesReceived;
                    _lastBytesSent = nic.GetIPv4Statistics().BytesSent;
                    break;
                }
            }
        }

        private void UpdateNetworkStats(object state)
        {
            if (_primaryInterface == null) return;

            var stats = _primaryInterface.GetIPv4Statistics();
            long bytesReceived = stats.BytesReceived;
            long bytesSent = stats.BytesSent;

            // Calculate speed in Mbps
            _currentDownloadSpeed = (bytesReceived - _lastBytesReceived) * 8 / (float)(_samplingInterval * 125000);
            _currentUploadSpeed = (bytesSent - _lastBytesSent) * 8 / (float)(_samplingInterval * 125000);

            _lastBytesReceived = bytesReceived;
            _lastBytesSent = bytesSent;

            NetworkSpeedUpdated?.Invoke(_currentDownloadSpeed, _currentUploadSpeed);
        }

        public float GetCurrentDownloadSpeed() => _currentDownloadSpeed;
        public float GetCurrentUploadSpeed() => _currentUploadSpeed;

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}