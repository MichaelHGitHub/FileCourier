namespace FileCourier.Core.Models;

/// <summary>
/// Describes a single file within a transfer request payload.
/// ByteOffset enables resuming partially transferred files.
/// </summary>
public class TransferFile
{
    public string FileName { get; set; } = string.Empty;

    /// <summary>Relative path preserving directory structure (e.g. "Photos/vacation.jpg").</summary>
    public string RelativePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    /// <summary>Byte position to resume from. 0 for a fresh transfer.</summary>
    public long ByteOffset { get; set; } = 0;

    /// <summary>Optional SHA-256 hex checksum for integrity verification.</summary>
    public string? Checksum { get; set; }
}
