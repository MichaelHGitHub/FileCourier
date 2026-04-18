using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using FileCourier.Core.Models;
using FileCourier.Core.Storage;
using FileCourier.Core.ViewModels;

namespace FileCourier.WinUI.Pages;

public sealed partial class HistoryPage : Page
{
    private readonly HistoryViewModel _vm;

    public HistoryPage()
    {
        this.InitializeComponent();
        _vm = new HistoryViewModel(App.Services.GetRequiredService<TransferHistoryStore>());
        HistoryListView.ItemsSource = _vm.Records;
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
}
