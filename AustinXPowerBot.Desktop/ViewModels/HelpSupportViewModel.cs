using System.Windows;
using AustinXPowerBot.Desktop.Services;
using AustinXPowerBot.Desktop.Utils;

namespace AustinXPowerBot.Desktop.ViewModels;

public sealed class HelpSupportViewModel : ObservableObject
{
    private readonly DiagnosticsService _diagnosticsService = new();
    private readonly FileLogService _fileLogService = new();
    private readonly AppStateService _appStateService;
    private string _status = "Use the diagnostics export for support requests.";

    public HelpSupportViewModel(AppStateService appStateService)
    {
        _appStateService = appStateService;
        ExportDiagnosticsCommand = new RelayCommand(_ => _ = ExportDiagnosticsAsync());
    }

    public RelayCommand ExportDiagnosticsCommand { get; }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private async Task ExportDiagnosticsAsync()
    {
        try
        {
            var path = await _diagnosticsService.ExportAsync(_appStateService, _fileLogService.LogFilePath);
            Status = $"Diagnostics exported: {path}";
            await _fileLogService.LogAsync($"Diagnostics exported to {path}");
        }
        catch (Exception ex)
        {
            Status = $"Diagnostics export failed: {ex.Message}";
            await _fileLogService.LogAsync($"Diagnostics export failed: {ex}");
            MessageBox.Show(Status, "Diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
