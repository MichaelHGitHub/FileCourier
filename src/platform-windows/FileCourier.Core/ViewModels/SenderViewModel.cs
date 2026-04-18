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
    private readonly TransferHistoryStore _historyStore;
    public Action<Action>? Dispatcher { get; set; }
    private Guid? _activeTransferId;

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

    private List<TransferHistoryRecord> _currentSessionRecords = new();

    public string FileCountDisplay => SelectedFiles.Count == 0 ? "No items selected" : $"{SelectedFiles.Count} item(s) selected";

    private CancellationTokenSource? _cts;

    public SenderViewModel(TcpTransferService tcp, SettingsStore settingsStore, TransferHistoryStore historyStore)
    {
        _tcp = tcp;
        _settingsStore = settingsStore;
        _historyStore = historyStore;
        _tcp.TransferProgressChanged += OnProgress;
        _tcp.TransferFailed += OnFailed;
        _tcp.FileTransferFailed += OnFileFailed;
        _tcp.FileTransferCompleted += OnFileCompleted;
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
            _activeTransferId = Guid.NewGuid();
            _currentSessionRecords.Clear();
            var now = DateTime.Now;

            if (SelectedFiles.Count > 0)
            {
                foreach (var file in SelectedFiles)
                {
                    _currentSessionRecords.Add(new TransferHistoryRecord
                    {
                        TransferId = Guid.NewGuid(),
                        CounterpartyId = TargetDevice.DeviceId,
                        CounterpartyName = TargetDevice.DeviceName,
                        Direction = TransferDirection.Sent,
                        ItemName = Path.GetFileName(file.AbsolutePath),
                        ItemPath = Path.GetDirectoryName(file.AbsolutePath) ?? string.Empty,
                        SourcePaths = file.AbsolutePath,
                        TotalFiles = 1,
                        TotalSize = new FileInfo(file.AbsolutePath).Length,
                        Timestamp = now,
                        Status = TransferStatus.InProgress
                    });
                }
            }

            await _tcp.SendAsync(TargetDevice, header, filePaths, transferId: _activeTransferId, ct: _cts.Token);
            State = SenderState.Completed;
            foreach (var r in _currentSessionRecords)
            {
                if (r.Status != TransferStatus.Failed)
                {
                    r.Status = TransferStatus.Completed;
                    r.BytesTransferred = r.TotalSize;
                }
                _historyStore.AddRecord(r);
            }
        }
        catch (OperationCanceledException)
        {
            State = SenderState.Idle;
            foreach (var r in _currentSessionRecords.Where(r => r.Status == TransferStatus.InProgress))
            {
                r.Status = TransferStatus.Cancelled;
                _historyStore.AddRecord(r);
            }
            foreach (var f in SelectedFiles.Where(f => f.Status == FileStatus.Added))
            {
                f.Status = FileStatus.Canceled;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            State = SenderState.Failed;
            foreach (var r in _currentSessionRecords)
            {
                r.Status = TransferStatus.Failed;
                _historyStore.AddRecord(r);
            }
        }
    }

    [RelayCommand]
    public async Task RetryFileAsync(FileItem item)
    {
        if (TargetDevice is null || item is null) return;

        State = SenderState.Transferring;
        ErrorMessage = string.Empty;
        item.Status = FileStatus.Added;
        item.ErrorMessage = string.Empty;

        var header = new TransferRequestHeader
        {
            SenderId = _settingsStore.Settings.DeviceId,
            SenderName = _settingsStore.Settings.DeviceName,
            SenderMac = NetworkUtils.GetMacAddress(),
            IsEncrypted = IsEncrypted,
            Files = new List<TransferFile>
            {
                new TransferFile
                {
                    FileName = Path.GetFileName(item.AbsolutePath),
                    RelativePath = item.RelativePath,
                    FileSize = new FileInfo(item.AbsolutePath).Length,
                    ByteOffset = 0
                }
            }
        };

        _activeTransferId = Guid.NewGuid();
        _currentSessionRecords.Clear();
        _currentSessionRecords.Add(new TransferHistoryRecord
        {
            TransferId = _activeTransferId.Value,
            CounterpartyId = TargetDevice.DeviceId,
            CounterpartyName = TargetDevice.DeviceName,
            Direction = TransferDirection.Sent,
            ItemName = Path.GetFileName(item.AbsolutePath),
            ItemPath = Path.GetDirectoryName(item.AbsolutePath) ?? string.Empty,
            SourcePaths = item.AbsolutePath,
            TotalFiles = 1,
            TotalSize = new FileInfo(item.AbsolutePath).Length,
            Timestamp = DateTime.Now,
            Status = TransferStatus.InProgress
        });

        try 
        {
            await _tcp.SendAsync(TargetDevice, header, new List<string> { item.AbsolutePath }, transferId: _activeTransferId);
            item.Status = FileStatus.Transferred;
            State = SenderState.Completed;

            foreach (var r in _currentSessionRecords)
            {
                r.Status = TransferStatus.Completed;
                r.BytesTransferred = r.TotalSize;
                _historyStore.AddRecord(r);
            }
        }
        catch (Exception ex)
        {
            item.Status = FileStatus.Failed;
            item.ErrorMessage = ex.Message;
            ErrorMessage = ex.Message;
            State = SenderState.Failed;

            foreach (var r in _currentSessionRecords)
            {
                r.Status = TransferStatus.Failed;
                _historyStore.AddRecord(r);
            }
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
        if (_activeTransferId == null || e.TransferId != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            ProgressPercent = e.ProgressPercent;
            CurrentFileName = e.CurrentFileName;
            SpeedDisplay = FormatSpeed(e.SpeedBytesPerSecond);
            EtaDisplay = e.EstimatedRemaining.HasValue
                ? $"{e.EstimatedRemaining.Value:mm\\:ss} remaining"
                : string.Empty;

            // Update individual record progress
            long cumulative = e.BytesTransferred;
            foreach (var r in _currentSessionRecords)
            {
                if (cumulative >= r.TotalSize)
                {
                    r.BytesTransferred = r.TotalSize;
                    cumulative -= r.TotalSize;
                }
                else
                {
                    r.BytesTransferred = cumulative;
                    cumulative = 0;
                }
            }
        });
    }



    private void OnFailed(object? sender, (Guid id, string error) e)
    {
        if (_activeTransferId == null || e.id != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            ErrorMessage = e.error;
            State = SenderState.Failed;
            foreach (var r in _currentSessionRecords)
            {
                r.Status = TransferStatus.Failed;
                _historyStore.AddRecord(r);
            }
        });
    }

    private void OnFileFailed(object? sender, (Guid id, string fileName, string error) e)
    {
        if (_activeTransferId == null || e.id != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            var record = _currentSessionRecords.FirstOrDefault(r => r.ItemName == e.fileName);
            if (record != null)
            {
                record.Status = TransferStatus.Failed;
            }

            var item = SelectedFiles.FirstOrDefault(f => Path.GetFileName(f.AbsolutePath) == e.fileName || f.RelativePath == e.fileName);
            if (item != null)
            {
                item.Status = FileStatus.Failed;
                item.ErrorMessage = e.error;
            }
        });
    }

    private void OnFileCompleted(object? sender, (Guid TransferId, string FileName) e)
    {
        if (_activeTransferId == null || e.TransferId != _activeTransferId) return;
        Dispatcher?.Invoke(() =>
        {
            var item = SelectedFiles.FirstOrDefault(f => Path.GetFileName(f.AbsolutePath) == e.FileName || f.RelativePath == e.FileName);
            if (item != null)
            {
                item.Status = FileStatus.Transferred;
            }
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
}
