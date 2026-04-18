using CommunityToolkit.Mvvm.ComponentModel;

namespace FileCourier.Core.Models;

public enum FileStatus { Added, Transferred, Failed, Canceled }

public partial class FileItem : ObservableObject
{
    public string AbsolutePath { get; }
    public string RelativePath { get; }
    public bool IsFolder { get; }

    [ObservableProperty]
    private FileStatus _status = FileStatus.Added;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public FileItem(string absolutePath, string relativePath, bool isFolder = false)
    {
        AbsolutePath = absolutePath;
        RelativePath = relativePath;
        IsFolder = isFolder;
    }
}
