using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;

namespace FileCourier.Core.ViewModels;

/// <summary>Exposes app settings and trusted-device management for SettingsPage binding.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _settingsStore;
    private readonly TrustStore _trustStore;

    [ObservableProperty] private string _deviceName;
    [ObservableProperty] private string _defaultSavePath;
    [ObservableProperty] private ConflictBehavior _conflictBehavior;
    [ObservableProperty] private long _maxBandwidthBytesPerSecond;

    public ObservableCollection<TrustedDeviceRecord> TrustedDevices { get; } = new();

    public SettingsViewModel(SettingsStore settingsStore, TrustStore trustStore)
    {
        _settingsStore = settingsStore;
        _trustStore = trustStore;

        var s = settingsStore.Settings;
        _deviceName = s.DeviceName;
        _defaultSavePath = s.DefaultSavePath;
        _conflictBehavior = s.ConflictBehavior;
        _maxBandwidthBytesPerSecond = s.MaxBandwidthBytesPerSecond;

        RefreshTrustedDevices();
    }

    [RelayCommand]
    public void Save()
    {
        _settingsStore.Update(s =>
        {
            s.DeviceName = DeviceName;
            s.DefaultSavePath = DefaultSavePath;
            s.ConflictBehavior = ConflictBehavior;
            s.MaxBandwidthBytesPerSecond = MaxBandwidthBytesPerSecond;
        });
    }

    [RelayCommand]
    public void RevokeTrust(Guid deviceId)
    {
        _trustStore.RevokeTrustedDevice(deviceId);
        RefreshTrustedDevices();
    }

    private void RefreshTrustedDevices()
    {
        TrustedDevices.Clear();
        foreach (var d in _trustStore.GetAll())
            TrustedDevices.Add(d);
    }
}
