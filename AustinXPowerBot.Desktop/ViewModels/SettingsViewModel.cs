using System;
using System.Threading.Tasks;
using AustinXPowerBot.Desktop.Services;
using AustinXPowerBot.Desktop.Utils;

namespace AustinXPowerBot.Desktop.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const string StorageKey = "app-settings";
    private readonly LocalStorageService _storage = new();

    private bool _autoUpdateEnabled;
    private string _updateManifestUrl = string.Empty;
    private bool _startOnLogin;
    private string _theme = "Auto";
    private string _claimGenerationMode = "OneTime";
    private int _claimExpirationMinutes = 10;
    private string _logLevel = "Info";

    public SettingsViewModel()
    {
        _ = LoadAsync();
    }

    public bool AutoUpdateEnabled
    {
        get => _autoUpdateEnabled;
        set { if (SetProperty(ref _autoUpdateEnabled, value)) _ = SaveAsync(); }
    }

    public string UpdateManifestUrl
    {
        get => _updateManifestUrl;
        set { if (SetProperty(ref _updateManifestUrl, value)) _ = SaveAsync(); }
    }

    public bool StartOnLogin
    {
        get => _startOnLogin;
        set { if (SetProperty(ref _startOnLogin, value)) _ = SaveAsync(); }
    }

    public string Theme
    {
        get => _theme;
        set { if (SetProperty(ref _theme, value)) _ = SaveAsync(); }
    }

    public string ClaimGenerationMode
    {
        get => _claimGenerationMode;
        set { if (SetProperty(ref _claimGenerationMode, value)) _ = SaveAsync(); }
    }

    public int ClaimExpirationMinutes
    {
        get => _claimExpirationMinutes;
        set { if (SetProperty(ref _claimExpirationMinutes, value)) _ = SaveAsync(); }
    }

    public string LogLevel
    {
        get => _logLevel;
        set { if (SetProperty(ref _logLevel, value)) _ = SaveAsync(); }
    }

    private async Task SaveAsync()
    {
        try
        {
            var dto = new
            {
                AutoUpdateEnabled,
                UpdateManifestUrl,
                StartOnLogin,
                Theme,
                ClaimGenerationMode,
                ClaimExpirationMinutes,
                LogLevel
            };

            await _storage.SaveAsync(StorageKey, dto);
        }
        catch { }
    }

    private async Task LoadAsync()
    {
        try
        {
            var obj = await _storage.LoadAsync<dynamic>(StorageKey);
            if (obj is null) return;

            AutoUpdateEnabled = obj.AutoUpdateEnabled ?? AutoUpdateEnabled;
            UpdateManifestUrl = obj.UpdateManifestUrl ?? UpdateManifestUrl;
            StartOnLogin = obj.StartOnLogin ?? StartOnLogin;
            Theme = obj.Theme ?? Theme;
            ClaimGenerationMode = obj.ClaimGenerationMode ?? ClaimGenerationMode;
            ClaimExpirationMinutes = obj.ClaimExpirationMinutes ?? ClaimExpirationMinutes;
            LogLevel = obj.LogLevel ?? LogLevel;
        }
        catch { }
    }
}
