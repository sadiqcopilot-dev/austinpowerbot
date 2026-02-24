using System.IO;
using System.Text;

namespace AustinXPowerBot.Desktop.Services;

public sealed class FileLogService : IFileLogService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AustinXPowerBot",
        "Logs",
        "app.log");

    public async Task LogAsync(string message, CancellationToken cancellationToken = default)
    {
        var folder = Path.GetDirectoryName(LogFilePath)!;
        Directory.CreateDirectory(folder);

        var line = $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}";

        await Gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(LogFilePath, line, Encoding.UTF8, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }
}
