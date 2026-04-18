using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using FileCourier.Core.Models;
using FileCourier.Core.Networking;
using FileCourier.Core.ViewModels;
using FileCourier.Core.Storage;

namespace FileCourier.WinUI.Pages;

public sealed partial class DevicesPage : Page
{
    private readonly DeviceListViewModel _deviceListVm;
    private readonly SenderViewModel _senderVm;
    private readonly ReceiverViewModel _receiverVm;

    public DevicesPage()
    {
        this.InitializeComponent();

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        void DispatchToUi(Action action) => dispatcherQueue.TryEnqueue(() => action());

        var discovery = App.Services.GetRequiredService<UdpDiscoveryService>();
        var tcp = App.Services.GetRequiredService<TcpTransferService>();
        var trustStore = App.Services.GetRequiredService<TrustStore>();

        _deviceListVm = App.Services.GetRequiredService<DeviceListViewModel>();
        _deviceListVm.Dispatcher = DispatchToUi;
        _senderVm = App.Services.GetRequiredService<SenderViewModel>();
        _senderVm.Dispatcher = DispatchToUi;

        DeviceListView.ItemsSource = _deviceListVm.OnlineDevices;

        // React to sender state changes
        _senderVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SenderViewModel.State))
            {
                dispatcherQueue.TryEnqueue(UpdateProgressOverlay);
                dispatcherQueue.TryEnqueue(UpdateSendButtonState);
            }
            else if (e.PropertyName == nameof(SenderViewModel.ProgressPercent))
                dispatcherQueue.TryEnqueue(() =>
                {
                    TransferProgress.Value = _senderVm.ProgressPercent;
                    ProgressFileName.Text = _senderVm.CurrentFileName;
                    SpeedText.Text = _senderVm.SpeedDisplay;
                    EtaText.Text = _senderVm.EtaDisplay;
                });
        };

        // React to status text from device list
        _deviceListVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DeviceListViewModel.StatusMessage))
                dispatcherQueue.TryEnqueue(() => StatusText.Text = _deviceListVm.StatusMessage);
        };

        // React to receiver state changes
        _receiverVm = App.Services.GetRequiredService<ReceiverViewModel>();
        _receiverVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ReceiverViewModel.State))
                dispatcherQueue.TryEnqueue(UpdateSendButtonState);
        };

        UpdateSendButtonState();
    }

    private void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeviceListView.SelectedItem is SystemDevice device)
        {
            _senderVm.TargetDevice = device;
            UpdateSendButtonState();
        }
    }

    private void DeviceListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SystemDevice device)
        {
            DeviceListView.SelectedItem = device;
            _senderVm.TargetDevice = device;
            UpdateSendButtonState();
        }
    }

    private readonly List<FileItem> _selectedItems = new();

    private async void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = App.GetWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var files = await picker.PickMultipleFilesAsync();
        if (files is not null && files.Count > 0)
        {
            foreach (var file in files)
            {
                if (!_selectedItems.Any(i => i.AbsolutePath == file.Path))
                    _selectedItems.Add(new FileItem(file.Path, Path.GetFileName(file.Path)));
            }
            UpdateSelectionDisplay();
        }
    }

    private async void ChooseFolders_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = App.GetWindowHandle();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            // Walk folder and add all files
            AddFolderRecursively(folder.Path, Path.GetFileName(folder.Path));
            UpdateSelectionDisplay();
        }
    }

    private void AddFolderRecursively(string absolutePath, string relativePrefix)
    {
        try
        {
            foreach (var file in Directory.GetFiles(absolutePath))
            {
                var relPath = Path.Combine(relativePrefix, Path.GetFileName(file));
                if (!_selectedItems.Any(i => i.AbsolutePath == file))
                    _selectedItems.Add(new FileItem(file, relPath));
            }
            foreach (var dir in Directory.GetDirectories(absolutePath))
            {
                AddFolderRecursively(dir, Path.Combine(relativePrefix, Path.GetFileName(dir)));
            }
        }
        catch { /* skip inaccessible */ }
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FileItem item)
        {
            _selectedItems.Remove(item);
            UpdateSelectionDisplay();
        }
    }

    private async void RetryFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FileItem item)
        {
            await _senderVm.RetryFileCommand.ExecuteAsync(item);
        }
    }

    private void UpdateSelectionDisplay()
    {
        _senderVm.SelectedFiles = _selectedItems.ToList(); // Update VM
        SelectedFilesDataGrid.ItemsSource = null;
        SelectedFilesDataGrid.ItemsSource = _selectedItems;
        SelectedFilesCountText.Text = _senderVm.FileCountDisplay;
        ClearAllButton.Visibility = _selectedItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSendButtonState();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _selectedItems.Clear();
        UpdateSelectionDisplay();
    }

    public static string GetFileIcon(bool isFolder) => isFolder ? "\uE8B7" : "\uE7C3"; // Folder / Document

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var filesToTransmit = _selectedItems.Where(i => i.Status != FileStatus.Transferred).ToList();
        if (filesToTransmit.Count == 0 && string.IsNullOrWhiteSpace(TextPayloadBox.Text)) return;

        // Reset status for failed/canceled files in this batch
        foreach (var item in filesToTransmit.Where(i => i.Status is FileStatus.Failed or FileStatus.Canceled))
        {
            item.Status = FileStatus.Added;
            item.ErrorMessage = string.Empty;
        }

        _senderVm.SelectedFiles = _selectedItems.ToList(); // Sync full list
        _senderVm.TextPayload = null;
        _senderVm.IsEncrypted = false;

        // Note: SenderViewModel.SendAsync currently uses SelectedFiles. 
        // We should temporarily filter what the VM sends, or update the VM to handle filtered lists.
        var originalList = _senderVm.SelectedFiles;
        _senderVm.SelectedFiles = filesToTransmit;

        await _senderVm.SendCommand.ExecuteAsync(null);

        _senderVm.SelectedFiles = originalList; // Restore full list for UI
        UpdateSelectionDisplay();
    }

    private void SendText_Click(object sender, RoutedEventArgs e)
    {
        var hasText = !string.IsNullOrWhiteSpace(TextPayloadBox.Text);
        var targetDevice = _senderVm.TargetDevice;
        if (!hasText || targetDevice == null) return;

        var text = TextPayloadBox.Text;
        TextPayloadBox.Text = string.Empty;

        // Run text send concurrently on a background thread so it doesn't block ongoing file transfers
        _ = Task.Run(async () =>
        {
            try
            {
                var settingsStore = App.Services.GetRequiredService<SettingsStore>();
                var tcp = App.Services.GetRequiredService<TcpTransferService>();

                var header = new TransferRequestHeader
                {
                    SenderId = settingsStore.Settings.DeviceId,
                    SenderName = settingsStore.Settings.DeviceName,
                    SenderMac = NetworkUtils.GetMacAddress(),
                    TextPayload = text,
                    Files = new List<TransferFile>()
                };

                await tcp.SendAsync(targetDevice, header, new List<string>());
            }
            catch (Exception)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // If it fails, restore the text so the user doesn't lose it
                    if (string.IsNullOrEmpty(TextPayloadBox.Text))
                    {
                        TextPayloadBox.Text = text;
                    }
                });
            }
        });
    }

    private void ClearMessage_Click(object sender, RoutedEventArgs e)
    {
        TextPayloadBox.Text = string.Empty;
    }

    private void TextPayloadBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClearMessageButton.Visibility = string.IsNullOrEmpty(TextPayloadBox.Text) ? Visibility.Collapsed : Visibility.Visible;
        UpdateSendButtonState();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _senderVm.CancelCommand.Execute(null);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _deviceListVm.Refresh();
    }

    private async void ManualConnect_Click(object sender, RoutedEventArgs e)
    {
        var ipBox = new TextBox { PlaceholderText = "e.g. 192.168.1.42" };
        var portBox = new NumberBox { PlaceholderText = "Port", Value = 45455, Minimum = 1024, Maximum = 65535 };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "IP Address" });
        panel.Children.Add(ipBox);
        panel.Children.Add(new TextBlock { Text = "Port" });
        panel.Children.Add(portBox);

        var dialog = new ContentDialog
        {
            Title = "Connect Manually",
            Content = panel,
            PrimaryButtonText = "Connect",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(ipBox.Text))
        {
            _deviceListVm.AddManualPeer(ipBox.Text.Trim(), (int)portBox.Value);
        }
    }

    private void UpdateSendButtonState()
    {
        bool isReceiving = _receiverVm.State is ReceiverState.Receiving or ReceiverState.PromptingUser;
        bool isSending = _senderVm.State is SenderState.WaitingForReceiver or SenderState.Transferring;
        bool isBusy = isReceiving || isSending;

        SendButton.IsEnabled = !isBusy && _senderVm.TargetDevice is not null && _selectedItems.Count > 0;
        SendTextButton.IsEnabled = _senderVm.TargetDevice is not null && !string.IsNullOrWhiteSpace(TextPayloadBox.Text);
        
        // Lock discovery and selection controls while busy
        DeviceListView.IsEnabled = !isBusy;
        RefreshButton.IsEnabled = !isBusy;
        ManualConnectButton.IsEnabled = !isBusy;
        ChooseFilesButton.IsEnabled = !isBusy;
        ChooseFoldersButton.IsEnabled = !isBusy;
        ClearAllButton.IsEnabled = !isBusy;

        // Show banner ONLY when we are receiving (to avoid redundancy for the sender)
        ActiveTransferWarning.IsOpen = isReceiving;
    }

    private void UpdateProgressOverlay()
    {
        UpdateSendButtonState();
        var isActive = _senderVm.State is SenderState.WaitingForReceiver or SenderState.Transferring;
        var isCompleted = _senderVm.State is SenderState.Completed;
        
        ProgressOverlay.Visibility = (isActive || isCompleted) ? Visibility.Visible : Visibility.Collapsed;

        ProgressTitle.Text = _senderVm.State switch
        {
            SenderState.WaitingForReceiver => "Waiting for receiver to accept…",
            SenderState.Transferring => "Sending…",
            SenderState.Completed => "Sent successfully!",
            _ => "Sending…"
        };

        if (_senderVm.State is SenderState.Completed)
        {
            // If it was text-only (no files), we can hide immediately as requested
            bool isTextOnly = _senderVm.SelectedFiles.Count == 0;
            int delay = isTextOnly ? 0 : 1500;

            _ = Task.Delay(delay).ContinueWith(_ =>
                DispatcherQueue.TryEnqueue(() => 
                {
                    _senderVm.ResetCommand.Execute(null);
                    DeviceListView.SelectedItem = null;
                    UpdateSendButtonState();
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                }));
        }
        else if (_senderVm.State is SenderState.Failed)
        {
            ProgressTitle.Text = "Transfer Failed";
            ProgressFileName.Text = _senderVm.ErrorMessage;
            TransferProgress.Value = 0;
            
            _ = Task.Delay(3000).ContinueWith(_ =>
                DispatcherQueue.TryEnqueue(() => 
                {
                    _senderVm.ResetCommand.Execute(null);
                    UpdateSendButtonState();
                    ProgressOverlay.Visibility = Visibility.Collapsed;
                }));
        }
        else
        {
            UpdateSendButtonState();
        }
    }
}
