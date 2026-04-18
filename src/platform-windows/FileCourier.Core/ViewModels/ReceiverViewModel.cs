using CommunityToolkit.Mvvm.ComponentModel;
using FileCourier.Core.Models;
using FileCourier.Core.Networking;
using FileCourier.Core.Storage;

namespace FileCourier.Core.ViewModels;

public enum ReceiverState { Idle, PromptingUser, Receiving, Completed }

/// <summary>
/// ViewModel for the receiver role. Raised on the UI thread via the provided dispatcher.
/// </summary>
public sealed partial class ReceiverViewModel : ObservableObject, IDisposable
{
    private readonly TcpTransferService _tcp;
    private readonly TrustStore _trustStore;
    private readonly SettingsStore _settings;
    private Guid? _activeTransferId;
    public Action<Action>? Dispatcher { get; set; }

    [ObservableProperty] private ReceiverState _state = ReceiverState.Idle;
    [ObservableProperty] private string _defaultSavePath;
    [ObservableProperty] private IncomingTransferEventArgs? _pendingTransfer;
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _currentFileName = string.Empty;
    [ObservableProperty] private string _speedDisplay = string.Empty;
    [ObservableProperty] private string _etaDisplay = string.Empty;

    private TransferHistoryRecord? _currentRecord;
    private CancellationTokenSource? _cts;

    public ReceiverViewModel(
        TcpTransferService tcp,
        TrustStore trustStore,
        SettingsStore settings)
    {
        _tcp = tcp;
        _trustStore = trustStore;
        _settings = settings;
        _defaultSavePath = settings.Settings.DefaultSavePath;

        _tcp.IncomingTransferRequested += OnIncomingTransferRequested;
        _tcp.TransferProgressChanged += OnProgress;
        _tcp.TransferCompleted += OnCompleted;
        _tcp.TransferFailed += OnFailed;
    }

    private void OnIncomingTransferRequested(object? sender, IncomingTransferEventArgs e)
    {
        // Pre-flight: disk space check
        var drive = new DriveInfo(Path.GetPathRoot(DefaultSavePath) ?? "C:\\");
        if (drive.AvailableFreeSpace < e.TotalBytes)
        {
            e.SaveDirectory = DefaultSavePath;
            e.SetDecision(false);
            return;
        }

        // Auto-accept trusted devices (files only)
        bool hasFiles = e.Header.Files.Count > 0;
        bool hasText = !string.IsNullOrEmpty(e.Header.TextPayload);

        if (_trustStore.IsDeviceTrusted(e.Header.SenderId, e.Header.SenderName, e.Header.SenderMac) && !hasText)
        {
            e.SaveDirectory = DefaultSavePath;
            e.SetDecision(true);
            _activeTransferId = e.TransferId;
            Dispatcher?.Invoke(() => State = ReceiverState.Receiving);
            return;
        }

        // Prompt user (Always prompt if there's a message, or if untrusted)
        e.SaveDirectory = DefaultSavePath;
        Dispatcher?.Invoke(() =>
        {
            PendingTransfer = e;
            State = ReceiverState.PromptingUser;
        });

        // Block the networking thread until UI sets e.Accepted (dialog is synchronous from the network layer's perspective)
        // In production, use a TaskCompletionSource wired to dialog buttons — this stub holds the pattern.
    }

    public void AcceptOnce(IncomingTransferEventArgs e, string? customPath = null)
    {
        if (customPath is not null) DefaultSavePath = customPath;
        e.SaveDirectory = DefaultSavePath;
        e.SetDecision(true);
        _activeTransferId = e.TransferId;
        State = ReceiverState.Receiving;
    }

    public void AlwaysAgree(IncomingTransferEventArgs e, string? customPath = null)
    {
        _trustStore.AddTrustedDevice(e.Header.SenderId, e.Header.SenderName, e.Header.SenderMac, e.SenderIp);
        AcceptOnce(e, customPath);
    }

    public void Deny(IncomingTransferEventArgs e)
    {
        e.SetDecision(false);
        State = ReceiverState.Idle;
        PendingTransfer = null;
    }

    private void OnProgress(object? sender, TransferProgressEventArgs e)
    {
        if (_activeTransferId == null || e.TransferId != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            ProgressPercent = e.ProgressPercent;
            CurrentFileName = e.CurrentFileName;
            SpeedDisplay = FormatSpeed(e.SpeedBytesPerSecond);
            EtaDisplay = e.EstimatedRemaining.HasValue
                ? $"{e.EstimatedRemaining.Value:mm\\:ss} remaining"
                : string.Empty;
        });
    }

    private void OnCompleted(object? sender, Guid id)
    {
        if (_activeTransferId == null || id != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            // If it was just a text message (no files), we go straight to Idle.
            // Otherwise we show Completed state briefly.
            bool hasFiles = PendingTransfer?.Header.Files.Count > 0;
            
            State = hasFiles ? ReceiverState.Completed : ReceiverState.Idle;
            PendingTransfer = null;
            
            if (hasFiles)
            {
                // Auto-reset to Idle after a short delay to clear UI state
                _ = Task.Delay(2000).ContinueWith(_ => Dispatcher?.Invoke(() => 
                {
                    if (State == ReceiverState.Completed) State = ReceiverState.Idle;
                }));
            }
        });
    }

    private void OnFailed(object? sender, (Guid id, string error) e)
    {
        if (_activeTransferId == null || e.id != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            State = ReceiverState.Idle;
            PendingTransfer = null;
        });
    }


    private static string FormatSpeed(double bps) =>
        bps switch
        {
            >= 1_000_000_000 => $"{bps / 1_000_000_000:F1} GB/s",
            >= 1_000_000 => $"{bps / 1_000_000:F1} MB/s",
            >= 1_000 => $"{bps / 1_000:F1} KB/s",
            _ => $"{bps:F0} B/s"
        };

    public void Dispose()
    {
        _tcp.IncomingTransferRequested -= OnIncomingTransferRequested;
        _tcp.TransferProgressChanged -= OnProgress;
        _tcp.TransferCompleted -= OnCompleted;
    }
}
