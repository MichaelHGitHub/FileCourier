using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;
using FileCourier.Core.ViewModels;

namespace FileCourier.WinUI.Pages;

public sealed partial class HistoryPage : Page
{
    internal readonly HistoryViewModel _vm;

    public HistoryPage()
    {
        this.InitializeComponent();
        _vm = App.Services.GetRequiredService<HistoryViewModel>();
        _vm.Dispatcher = action => DispatcherQueue.TryEnqueue(() => action());
        
        _vm.ShowDialogAsync = async (title, message) =>
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        };

        this.DataContext = _vm;
        HistoryListView.ItemsSource = _vm.Records;
        ReceivedListView.ItemsSource = _vm.ReceivedRecords;

        this.Unloaded += (s, e) => _vm.Dispose();
    }

    // ── Helpers used by x:Bind in the DataTemplate ──────────────────────

    public static string DirectionGlyph(TransferDirection dir) =>
        dir == TransferDirection.Sent ? "\uE898" : "\uE896"; // UpArrow / DownArrow

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };

    public static string FormatDate(DateTime dt) => dt.ToLocalTime().ToString("g");

    public static Microsoft.UI.Xaml.Visibility ShowRetry(TransferStatus status, TransferDirection direction, bool isTransferring) =>
        (!isTransferring && status != TransferStatus.Completed && direction == TransferDirection.Sent) 
            ? Microsoft.UI.Xaml.Visibility.Visible 
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    public static string RetryButtonText(TransferStatus status, long sent, long total) =>
        (sent > 0 && sent < total) ? "Resume" : "Retry";

    private void HistoryPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm?.Refresh();
    }
}
