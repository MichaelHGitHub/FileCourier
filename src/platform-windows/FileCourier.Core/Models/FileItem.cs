namespace FileCourier.Core.Models;

/// <summary>
/// Represents a file or folder selected for sending.
/// </summary>
public record FileItem(string AbsolutePath, string RelativePath, bool IsFolder = false);
