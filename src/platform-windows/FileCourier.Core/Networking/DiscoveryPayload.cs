using System.Text.Json.Serialization;

namespace FileCourier.Core.Networking;

/// <summary>
/// UDP broadcast payload (Schema v1 per architecture.md).
/// Sent every 3 seconds as a heartbeat. IsGoodbye=true on graceful shutdown.
/// </summary>
public class DiscoveryPayload
{
    public int SchemaVersion { get; set; } = 1;
    public Guid DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string OS { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public bool IsGoodbye { get; set; } = false;
}
