namespace FileCourier.Core.Services;

public interface IStartupService
{
    bool IsStartupEnabled { get; }
    void SetStartup(bool enable);
}
