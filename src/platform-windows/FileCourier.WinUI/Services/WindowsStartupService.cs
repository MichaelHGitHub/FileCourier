using Microsoft.Win32;
using System.Diagnostics;
using FileCourier.Core.Services;

namespace FileCourier.WinUI.Services;

public class WindowsStartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FileCourier";

    public bool IsStartupEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public void SetStartup(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key == null) return;

        if (enable)
        {
            string path = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (!string.IsNullOrEmpty(path))
            {
                key.SetValue(AppName, $"\"{path}\"");
            }
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
