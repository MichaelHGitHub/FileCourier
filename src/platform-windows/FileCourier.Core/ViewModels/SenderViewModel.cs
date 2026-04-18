using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCourier.Core.Models;
using FileCourier.Core.Networking;
using FileCourier.Core.Storage;

namespace FileCourier.Core.ViewModels;

public enum SenderState { Idle, WaitingForReceiver, Transferring, Completed, Rejected, Failed }

/// <summary>
/// ViewModel for the sender role. Drives the DevicesPage UI state machine.
/// </summary>
public sealed partial class SenderViewModel : ObservableObject
{
    private readonly TcpTransferService _tcp;
    private readonly SettingsStore _settingsStore;

    [ObservableProperty] private SenderState _state = SenderState.Idle;
    [ObservableProperty] private SystemDevice? _targetDevice;
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(FileCountDisplay))]
    private List<FileItem> _selectedFiles = new();
    
    [ObservableProperty] private string? _textPayload;
    [ObservableProperty] private bool _isEncrypted = false;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _currentFileName = string.Empty;
    [ObservableProperty] private string _speedDisplay = string.Empty;
    [ObservableProperty] private string _etaDisplay = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public string FileCountDisplay => SelectedFiles.Count == 0 ? "No items selected" : $"{SelectedFiles.Count} item(s) selected";

    private CancellationTokenSource? _cts;

    public SenderViewModel(TcpTransferService tcp, SettingsStore settingsStore)
    {
        _tcp = tcp;
        _settingsStore = settingsStore;
        _tcp.TransferProgressChanged += OnProgress;
        _tcp.TransferCompleted += OnCompleted;
        _tcp.TransferFailed += OnFailed;
    }

    [RelayCommand]
    public async Task SendAsync()
    {
        if (TargetDevice is null || (SelectedFiles.Count == 0 && string.IsNullOrWhiteSpace(TextPayload)))
            return;

        State = SenderState.WaitingForReceiver;
        _cts = new CancellationTokenSource();

        try
        {
            var header = new TransferRequestHeader
            {
                SenderId = _settingsStore.Settings.DeviceId,
                SenderName = _settingsStore.Settings.DeviceName,
                SenderMac = NetworkUtils.GetMacAddress(),
                IsEncrypted = IsEncrypted,
                TextPayload = TextPayload,
                Files = SelectedFiles.Select(item => new TransferFile
                {
                    FileName = Path.GetFileName(item.AbsolutePath),
                    RelativePath = item.RelativePath,
                    FileSize = new FileInfo(item.AbsolutePath).Length,
                    ByteOffset = 0
                }).ToList()
            };

            var filePaths = SelectedFiles.Select(f => f.AbsolutePath).ToList();

            State = SenderState.Transferring;
            await _tcp.SendAsync(TargetDevice, header, filePaths, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            State = SenderState.Idle;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = SenderState.Failed;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        _cts?.Cancel();
        State = SenderState.Idle;
    }

    [RelayCommand]
    public void Reset()
    {
        State = SenderState.Idle;
        SelectedFiles = new();
        TextPayload = null;
        TargetDevice = null;
        ProgressPercent = 0;
        SpeedDisplay = string.Empty;
        EtaDisplay = string.Empty;
        ErrorMessage = string.Empty;
    }

    private void OnProgress(object? sender, TransferProgressEventArgs e)
    {
        ProgressPercent = e.ProgressPercent;
        CurrentFileName = e.CurrentFileName;
        SpeedDisplay = FormatSpeed(e.SpeedBytesPerSecond);
        EtaDisplay = e.EstimatedRemaining.HasValue
            ? $"{e.EstimatedRemaining.Value:mm\\:ss} remaining"
            : string.Empty;
    }

    private void OnCompleted(object? sender, Guid id) => State = SenderState.Completed;
    private void OnFailed(object? sender, (Guid id, string error) e)
    {
        ErrorMessage = e.error;
        State = SenderState.Failed;
    }

    private static string FormatSpeed(double bps) =>
        bps switch
        {
            >= 1_000_000_000 => $"{bps / 1_000_000_000:F1} GB/s",
            >= 1_000_000 => $"{bps / 1_000_000:F1} MB/s",
            >= 1_000 => $"{bps / 1_000:F1} KB/s",
            _ => $"{bps:F0} B/s"
        };
}
