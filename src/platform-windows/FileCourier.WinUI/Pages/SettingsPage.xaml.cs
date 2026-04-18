using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using FileCourier.Core.Storage;
using FileCourier.Core.ViewModels;

namespace FileCourier.WinUI.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel _vm;

    public SettingsPage()
    {
        this.InitializeComponent();

        _vm = new SettingsViewModel(
            App.Services.GetRequiredService<SettingsStore>(),
            App.Services.GetRequiredService<TrustStore>());

        // Populate controls from ViewModel
        DeviceNameBox.Text = _vm.DeviceName;
        SavePathBox.Text = _vm.DefaultSavePath;

        var conflictIndex = (int)_vm.ConflictBehavior;
        if (conflictIndex >= 0 && conflictIndex < ConflictRadio.Items.Count)
            ((RadioButton)ConflictRadio.Items[conflictIndex]).IsChecked = true;

        BandwidthSlider.Value = _vm.MaxBandwidthBytesPerSecond / 1_000_000.0; // MB/s
        UpdateBandwidthLabel();
        BandwidthSlider.ValueChanged += (_, _) => UpdateBandwidthLabel();

        TrustedDevicesList.ItemsSource = _vm.TrustedDevices;
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
            SavePathBox.Text = folder.Path;
    }

    private void RevokeTrust_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid deviceId)
        {
            _vm.RevokeTrustCommand.Execute(deviceId);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.DeviceName = DeviceNameBox.Text;
        _vm.DefaultSavePath = SavePathBox.Text;

        // Get selected conflict behavior
        foreach (RadioButton rb in ConflictRadio.Items)
        {
            if (rb.IsChecked == true && rb.Tag is string tag)
            {
                _vm.ConflictBehavior = Enum.Parse<ConflictBehavior>(tag);
                break;
            }
        }

        _vm.MaxBandwidthBytesPerSecond = (long)(BandwidthSlider.Value * 1_000_000);
        _vm.SaveCommand.Execute(null);
    }

    public static string FormatDate(DateTime dt) => dt.ToLocalTime().ToString("g");
}
