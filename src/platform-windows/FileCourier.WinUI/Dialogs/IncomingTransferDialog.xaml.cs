using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using FileCourier.Core.Networking;

namespace FileCourier.WinUI.Dialogs;

public sealed partial class IncomingTransferDialog : ContentDialog
{
    private readonly IncomingTransferEventArgs _transferArgs;

    public IncomingTransferDialog(IncomingTransferEventArgs args, XamlRoot xamlRoot)
    {
        this.InitializeComponent();
        XamlRoot = xamlRoot;
        _transferArgs = args;

        SenderIp.Text = args.SenderIp;

        var header = args.Header;
        bool hasFiles = header.Files.Count > 0;
        bool hasText = !string.IsNullOrEmpty(header.TextPayload);

        SenderName.Text = $"Device {header.SenderId.ToString()[..8]}…";

        // File transfer info
        if (hasFiles)
        {
            var totalSize = FormatSize(args.TotalBytes);
            FileSummaryText.Text = header.Files.Count == 1
                ? $"wants to send you {header.Files[0].FileName} ({totalSize})"
                : $"wants to send you {header.Files.Count} files ({totalSize})";
            SavePathBox.Text = args.SaveDirectory;
        }
        else
        {
            FileSummaryPanel.Visibility = Visibility.Collapsed;
        }

        // Text transfer info
        if (hasText)
        {
            TextPayloadPanel.Visibility = Visibility.Visible;
            TextContentBox.Text = header.TextPayload;

            if (!hasFiles)
            {
                // Text-only: don't need file buttons
                PrimaryButtonText = "OK";
                SecondaryButtonText = string.Empty;
            }
        }
    }

    public string SelectedSavePath => SavePathBox.Text;

    private async void ChangePath_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = App.GetWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null) SavePathBox.Text = folder.Path;
    }

    private void CopyToClipboard_Click(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(_transferArgs.Header.TextPayload ?? string.Empty);
        Clipboard.SetContent(dp);
        CopyButton.Content = "Copied ✓";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
        >= 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes} B"
    };
}
