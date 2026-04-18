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
        var settingsStore = App.Services.GetRequiredService<SettingsStore>();
        _senderVm = new SenderViewModel(tcp, settingsStore);

        DeviceListView.ItemsSource = _deviceListVm.OnlineDevices;

        // React to sender state changes
        _senderVm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SenderViewModel.State))
                dispatcherQueue.TryEnqueue(UpdateProgressOverlay);
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

    private void UpdateSelectionDisplay()
    {
        _senderVm.SelectedFiles = _selectedItems.ToList(); // Update VM
        SelectedFilesDataGrid.ItemsSource = null;
        SelectedFilesDataGrid.ItemsSource = _selectedItems;
        SelectedFilesCountText.Text = _senderVm.FileCountDisplay;
        UpdateSendButtonState();
    }

    public static string GetFileIcon(bool isFolder) => isFolder ? "\uE8B7" : "\uE7C3"; // Folder / Document

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var hasText = !string.IsNullOrWhiteSpace(TextPayloadBox.Text);
        var hasFiles = _selectedItems.Count > 0;

        _senderVm.TextPayload = hasText ? TextPayloadBox.Text : null;
        _senderVm.IsEncrypted = EncryptToggle.IsOn;

        if (hasText && hasFiles)
        {
            // Requirement: "when there is context... don't send the files"
            // We temporarily clear the files in the VM for this specific send
            var originalFiles = _senderVm.SelectedFiles;
            _senderVm.SelectedFiles = new List<FileItem>();
            
            await _senderVm.SendCommand.ExecuteAsync(null);
            
            // If success, clear the local list too
            if (_senderVm.State == SenderState.Completed)
            {
                _selectedItems.Clear();
                UpdateSelectionDisplay();
                TextPayloadBox.Text = string.Empty;
            }
            else
            {
                // Restore files if it failed or was cancelled? 
                // Actually, let's just keep them in the UI but the VM was cleared.
                _senderVm.SelectedFiles = originalFiles;
            }
        }
        else
        {
            await _senderVm.SendCommand.ExecuteAsync(null);
            
            if (_senderVm.State == SenderState.Completed)
            {
                if (hasText) TextPayloadBox.Text = string.Empty;
                if (hasFiles)
                {
                    _selectedItems.Clear();
                    UpdateSelectionDisplay();
                }
            }
        }
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
        SendButton.IsEnabled = _senderVm.TargetDevice is not null
            && (_selectedItems.Count > 0 || !string.IsNullOrWhiteSpace(TextPayloadBox.Text));
    }

    private void UpdateProgressOverlay()
    {
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
            _ = ShowErrorAsync(_senderVm.ErrorMessage);
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Transfer Failed",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
        _senderVm.ResetCommand.Execute(null);
    }
}
