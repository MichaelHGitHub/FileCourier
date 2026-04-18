using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FileCourier.WinUI.Pages;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using FileCourier.Core.ViewModels;
using FileCourier.Core.Storage;
using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI;

namespace FileCourier.WinUI;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        this.InitializeComponent();

        // Set window properties
        Title = "FileCourier";
        ExtendsContentIntoTitleBar = true;

        // Navigate to Devices page by default
        ContentFrame.Navigate(typeof(DevicesPage));
        NavView.SelectedItem = NavView.MenuItems[0];

        // Global Receiver handling
        var receiverVm = App.Services.GetRequiredService<ReceiverViewModel>();
        var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        receiverVm.Dispatcher = action => dispatcherQueue.TryEnqueue(() => action());
        receiverVm.PropertyChanged += OnReceiverPropertyChanged;

        // Set Icon
        try
        {
            this.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "logo_256x256.ico"));
        }
        catch { /* Fallback */ }
    }

    private void OnReceiverPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReceiverViewModel.PendingTransfer))
        {
            var vm = (ReceiverViewModel)sender!;
            if (vm.PendingTransfer != null)
            {
                _ = ShowIncomingTransferDialog(vm);
            }
        }
    }

    private async Task ShowIncomingTransferDialog(ReceiverViewModel vm)
    {
        var transfer = vm.PendingTransfer;
        if (transfer == null) return;

        bool hasFiles = transfer.Header.Files.Count > 0;
        bool hasText = !string.IsNullOrEmpty(transfer.Header.TextPayload);

        var panel = new StackPanel { Spacing = 12, Width = 400 };
        panel.Children.Add(new TextBlock { 
            Text = $"From: {transfer.SenderIp}",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"]
        });

        if (hasText)
        {
            panel.Children.Add(new TextBlock { Text = "Message:", Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] });
            var textBorder = new Border {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]
            };
            textBorder.Child = new TextBlock { Text = transfer.Header.TextPayload, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            panel.Children.Add(textBorder);

            var copyBtn = new Button { 
                Content = "Copy to Clipboard", 
                HorizontalAlignment = HorizontalAlignment.Right,
                Style = (Style)Application.Current.Resources["DefaultButtonStyle"] 
            };
            copyBtn.Click += (s, e) => {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(transfer.Header.TextPayload);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                copyBtn.Content = "Copied!";
            };
            panel.Children.Add(copyBtn);
        }

        if (hasFiles)
        {
            panel.Children.Add(new TextBlock { 
                Text = $"Files: {transfer.Header.Files.Count} ({transfer.TotalBytes / 1024.0 / 1024.0:F1} MB)",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
            });
        }

        var trustStore = App.Services.GetRequiredService<TrustStore>();
        bool isTrusted = trustStore.IsDeviceTrusted(transfer.Header.SenderId);

        var dialog = new ContentDialog
        {
            Title = hasFiles ? "Incoming Transfer" : "Incoming Message",
            Content = panel,
            PrimaryButtonText = hasFiles ? "Accept" : "OK",
            SecondaryButtonText = (hasFiles && !isTrusted) ? "Always Accept" : null,
            CloseButtonText = hasFiles ? "Decline" : null,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        
        if (hasFiles)
        {
            switch (result)
            {
                case ContentDialogResult.Primary:
                    vm.AcceptOnce(transfer);
                    break;
                case ContentDialogResult.Secondary:
                    vm.AlwaysAgree(transfer);
                    break;
                default:
                    vm.Deny(transfer);
                    break;
            }
        }
        else
        {
            // Auto-accept text-only to close handshake
            vm.AcceptOnce(transfer);
        }
    }

    private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItemContainer is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            switch (tag)
            {
                case "Devices":
                    ContentFrame.Navigate(typeof(DevicesPage));
                    break;
                case "History":
                    ContentFrame.Navigate(typeof(HistoryPage));
                    break;
                case "About":
                    ContentFrame.Navigate(typeof(AboutPage));
                    break;
            }
        }
    }
}
