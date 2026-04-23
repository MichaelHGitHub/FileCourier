using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;
using FileCourier.Core.Services;

namespace FileCourier.Core.ViewModels;

/// <summary>Exposes app settings and trusted-device management for SettingsPage binding.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _settingsStore;
    private readonly TrustStore _trustStore;
    private readonly IStartupService _startupService;

    [ObservableProperty] private string _deviceName;
    [ObservableProperty] private string _defaultSavePath;
    [ObservableProperty] private ConflictBehavior _conflictBehavior;
    [ObservableProperty] private long _maxBandwidthBytesPerSecond;
    [ObservableProperty] private bool _startWithWindows;

    public double MaxBandwidthMb
    {
        get => MaxBandwidthBytesPerSecond / 1_000_000.0;
        set => MaxBandwidthBytesPerSecond = (long)(value * 1_000_000);
    }

    public ObservableCollection<TrustedDeviceRecord> TrustedDevices { get; } = new();

    public SettingsViewModel(SettingsStore settingsStore, TrustStore trustStore, IStartupService startupService)
    {
        _settingsStore = settingsStore;
        _trustStore = trustStore;
        _startupService = startupService;

        var s = settingsStore.Settings;
        _deviceName = s.DeviceName;
        _defaultSavePath = s.DefaultSavePath;
        _conflictBehavior = s.ConflictBehavior;
        _maxBandwidthBytesPerSecond = s.MaxBandwidthBytesPerSecond;
        _startWithWindows = s.StartWithWindows;

        RefreshTrustedDevices();
    }

    partial void OnDeviceNameChanged(string value) => AutoSave();
    partial void OnDefaultSavePathChanged(string value) => AutoSave();
    partial void OnConflictBehaviorChanged(ConflictBehavior value) => AutoSave();
    partial void OnMaxBandwidthBytesPerSecondChanged(long value) => AutoSave();
    
    partial void OnStartWithWindowsChanged(bool value)
    {
        _startupService.SetStartup(value);
        AutoSave();
    }

    private void AutoSave()
    {
        Save();
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
            s.StartWithWindows = StartWithWindows;
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
