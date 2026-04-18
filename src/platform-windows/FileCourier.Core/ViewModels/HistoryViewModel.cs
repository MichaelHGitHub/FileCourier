using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;
using FileCourier.Core.Networking;
using CommunityToolkit.Mvvm.Input;
using System.IO;

namespace FileCourier.Core.ViewModels;

/// <summary>Exposes transfer history records for binding in HistoryPage.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly TransferHistoryStore _store;
    private readonly DeviceListViewModel _deviceList;
    private readonly TcpTransferService _tcp;
    private readonly SettingsStore _settings;

    public Action<Action>? Dispatcher { get; set; }
    public ObservableCollection<TransferHistoryRecord> Records { get; } = new();

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
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading history: {ex.Message}";
        }
    }

    public void Refresh()
    {
        Action action = () =>
        {
            Records.Clear();
            var sentRecords = _store.GetAll()
                .Where(r => r.Direction == TransferDirection.Sent)
                .ToList();

            var last3Transfers = sentRecords
                .GroupBy(r => r.Timestamp)
                .OrderByDescending(g => g.Key)
                .Take(3);

            foreach (var group in last3Transfers)
            {
                foreach (var r in group)
                {
                    Records.Add(r);
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
    public void ClearAll()
    {
        _store.ClearAll();
        Refresh();
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
                var header = new TransferRequestHeader
                {
                    SenderId = _settings.Settings.DeviceId,
                    SenderName = _settings.Settings.DeviceName,
                    SenderMac = NetworkUtils.GetMacAddress(),
                    Files = files
                };

                await _tcp.SendAsync(device, header, existingPaths, retryId);
                
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
            catch (Exception ex)
            {
                StatusMessage = $"Transfer failed: {ex.Message}";
                record.Status = TransferStatus.Failed;
                _store.UpdateRecord(record);
            }
            finally
            {
                _tcp.TransferProgressChanged -= progressHandler;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unexpected error: {ex.Message}";
        }
    }
}
