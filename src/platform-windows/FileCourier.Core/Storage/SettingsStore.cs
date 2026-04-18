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
    public static readonly string DefaultDownloadPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FileCourierDownload");
    public string DefaultSavePath { get; set; } = DefaultDownloadPath;
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
        AppSettings settings;
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                settings = new AppSettings();
            }
        }
        catch 
        { 
            settings = new AppSettings();
        }

        // Migration: If the save path is the old default, update it to the new one
        var oldDefault = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "FileCourier");
        if (settings.DefaultSavePath == oldDefault)
        {
            settings.DefaultSavePath = AppSettings.DefaultDownloadPath;
        }

        return settings;
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
