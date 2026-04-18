using CommunityToolkit.Mvvm.ComponentModel;

namespace FileCourier.Core.Models;

public enum TransferDirection { Sent, Received }
public enum TransferStatus { Completed, Cancelled, Failed }

/// <summary>
/// Persisted record of a completed (or failed/cancelled) transfer, stored in SQLite.
/// </summary>
public partial class TransferHistoryRecord : ObservableObject
{
    public Guid TransferId { get; set; } = Guid.NewGuid();
    public Guid CounterpartyId { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public TransferDirection Direction { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ItemPath { get; set; } = string.Empty;
    public string SourcePaths { get; set; } = string.Empty; // Semicolon separated list of local paths
    public int TotalFiles { get; set; }
    public long TotalSize { get; set; }
    
    [ObservableProperty] 
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    private long _bytesTransferred;
    
    [ObservableProperty] private TransferStatus _status;
    
    [ObservableProperty] private bool _isTransferring;

    public double ProgressPercent => TotalSize > 0 ? (double)BytesTransferred / TotalSize * 100 : 0;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
