using System.IO;
using System.Linq;
using System.Text;

namespace AustinXPowerBot.Desktop.Services;

public sealed class DiagnosticsService
{
    public async Task<string> ExportAsync(AppStateService appState, string logFilePath, CancellationToken cancellationToken = default)
    {
        var exportFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "AustinXPowerBot",
            "Diagnostics");

        Directory.CreateDirectory(exportFolder);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        var exportPath = Path.Combine(exportFolder, $"diagnostics_{timestamp}.txt");

        var builder = new StringBuilder();
        builder.AppendLine("AustinXPowerBot Diagnostics");
        builder.AppendLine($"GeneratedUtc: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"MachineName: {Environment.MachineName}");
        builder.AppendLine($"OSVersion: {Environment.OSVersion}");
        builder.AppendLine();
        builder.AppendLine("App State");
        builder.AppendLine($"CurrentUser: {appState.CurrentUser ?? "(none)"}");
        builder.AppendLine($"LicenseStatus: {appState.LicenseStatus.Status}");
        builder.AppendLine($"LicenseMessage: {appState.LicenseStatus.Message}");
        builder.AppendLine($"DeviceModel: {appState.DeviceInfo.DeviceModel}");
        builder.AppendLine($"DeviceDisplayId: {appState.DeviceInfo.DeviceDisplayId}");
        builder.AppendLine($"TelegramStatus: {appState.TelegramStatusText}");
        builder.AppendLine($"BrokerStatus: {appState.BrokerStatusText}");
        builder.AppendLine($"UnreadNotifications: {appState.UnreadNotificationCount}");
        builder.AppendLine();

        if (File.Exists(logFilePath))
        {
            builder.AppendLine("Recent Logs");
            var lines = await File.ReadAllLinesAsync(logFilePath, cancellationToken);
            foreach (var line in lines.TakeLast(200))
            {
                builder.AppendLine(line);
            }
        }
        else
        {
            builder.AppendLine("Recent Logs");
            builder.AppendLine("(log file not found)");
        }

        await File.WriteAllTextAsync(exportPath, builder.ToString(), cancellationToken);
        return exportPath;
    }
}
