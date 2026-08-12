using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PCHealthDashboard.Services;

public class NetworkMonitorService
{
    private readonly Ping _pingSender;
    private const string TargetHost = "8.8.8.8";

    public NetworkMonitorService()
    {
        _pingSender = new Ping();
    }

    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastTime;
    private NetworkInterface[]? _cachedInterfaces;
    private DateTime _lastInterfaceCheck;

    private NetworkInterface[] GetActiveInterfaces()
    {
        if (_cachedInterfaces == null || (DateTime.UtcNow - _lastInterfaceCheck).TotalSeconds > 10)
        {
            try
            {
                _cachedInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(i => i.OperationalStatus == OperationalStatus.Up && i.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToArray();
            }
            catch
            {
                _cachedInterfaces = Array.Empty<NetworkInterface>();
            }
            _lastInterfaceCheck = DateTime.UtcNow;
        }
        return _cachedInterfaces;
    }

    public async Task<(long Latency, double PacketLoss, double DownloadMbps, double UploadMbps)> GetNetworkStatusAsync()
    {
        long avgLatency = 0;
        double packetLoss = 100.0;
        
        try
        {
            var reply = await _pingSender.SendPingAsync(TargetHost, 500);
            if (reply.Status == IPStatus.Success)
            {
                avgLatency = reply.RoundtripTime;
                packetLoss = 0.0;
            }
        }
        catch
        {
            // Ignore failure
        }

        // Calculate speeds
        long currentBytesReceived = 0;
        long currentBytesSent = 0;
        
        foreach (var interfaceObj in GetActiveInterfaces())
        {
            try
            {
                var stats = interfaceObj.GetIPStatistics();
                currentBytesReceived += stats.BytesReceived;
                currentBytesSent += stats.BytesSent;
            }
            catch { }
        }

        double downloadMbps = 0;
        double uploadMbps = 0;

        var now = DateTime.UtcNow;
        if (_lastTime != default)
        {
            var seconds = (now - _lastTime).TotalSeconds;
            if (seconds > 0)
            {
                // Bytes to Megabits: (Bytes * 8) / 1,000,000
                downloadMbps = ((currentBytesReceived - _lastBytesReceived) * 8.0 / 1_000_000.0) / seconds;
                uploadMbps = ((currentBytesSent - _lastBytesSent) * 8.0 / 1_000_000.0) / seconds;
                
                // Ensure no negative values due to counter reset
                if (downloadMbps < 0) downloadMbps = 0;
                if (uploadMbps < 0) uploadMbps = 0;
            }
        }

        _lastBytesReceived = currentBytesReceived;
        _lastBytesSent = currentBytesSent;
        _lastTime = now;

        return (avgLatency, packetLoss, downloadMbps, uploadMbps);
    }
}
