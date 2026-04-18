using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using FileCourier.Core.Networking;
using FileCourier.Core.Storage;
using FileCourier.Core.ViewModels;

namespace FileCourier.WinUI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;

    public static IntPtr GetWindowHandle() =>
        WinRT.Interop.WindowNative.GetWindowHandle(MainWindow);

    public App()
    {
        this.InitializeComponent();
        Services = BuildServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Start background services
        Services.GetRequiredService<UdpDiscoveryService>().Start();
        Services.GetRequiredService<TcpTransferService>().Start();

        MainWindow = new MainWindow();
        MainWindow.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // --- Settings (load first; other services depend on them) ---
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileCourier");
        Directory.CreateDirectory(appDataDir);

        var settingsStore = new SettingsStore(Path.Combine(appDataDir, "settings.json"));
        settingsStore.Save(); // Ensure initial DeviceId is persisted
        var s = settingsStore.Settings;

        services.AddSingleton(settingsStore);

        // --- Storage ---
        services.AddSingleton(new TrustStore(Path.Combine(appDataDir, "trust.db")));
        services.AddSingleton(new TransferHistoryStore(Path.Combine(appDataDir, "history.db")));

        // --- Networking ---
        services.AddSingleton(new UdpDiscoveryService(
            s.DeviceId, s.DeviceName,
            os: "Windows",
            tcpPort: s.TcpPort,
            udpPort: s.UdpPort));

        services.AddSingleton(new TcpTransferService(s.TcpPort));

        // --- ViewModels ---
        services.AddSingleton<DeviceListViewModel>();
        services.AddTransient<SenderViewModel>();
        services.AddSingleton<ReceiverViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }
}
