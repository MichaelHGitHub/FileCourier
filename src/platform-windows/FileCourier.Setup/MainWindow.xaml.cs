using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Forms;

namespace FileCourier.Setup;

public partial class MainWindow : Window
{
    private string _defaultPath;

    public MainWindow()
    {
        InitializeComponent();
        _defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "FileCourier");
        PathTextBox.Text = _defaultPath;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var fbd = new FolderBrowserDialog();
        if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            PathTextBox.Text = fbd.SelectedPath;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        string targetDir = PathTextBox.Text;
        
        try
        {
            InstallButton.IsEnabled = false;
            PathTextBox.IsEnabled = false;
            InstallProgressBar.Visibility = Visibility.Visible;
            InstallProgressBar.IsIndeterminate = true;
            StatusTextBlock.Text = "Installing FileCourier...";

            await Task.Run(() =>
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var resourceName = "FileCourier.Setup.Resources.FileCourier.zip";
                
                using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) 
                        throw new Exception("Internal installer error: Embedded FileCourier.zip not found.");

                    if (Directory.Exists(targetDir)) {
                        try { Directory.Delete(targetDir, true); } catch { }
                    }
                    Directory.CreateDirectory(targetDir);

                    using (var archive = new ZipArchive(stream))
                    {
                        archive.ExtractToDirectory(targetDir, true);
                    }
                }

                string exePath = Path.Combine(targetDir, "FileCourier.exe");
                
                // Start Menu
                string startMenuPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "FileCourier.lnk");
                CreateShortcut(exePath, startMenuPath, "Fast LAN File Transfer");

                // Desktop
                string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "FileCourier.lnk");
                CreateShortcut(exePath, desktopPath, "Fast LAN File Transfer");
            });

            StatusTextBlock.Text = "Installation Successful!";
            InstallProgressBar.IsIndeterminate = false;
            InstallProgressBar.Value = 100;

            System.Windows.MessageBox.Show("FileCourier has been installed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Installation Failed.";
            InstallProgressBar.Visibility = Visibility.Collapsed;
            InstallButton.IsEnabled = true;
            PathTextBox.IsEnabled = true;
            System.Windows.MessageBox.Show($"Installation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void CreateShortcut(string targetPath, string shortcutPath, string description)
    {
        Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType == null) return;
        
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = description;
        shortcut.Save();
    }
}
