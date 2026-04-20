using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;
using FileCourier.Core.Networking;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace FileCourier.Core.ViewModels;

/// <summary>Exposes transfer history records for binding in HistoryPage.</summary>
public sealed partial class HistoryViewModel : ObservableObject, IDisposable
{
    private readonly TransferHistoryStore _store;
    private readonly DeviceListViewModel _deviceList;
    private readonly TcpTransferService _tcp;
    private readonly SettingsStore _settings;

    public Action<Action>? Dispatcher { get; set; }
    public Func<string, string, Task>? ShowDialogAsync { get; set; }
    public ObservableCollection<TransferHistoryRecord> Records { get; } = new();
    public ObservableCollection<TransferHistoryRecord> ReceivedRecords { get; } = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _activeRetries = new();

    [ObservableProperty] private string _statusMessage = string.Empty;

    public HistoryViewModel(
        TransferHistoryStore store, 
        DeviceListViewModel deviceList,
        TcpTransferService tcp,
        SettingsStore settings)
    {
        _store = store;
        _deviceList = deviceList;
        _tcp = tcp;
        _settings = settings;
        try
        {
            Refresh();
            _tcp.TransferCompleted += OnTransferCompleted;
            _tcp.TransferFailed += OnTransferFailed;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading history: {ex.Message}";
        }
    }

    private void OnTransferCompleted(object? sender, Guid transferId)
    {
        // Give the database a brief moment to finish writing records from the view models
        Task.Delay(500).ContinueWith(_ => Refresh());
    }

    private void OnTransferFailed(object? sender, (Guid TransferId, string Error) e)
    {
        Task.Delay(500).ContinueWith(_ => Refresh());
    }

    public void Refresh()
    {
        Action action = () =>
        {
            Records.Clear();
            ReceivedRecords.Clear();

            var allRecords = _store.GetAll();

            var sentRecords = allRecords
                .Where(r => r.Direction == TransferDirection.Sent)
                .ToList();

            var last3SentTransfers = sentRecords
                .GroupBy(r => r.Timestamp)
                .OrderByDescending(g => g.Key)
                .Take(3);

            foreach (var group in last3SentTransfers)
            {
                foreach (var r in group)
                {
                    Records.Add(r);
                }
            }

            var receivedRecords = allRecords
                .Where(r => r.Direction == TransferDirection.Received)
                .ToList();

            var last3ReceivedTransfers = receivedRecords
                .GroupBy(r => r.Timestamp)
                .OrderByDescending(g => g.Key)
                .Take(3);

            foreach (var group in last3ReceivedTransfers)
            {
                foreach (var r in group)
                {
                    ReceivedRecords.Add(r);
                }
            }
        };

        if (Dispatcher != null) Dispatcher(action);
        else action();
    }

    [RelayCommand]
    public void DeleteRecord(TransferHistoryRecord record)
    {
        _store.DeleteRecord(record.TransferId);
        Refresh();
    }

    [RelayCommand]
    public void ClearSent()
    {
        _store.ClearDirection(TransferDirection.Sent);
        Refresh();
    }

    [RelayCommand]
    public void ClearReceived()
    {
        _store.ClearDirection(TransferDirection.Received);
        Refresh();
    }

    [RelayCommand]
    public async Task OpenLocalFolderAsync(TransferHistoryRecord record)
    {
        if (File.Exists(record.ItemPath))
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{record.ItemPath}\"");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open Explorer: {ex.Message}";
            }
        }
        else
        {
            if (ShowDialogAsync != null)
            {
                await ShowDialogAsync("File not found", "The file has been moved or deleted.");
            }
            else
            {
                StatusMessage = "File no longer exists.";
            }
        }
    }

    [RelayCommand]
    public void CancelRetry(TransferHistoryRecord record)
    {
        if (_activeRetries.TryGetValue(record.TransferId, out var cts))
        {
            cts.Cancel();
        }
    }

    [RelayCommand]
    public async Task RetryAsync(TransferHistoryRecord record)
    {
        if (record.Direction != TransferDirection.Sent) return;

        var device = _deviceList.OnlineDevices.FirstOrDefault(d => d.DeviceId == record.CounterpartyId);
        if (device == null)
        {
            StatusMessage = "Device is not online.";
            return;
        }

        string actionText = record.BytesTransferred > 0 ? "Resuming" : "Retrying";
        StatusMessage = $"{actionText} transfer...";
        
        try 
        {
            var paths = record.SourcePaths.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var existingPaths = paths.Where(File.Exists).ToList();
            var files = existingPaths.Select(p => new TransferFile
            {
                FileName = Path.GetFileName(p),
                RelativePath = Path.GetFileName(p), // Flatten for simple retry
                FileSize = new FileInfo(p).Length,
                ByteOffset = record.BytesTransferred // Use stored offset for resume
            }).ToList();

            if (files.Count == 0)
            {
                StatusMessage = "Source files no longer exist.";
                return;
            }

            var retryId = record.TransferId;
            EventHandler<TransferProgressEventArgs> progressHandler = (s, e) =>
            {
                if (e.TransferId != retryId) return;
                Dispatcher?.Invoke(() =>
                {
                    record.BytesTransferred = e.BytesTransferred;
                });
            };

            _tcp.TransferProgressChanged += progressHandler;

            try 
            {
                Dispatcher?.Invoke(() => record.IsTransferring = true);
                var header = new TransferRequestHeader
                {
                    SenderId = _settings.Settings.DeviceId,
                    SenderName = _settings.Settings.DeviceName,
                    SenderMac = NetworkUtils.GetMacAddress(),
                    Files = files
                };

                var cts = new CancellationTokenSource();
                _activeRetries[retryId] = cts;

                await _tcp.SendAsync(device, header, existingPaths, retryId, ct: cts.Token);
                
                // Update properties first so they are ready for DB save
                record.Status = TransferStatus.Completed;
                record.BytesTransferred = record.TotalSize;
                _store.UpdateRecord(record);

                Dispatcher?.Invoke(() =>
                {
                    // Triggers UI update
                    StatusMessage = "Transfer completed.";
                });
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Transfer cancelled.";
                record.Status = TransferStatus.Cancelled;
                _store.UpdateRecord(record);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Transfer failed: {ex.Message}";
                record.Status = TransferStatus.Failed;
                _store.UpdateRecord(record);
            }
            finally
            {
                if (_activeRetries.Remove(retryId, out var cts))
                {
                    cts.Dispose();
                }
                Dispatcher?.Invoke(() => record.IsTransferring = false);
                _tcp.TransferProgressChanged -= progressHandler;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
    }

    public void Dispose()
    {
        _tcp.TransferCompleted -= OnTransferCompleted;
        _tcp.TransferFailed -= OnTransferFailed;
    }
}
