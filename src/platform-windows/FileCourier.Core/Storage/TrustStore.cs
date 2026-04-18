using Microsoft.Data.Sqlite;
using FileCourier.Core.Models;

namespace FileCourier.Core.Storage;

/// <summary>
/// Persists trusted device IDs to SQLite so "Always Agree" survives app restarts.
/// Pass dbPath=null for an in-memory database (unit tests).
/// </summary>
public sealed class TrustStore : IDisposable
{
    private readonly SqliteConnection _db;

    public TrustStore(string? dbPath = null)
    {
        var connectionString = dbPath is null
            ? "Data Source=:memory:"
            : $"Data Source={dbPath}";

        _db = new SqliteConnection(connectionString);
        _db.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS TrustedDevices (
                DeviceId    TEXT PRIMARY KEY NOT NULL,
                DeviceName  TEXT NOT NULL DEFAULT '',
                DateAdded   TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Migrations: Add new columns if they don't exist
        try { cmd.CommandText = "ALTER TABLE TrustedDevices ADD COLUMN MacAddress TEXT NOT NULL DEFAULT '';"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE TrustedDevices ADD COLUMN LastKnownIp TEXT NOT NULL DEFAULT '';"; cmd.ExecuteNonQuery(); } catch { }
    }

    public void AddTrustedDevice(Guid deviceId, string deviceName = "", string macAddress = "", string ipAddress = "")
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO TrustedDevices (DeviceId, DeviceName, MacAddress, LastKnownIp, DateAdded)
            VALUES ($id, $name, $mac, $ip, $date);
            """;
        cmd.Parameters.AddWithValue("$id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$name", deviceName);
        cmd.Parameters.AddWithValue("$mac", macAddress);
        cmd.Parameters.AddWithValue("$ip", ipAddress);
        cmd.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public void RevokeTrustedDevice(Guid deviceId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM TrustedDevices WHERE DeviceId = $id;";
        cmd.Parameters.AddWithValue("$id", deviceId.ToString());
        cmd.ExecuteNonQuery();
    }

    public bool IsDeviceTrusted(Guid deviceId, string deviceName = "", string macAddress = "")
    {
        using var cmd = _db.CreateCommand();
        // Trust if DeviceId matches OR (Name and MAC match)
        cmd.CommandText = """
            SELECT COUNT(1) FROM TrustedDevices 
            WHERE DeviceId = $id 
               OR (DeviceName = $name AND MacAddress = $mac AND $mac <> '');
            """;
        cmd.Parameters.AddWithValue("$id", deviceId.ToString());
        cmd.Parameters.AddWithValue("$name", deviceName);
        cmd.Parameters.AddWithValue("$mac", macAddress);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public IReadOnlyList<TrustedDeviceRecord> GetAll()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT DeviceId, DeviceName, MacAddress, LastKnownIp, DateAdded FROM TrustedDevices;";
        using var reader = cmd.ExecuteReader();
        var list = new List<TrustedDeviceRecord>();
        while (reader.Read())
        {
            list.Add(new TrustedDeviceRecord
            {
                TrustedDeviceId = Guid.Parse(reader.GetString(0)),
                DeviceName = reader.GetString(1),
                MacAddress = reader.GetString(2),
                LastKnownIp = reader.GetString(3),
                DateAdded = DateTime.Parse(reader.GetString(4))
            });
        }
        return list;
    }

    public void Dispose() => _db.Dispose();
}
