namespace FileCourier.Core.Models;

public enum TransferResponseStatus { Accepted, Rejected }

/// <summary>
/// Response sent by the receiver back to the sender before any data is streamed.
/// If Status is Rejected, the TCP socket is closed immediately after sending.
/// </summary>
public class TransferResponseHeader
{
    public int SchemaVersion { get; set; } = 1;
    public TransferResponseStatus Status { get; set; } = TransferResponseStatus.Accepted;
    public string Reason { get; set; } = string.Empty;
}
