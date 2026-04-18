namespace FileCourier.Core.Models;

/// <summary>
/// Represents a peer device discovered on the local network via UDP heartbeat.
/// </summary>
public class SystemDevice
{
    public Guid DeviceId { get; set; } = Guid.NewGuid();
    public string DeviceName { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public int TcpPort { get; set; } = 45455;
    public string OS { get; set; } = string.Empty;
    public bool IsTrusted { get; set; } = false;
    public bool IsGoodbye { get; set; } = false;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}
