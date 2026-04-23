using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using FileCourier.Core.Storage;
using FileCourier.Core.ViewModels;

namespace FileCourier.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        this.InitializeComponent();

        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();

        BandwidthSlider.ValueChanged += (_, _) => UpdateBandwidthLabel();
        UpdateBandwidthLabel();
    }

    private void UpdateBandwidthLabel()
    {
        BandwidthLabel.Text = BandwidthSlider.Value <= 0
            ? "Unlimited"
            : $"{BandwidthSlider.Value:F0} MB/s";
    }

    private async void BrowseSavePath_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = App.GetWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            ViewModel.DefaultSavePath = folder.Path;
    }

    private void RevokeTrust_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid deviceId)
        {
            ViewModel.RevokeTrustCommand.Execute(deviceId);
        }
    }

    public static string FormatDate(DateTime dt) => dt.ToLocalTime().ToString("g");
}
