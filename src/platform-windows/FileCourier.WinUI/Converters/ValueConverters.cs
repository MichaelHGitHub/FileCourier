using System;
using System.IO;
using Microsoft.UI.Xaml.Data;

namespace FileCourier.WinUI.Converters;

public class FileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string path)
        {
            return Path.GetFileName(path);
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string path && File.Exists(path))
        {
            var info = new FileInfo(path);
            long bytes = info.Length;
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblSze = bytes;
            while (i < suffixes.Length && bytes >= 1024)
            {
                dblSze = bytes / 1024.0;
                i++;
                bytes /= 1024;
            }
            return $"{dblSze:n1} {suffixes[i]}";
        }
        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class IconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isFolder && isFolder)
        {
            return "\uE8B7";
        }
        return "\uE8A5";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        }
        return Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        }
        return Microsoft.UI.Xaml.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FileCourier.Core.Models.TransferStatus status)
        {
            return status switch
            {
                FileCourier.Core.Models.TransferStatus.Completed => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
                FileCourier.Core.Models.TransferStatus.Failed => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                FileCourier.Core.Models.TransferStatus.Cancelled => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class FileStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FileCourier.Core.Models.FileStatus status)
        {
            return status switch
            {
                FileCourier.Core.Models.FileStatus.Added => "\uE718", // Document
                FileCourier.Core.Models.FileStatus.Transferred => "\uE930", // CheckMark
                FileCourier.Core.Models.FileStatus.Failed => "\uEA39", // Error
                FileCourier.Core.Models.FileStatus.Canceled => "\uE711", // Cancel
                _ => "\uE718"
            };
        }
        return "\uE718";
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class FileStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FileCourier.Core.Models.FileStatus status)
        {
            return status switch
            {
                FileCourier.Core.Models.FileStatus.Transferred => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Green),
                FileCourier.Core.Models.FileStatus.Failed => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red),
                FileCourier.Core.Models.FileStatus.Canceled => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray),
                _ => new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray)
            };
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class FileStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FileCourier.Core.Models.FileStatus status && parameter is string target)
        {
            return target switch
            {
                "Remove" => Microsoft.UI.Xaml.Visibility.Visible,
                "Retry" => (status == FileCourier.Core.Models.FileStatus.Failed || status == FileCourier.Core.Models.FileStatus.Canceled) 
                            ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed,
                _ => Microsoft.UI.Xaml.Visibility.Visible
            };
        }
        return Microsoft.UI.Xaml.Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class FileStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is FileCourier.Core.Models.FileStatus status)
        {
            return status switch
            {
                FileCourier.Core.Models.FileStatus.Added => "Added",
                FileCourier.Core.Models.FileStatus.Transferred => "Transferred",
                FileCourier.Core.Models.FileStatus.Failed => "Failed",
                FileCourier.Core.Models.FileStatus.Canceled => "Canceled",
                _ => "Unknown"
            };
        }
        return string.Empty;
    }
    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
