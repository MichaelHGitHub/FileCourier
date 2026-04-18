namespace FileCourier.Core.Models;

/// <summary>
/// Persisted record of a device the user has marked as "Always Agree".
/// Stored in SQLite via TrustStore.
/// </summary>
public class TrustedDeviceRecord
{
    public Guid TrustedDeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string LastKnownIp { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
}
