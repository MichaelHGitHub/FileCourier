using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileCourier.Core.Models;
using FileCourier.Core.Networking;
using FileCourier.Core.Storage;

namespace FileCourier.Core.ViewModels;

/// <summary>
/// Maintains the observable list of online peers by subscribing to UdpDiscoveryService events.
/// Thread-safe: events are marshalled via the provided dispatcher delegate.
/// </summary>
public sealed partial class DeviceListViewModel : ObservableObject, IDisposable
{
    private readonly UdpDiscoveryService _discovery;
    private readonly TrustStore _trustStore;
    
    /// <summary>Set by the UI layer to enable thread-safe updates.</summary>
    public Action<Action>? Dispatcher { get; set; }

    public ObservableCollection<SystemDevice> OnlineDevices { get; } = new();

    [ObservableProperty]
    private string _statusMessage = "Scanning for devices on your local network…";

    public DeviceListViewModel(UdpDiscoveryService discovery, TrustStore trustStore)
    {
        _discovery = discovery;
        _trustStore = trustStore;
        _discovery.DeviceDiscovered += OnDeviceDiscovered;
        _discovery.DeviceLost += OnDeviceLost;
    }

    private void OnDeviceDiscovered(object? sender, DeviceEventArgs e) =>
        Dispatcher?.Invoke(() =>
        {
            if (!OnlineDevices.Any(d => d.DeviceId == e.Device.DeviceId))
            {
                e.Device.IsTrusted = _trustStore.IsDeviceTrusted(e.Device.DeviceId, e.Device.DeviceName, e.Device.MacAddress);
                OnlineDevices.Add(e.Device);
            }
            StatusMessage = $"{OnlineDevices.Count} device(s) online";
        });

    private void OnDeviceLost(object? sender, DeviceEventArgs e) =>
        Dispatcher?.Invoke(() =>
        {
            var existing = OnlineDevices.FirstOrDefault(d => d.DeviceId == e.Device.DeviceId);
            if (existing is not null) OnlineDevices.Remove(existing);
            StatusMessage = OnlineDevices.Count > 0
                ? $"{OnlineDevices.Count} device(s) online"
                : "Scanning for devices on your local network…";
        });

    public void AddManualPeer(string ip, int port) => _discovery.AddManualPeer(ip, port);

    public void Refresh()
    {
        _discovery.Refresh();
        Dispatcher?.Invoke(() =>
        {
            OnlineDevices.Clear();
            StatusMessage = "Refreshing device list…";
        });
    }

    public void Dispose()
    {
        _discovery.DeviceDiscovered -= OnDeviceDiscovered;
        _discovery.DeviceLost -= OnDeviceLost;
    }
}
