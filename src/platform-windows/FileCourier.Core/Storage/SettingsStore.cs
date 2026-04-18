using System.Text.Json;

namespace FileCourier.Core.Storage;

public enum ConflictBehavior { KeepBoth, Overwrite, Skip }

/// <summary>
/// Application settings persisted to a JSON file in %AppData%\FileCourier\.
/// </summary>
public class AppSettings
{
    public string DeviceName { get; set; } = Environment.MachineName;
    public Guid DeviceId { get; set; } = Guid.NewGuid();
    public int TcpPort { get; set; } = 45455;
    public int UdpPort { get; set; } = 45454;
    public string DefaultSavePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FileCourier");
    public ConflictBehavior ConflictBehavior { get; set; } = ConflictBehavior.KeepBoth;
    /// <summary>0 = unlimited.</summary>
    public long MaxBandwidthBytesPerSecond { get; set; } = 0;
}

public sealed class SettingsStore
{
    private readonly string _filePath;
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public SettingsStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FileCourier", "settings.json");

        _settings = Load();
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { /* return defaults on any parse error */ }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(_settings);
        Save();
    }
}
