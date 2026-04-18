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
