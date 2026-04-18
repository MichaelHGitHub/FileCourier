namespace FileCourier.Core.Models;

public enum TransferDirection { Sent, Received }
public enum TransferStatus { Completed, Cancelled, Failed }

/// <summary>
/// Persisted record of a completed (or failed/cancelled) transfer, stored in SQLite.
/// </summary>
public class TransferHistoryRecord
{
    public Guid TransferId { get; set; } = Guid.NewGuid();
    public Guid CounterpartyId { get; set; }
    public string CounterpartyName { get; set; } = string.Empty;
    public TransferDirection Direction { get; set; }
    public int TotalFiles { get; set; }
    public long TotalSize { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public TransferStatus Status { get; set; }
}
