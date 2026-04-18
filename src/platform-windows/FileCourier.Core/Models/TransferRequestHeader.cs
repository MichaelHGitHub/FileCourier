namespace FileCourier.Core.Models;

/// <summary>
/// Header sent by the sender at the start of a TCP transfer session.
/// Wire format: [HeaderLength: 4-byte Int32] + [Header JSON bytes] + [File Data Chunks]
/// If IsEncrypted=true, an ECDH key exchange precedes this header.
/// </summary>
public class TransferRequestHeader
{
    public int SchemaVersion { get; set; } = 2;
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string SenderMac { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; } = false;

    /// <summary>Optional clipboard/text payload. No file stream is sent if Files is empty.</summary>
    public string? TextPayload { get; set; }

    public List<TransferFile> Files { get; set; } = new();
}
