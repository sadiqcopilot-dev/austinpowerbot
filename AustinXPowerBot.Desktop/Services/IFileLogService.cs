namespace AustinXPowerBot.Desktop.Services;

public interface IFileLogService
{
    Task LogAsync(string message, CancellationToken cancellationToken = default);
    string LogFilePath { get; }
}
