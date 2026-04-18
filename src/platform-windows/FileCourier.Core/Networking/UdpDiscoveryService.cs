using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FileCourier.Core.Models;

namespace FileCourier.Core.Networking;

public class DeviceEventArgs(SystemDevice device) : EventArgs
{
    public SystemDevice Device { get; } = device;
}

/// <summary>
/// Sends UDP heartbeat payloads every 3 seconds across all active network interfaces
/// and listens for payloads from other FileCourier peers.
/// Raises DeviceDiscovered / DeviceLost events as the online device list changes.
/// </summary>
public sealed class UdpDiscoveryService : IDisposable
{
    public const int DefaultUdpPort = 45454;

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceLost;

    private readonly Guid _deviceId;
    private readonly string _deviceName;
    private readonly string _os;
    private readonly int _tcpPort;
    private readonly int _udpPort;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(3);
    private readonly TimeSpan _deviceTimeout = TimeSpan.FromSeconds(10);

    private readonly Dictionary<Guid, SystemDevice> _onlineDevices = new();
    private readonly object _devicesLock = new();

    private CancellationTokenSource? _cts;
    private bool _disposed;

    public UdpDiscoveryService(Guid deviceId, string deviceName, string os, int tcpPort, int udpPort = DefaultUdpPort)
    {
        _deviceId = deviceId;
        _deviceName = deviceName;
        _os = os;
        _tcpPort = tcpPort;
        _udpPort = udpPort;
    }

    public IReadOnlyList<SystemDevice> GetOnlineDevices()
    {
        lock (_devicesLock)
            return _onlineDevices.Values.ToList();
    }

    /// <summary>Add a device by IP for manual-connection fallback (spec Scenario 1).</summary>
    public void AddManualPeer(string ipAddress, int tcpPort)
    {
        var device = new SystemDevice
        {
            DeviceName = $"{ipAddress}:{tcpPort}",
            IPAddress = ipAddress,
            TcpPort = tcpPort,
            OS = "Unknown",
            LastSeenUtc = DateTime.UtcNow
        };
        lock (_devicesLock)
            _onlineDevices[device.DeviceId] = device;
        DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _cts = new CancellationTokenSource();
        _ = BroadcastLoopAsync(_cts.Token);
        _ = ListenLoopAsync(_cts.Token);
        _ = PruneLoopAsync(_cts.Token);
    }

    // ── Broadcast ──────────────────────────────────────────────────────────

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await BroadcastHeartbeatAsync(isGoodbye: false, ct);
                await Task.Delay(_heartbeatInterval, ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* transient network error — continue */ }
        }
    }

    private async Task BroadcastHeartbeatAsync(bool isGoodbye, CancellationToken ct = default)
    {
        var payload = new DiscoveryPayload
        {
            DeviceId = _deviceId,
            DeviceName = _deviceName,
            MacAddress = GetMacAddress(), // Get primary MAC
            OS = _os,
            TcpPort = _tcpPort,
            IsGoodbye = isGoodbye
        };
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback) continue;

            foreach (var unicast in iface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                try
                {
                    using var udp = new UdpClient(AddressFamily.InterNetwork);
                    udp.EnableBroadcast = true;
                    udp.Client.Bind(new IPEndPoint(unicast.Address, 0));
                    var broadcast = GetBroadcastAddress(unicast.Address, unicast.IPv4Mask);
                    await udp.SendAsync(bytes, bytes.Length, new IPEndPoint(broadcast, _udpPort));
                }
                catch { /* interface may not support broadcast */ }
            }
        }
    }

    // ── Listen ─────────────────────────────────────────────────────────────

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var udp = new UdpClient();
                // On some systems, we need to allow port reuse if multiple instances or quick restarts happen
                udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, _udpPort));

                while (!ct.IsCancellationRequested)
                {
                    var result = await udp.ReceiveAsync(ct);
                    var payload = JsonSerializer.Deserialize<DiscoveryPayload>(result.Buffer);
                    if (payload is null || payload.DeviceId == _deviceId) continue;
                    HandleIncomingPayload(payload, result.RemoteEndPoint.Address.ToString());
                }
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) 
            {
                // Socket errors (e.g. network down) — wait a bit and retry
                try { await Task.Delay(2000, ct); } catch { break; }
            }
            catch { /* malformed packet or other — continue */ }
        }
    }

    public void Refresh()
    {
        lock (_devicesLock)
        {
            _onlineDevices.Clear();
        }
        _ = BroadcastHeartbeatAsync(isGoodbye: false);
    }

    private void HandleIncomingPayload(DiscoveryPayload payload, string senderIp)
    {
        var device = new SystemDevice
        {
            DeviceId = payload.DeviceId,
            DeviceName = payload.DeviceName,
            IPAddress = senderIp,
            MacAddress = payload.MacAddress,
            TcpPort = payload.TcpPort,
            OS = payload.OS,
            LastSeenUtc = DateTime.UtcNow
        };

        if (payload.IsGoodbye)
        {
            SystemDevice? removed = null;
            lock (_devicesLock) _onlineDevices.Remove(payload.DeviceId, out removed);
            if (removed is not null) DeviceLost?.Invoke(this, new DeviceEventArgs(removed));
            return;
        }

        bool isNew;
        lock (_devicesLock)
        {
            isNew = !_onlineDevices.ContainsKey(device.DeviceId);
            _onlineDevices[device.DeviceId] = device;
        }
        if (isNew) DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
    }

    // ── Prune stale devices ─────────────────────────────────────────────────

    private async Task PruneLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_heartbeatInterval, ct);
                var cutoff = DateTime.UtcNow - _deviceTimeout;
                List<SystemDevice> lost = new();
                lock (_devicesLock)
                {
                    var stale = _onlineDevices.Where(kv => kv.Value.LastSeenUtc < cutoff).ToList();
                    foreach (var kv in stale) { _onlineDevices.Remove(kv.Key); lost.Add(kv.Value); }
                }
                foreach (var d in lost) DeviceLost?.Invoke(this, new DeviceEventArgs(d));
            }
            catch (OperationCanceledException) { break; }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    
    private static string GetMacAddress() => NetworkUtils.GetMacAddress();

    private static IPAddress GetBroadcastAddress(IPAddress addr, IPAddress mask)
    {
        var a = addr.GetAddressBytes();
        var m = mask.GetAddressBytes();
        return new IPAddress(new byte[] { (byte)(a[0] | ~m[0]), (byte)(a[1] | ~m[1]), (byte)(a[2] | ~m[2]), (byte)(a[3] | ~m[3]) });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts?.Cancel();
        try { BroadcastHeartbeatAsync(isGoodbye: true).GetAwaiter().GetResult(); } catch { }
        _cts?.Dispose();
    }
}
