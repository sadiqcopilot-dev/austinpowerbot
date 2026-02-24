using AustinXPowerBot.Desktop.Services;
using AustinXPowerBot.Desktop.Utils;
using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Windows;
using System.ComponentModel;

namespace AustinXPowerBot.Desktop.ViewModels;

public sealed class DeviceLicenseViewModel : ObservableObject
{
    private const string StorageKey = "license-state";
    private static readonly string TelegramLinkResolutionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AustinXPowerBot",
        "TelegramBot",
        "link-resolutions.json");
    private static readonly string PendingClientActivationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AustinXPowerBot",
        "TelegramBot",
        "pending-client-activations.json");
    private static readonly string ActivationDecisionPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AustinXPowerBot",
        "TelegramBot",
        "activation-decisions.json");

    private readonly AppStateService _appState;
    private readonly LocalStorageService _localStorageService;

    private string _licenseKeyText = string.Empty;
    private string _telegramIdText = string.Empty;
    private string _clientBotUsername = "austinxfinalbot";
    private string? _boundDeviceIdHash;
    private bool _isLicenseGenerationLocked;
    private bool _isConnectingTelegram;
    private bool _isWaitingForApproval;
    private string _statusText = "Set license details and click Activate License.";
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public DeviceLicenseViewModel(AppStateService appState)
    {
        _appState = appState;
        _appState.PropertyChanged += OnAppStatePropertyChanged;
        _localStorageService = new LocalStorageService();

        ActivateLicenseCommand = new RelayCommand(_ => _ = ExecuteAsyncSafe(ActivateLicenseAsync));
        GenerateLicenseCommand = new RelayCommand(_ => ExecuteSafe(GenerateLicenseKey));
        ConnectTelegramCommand = new RelayCommand(_ => _ = ConnectTelegramSafeAsync());
        SetPendingCommand = new RelayCommand(_ => _ = ExecuteAsyncSafe(SetPendingAsync));
        ReloadStateCommand = new RelayCommand(_ => _ = ExecuteAsyncSafe(LoadStateSafeAsync));
        CopyActivationMessageCommand = new RelayCommand(_ => ExecuteSafe(CopyActivationMessage));
        OpenAdminBotCommand = new RelayCommand(_ => ExecuteSafe(OpenAdminBot));

        _ = LoadStateSafeAsync();
    }

    public RelayCommand ActivateLicenseCommand { get; }
    public RelayCommand GenerateLicenseCommand { get; }
    public RelayCommand ConnectTelegramCommand { get; }
    public RelayCommand SetPendingCommand { get; }
    public RelayCommand ReloadStateCommand { get; }
    public RelayCommand CopyActivationMessageCommand { get; }
    public RelayCommand OpenAdminBotCommand { get; }

    public string LicenseKeyText
    {
        get => _licenseKeyText;
        set => SetProperty(ref _licenseKeyText, value);
    }

    public string TelegramIdText
    {
        get => _telegramIdText;
        set => SetProperty(ref _telegramIdText, value);
    }

    public string ClientBotUsername
    {
        get => _clientBotUsername;
        set
        {
            if (SetProperty(ref _clientBotUsername, value))
            {
                RaisePropertyChanged(nameof(ActivationMessage));
            }
        }
    }

    public string ActivationMessage
    {
        get
        {
            var keyInput = (LicenseKeyText ?? string.Empty).Trim();
            var key = string.IsNullOrWhiteSpace(keyInput) ? "REQUEST_KEY" : keyInput;
            var telegram = (TelegramIdText ?? string.Empty).Trim();
            var device = _appState.DeviceInfo;
            var deviceName = string.IsNullOrWhiteSpace(device?.DeviceName) ? Environment.MachineName : device.DeviceName;
            var deviceId = string.IsNullOrWhiteSpace(device?.DeviceDisplayId) ? "N/A" : device.DeviceDisplayId;

            return $"/activate key={key} telegramId={telegram} deviceName={deviceName} deviceId={deviceId}";
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsActivateOnlyMode => _appState.LicenseStatus.Status == LicenseState.Pending;
    public bool CanUseNonActivateControls => !IsActivateOnlyMode;
    public bool CanGenerateLicense => !_isLicenseGenerationLocked;
    public bool CanConnectTelegram => !_isConnectingTelegram;

    private async Task ConnectTelegramSafeAsync()
    {
        if (_isConnectingTelegram)
        {
            return;
        }

        _isConnectingTelegram = true;
        RaisePropertyChanged(nameof(CanConnectTelegram));
        try
        {
            await ExecuteAsyncSafe(ConnectTelegramAsync);
        }
        finally
        {
            _isConnectingTelegram = false;
            RaisePropertyChanged(nameof(CanConnectTelegram));
        }
    }

    private void OnAppStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppStateService.LicenseStatus))
        {
            RaisePropertyChanged(nameof(IsActivateOnlyMode));
            RaisePropertyChanged(nameof(CanUseNonActivateControls));
            RaisePropertyChanged(nameof(CanGenerateLicense));
            RaisePropertyChanged(nameof(CanConnectTelegram));
        }
    }

    private void CopyActivationMessage()
    {
        Clipboard.SetText(ActivationMessage);
        StatusText = "Activation message copied. Send it to the client bot.";
    }

    private void GenerateLicenseKey()
    {
        if (_isLicenseGenerationLocked)
        {
            StatusText = "License generation is locked. Contact admin before generating another license.";
            return;
        }

        var deviceHash = _appState.DeviceInfo?.DeviceIdHash;
        if (string.IsNullOrWhiteSpace(deviceHash) || deviceHash == "N/A")
        {
            StatusText = "Cannot generate device-locked key because device ID is unavailable.";
            return;
        }

        var suffix = deviceHash.Length >= 6
            ? deviceHash[^6..].ToUpperInvariant()
            : deviceHash.ToUpperInvariant();
        var part1 = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var part2 = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        LicenseKeyText = $"AX-{part1}-{part2}-{suffix}";
        _boundDeviceIdHash = deviceHash;
        _isLicenseGenerationLocked = true;
        RaisePropertyChanged(nameof(CanGenerateLicense));
        StatusText = "Generated a device-locked license key for this machine.";

        _ = ExecuteAsyncSafe(SaveStateAsync);
    }

    private void OpenAdminBot()
    {
        var username = (ClientBotUsername ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(username))
        {
            StatusText = "Client bot username is required.";
            return;
        }

        var url = $"https://t.me/{Uri.EscapeDataString(username)}";
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        StatusText = $"Opened client bot @{username}. Send activation there.";
    }

    private void ExecuteSafe(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            StatusText = $"Action failed: {ex.Message}";
        }
    }

    private async Task ExecuteAsyncSafe(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StatusText = $"Action failed: {ex.Message}";
        }
    }

    private async Task ActivateLicenseAsync()
    {
        var normalizedKey = (LicenseKeyText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            StatusText = "License key is required.";
            return;
        }

        long? telegramId = null;
        var normalizedTelegram = (TelegramIdText ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(normalizedTelegram))
        {
            if (!long.TryParse(normalizedTelegram, out var parsedTelegram) || parsedTelegram <= 0)
            {
                StatusText = "Telegram ID must be a positive number.";
                return;
            }

            telegramId = parsedTelegram;
        }

        _appState.TelegramId = telegramId;
        _appState.TelegramLinked = telegramId.HasValue;

        var currentDeviceHash = _appState.DeviceInfo?.DeviceIdHash;
        if (!string.IsNullOrWhiteSpace(_boundDeviceIdHash)
            && !string.IsNullOrWhiteSpace(currentDeviceHash)
            && !string.Equals(_boundDeviceIdHash, currentDeviceHash, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "This license key is locked to a different device.";
            return;
        }

        _appState.LicenseStatus = new LicenseStatusDto(
            LicenseState.Pending,
            "Unknown",
            null,
            !string.IsNullOrWhiteSpace(currentDeviceHash),
            telegramId.HasValue,
            "Activation request submitted via client bot.");

        await SaveStateAsync();

        _ = ExecuteAsyncSafe(WaitForApprovedDecisionAsync);

        var startPayload = BuildActivationStartPayload();
        if (!string.IsNullOrWhiteSpace(startPayload) && startPayload.Length <= 64)
        {
            OpenClientBotForActivation(startPayload);
            StatusText = "Activation request sent to client bot with required info.";
            return;
        }

        var activationToken = BuildActivationToken();
        await SavePendingClientActivationAsync(activationToken);
        OpenClientBotForActivation($"act_{activationToken}");
        StatusText = "Activation request sent to client bot. It will be forwarded automatically.";
    }

    private string BuildActivationStartPayload()
    {
        var key = (LicenseKeyText ?? string.Empty).Trim().ToUpperInvariant();
        var keyCompact = new string(key.Where(char.IsLetterOrDigit).ToArray());
        var telegram = (TelegramIdText ?? string.Empty).Trim();
        var deviceDisplay = (_appState.DeviceInfo?.DeviceDisplayId ?? "N/A").Trim().ToUpperInvariant();
        var deviceCompact = new string(deviceDisplay.Where(char.IsLetterOrDigit).ToArray());
        var deviceNameRaw = (_appState.DeviceInfo?.DeviceName ?? Environment.MachineName).Trim().ToUpperInvariant();
        var deviceNameCompact = new string(deviceNameRaw.Where(char.IsLetterOrDigit).ToArray());
        if (deviceNameCompact.Length > 12)
        {
            deviceNameCompact = deviceNameCompact[..12];
        }

        if (string.IsNullOrWhiteSpace(keyCompact)
            || string.IsNullOrWhiteSpace(telegram)
            || string.IsNullOrWhiteSpace(deviceCompact)
            || string.IsNullOrWhiteSpace(deviceNameCompact))
        {
            return string.Empty;
        }

        return $"actk_{keyCompact}_t_{telegram}_d_{deviceCompact}_n_{deviceNameCompact}";
    }

    private string BuildActivationToken()
    {
        var baseId = _appState.DeviceInfo?.DeviceDisplayId;
        if (string.IsNullOrWhiteSpace(baseId) || baseId == "N/A")
        {
            baseId = Guid.NewGuid().ToString("N")[..10];
        }

        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{baseId}-{random}".ToLowerInvariant();
    }

    private void OpenClientBotForActivation(string startPayload)
    {
        var username = (ClientBotUsername ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(username))
        {
            StatusText = "Client bot username is required.";
            return;
        }

        var url = $"https://t.me/{Uri.EscapeDataString(username)}?start={Uri.EscapeDataString(startPayload)}";
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private async Task SavePendingClientActivationAsync(string token)
    {
        var folder = Path.GetDirectoryName(PendingClientActivationPath)!;
        Directory.CreateDirectory(folder);

        Dictionary<string, PendingClientActivation> map;
        if (File.Exists(PendingClientActivationPath))
        {
            await using var readStream = File.OpenRead(PendingClientActivationPath);
            map = await JsonSerializer.DeserializeAsync<Dictionary<string, PendingClientActivation>>(readStream, _jsonOptions)
                  ?? new Dictionary<string, PendingClientActivation>();
        }
        else
        {
            map = new Dictionary<string, PendingClientActivation>();
        }

        map[token] = new PendingClientActivation(
            ActivationMessage,
            DateTimeOffset.UtcNow,
            _appState.DeviceInfo?.DeviceDisplayId ?? "N/A",
            _appState.TelegramId ?? 0);

        await using var writeStream = File.Create(PendingClientActivationPath);
        await JsonSerializer.SerializeAsync(writeStream, map, _jsonOptions);
    }

    private async Task ConnectTelegramAsync()
    {
        var username = (ClientBotUsername ?? string.Empty).Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(username))
        {
            StatusText = "Client bot username is required.";
            return;
        }

        var token = BuildLinkToken();
        var url = $"https://t.me/{Uri.EscapeDataString(username)}?start=link_{Uri.EscapeDataString(token)}";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        StatusText = "Telegram opened. Complete bot start, waiting for automatic Telegram ID detection...";

        var linkedId = await WaitForLinkedTelegramIdAsync(token, TimeSpan.FromMinutes(2));
        if (!linkedId.HasValue)
        {
            StatusText = "Could not detect Telegram ID yet. Open the client bot and press Start, then click Connect Telegram again.";
            return;
        }

        TelegramIdText = linkedId.Value.ToString();
        _appState.TelegramId = linkedId.Value;
        _appState.TelegramLinked = true;
        await SaveStateAsync();
        StatusText = $"Telegram connected automatically: {linkedId.Value}.";
    }

    private string BuildLinkToken()
    {
        var baseId = _appState.DeviceInfo?.DeviceDisplayId;
        if (string.IsNullOrWhiteSpace(baseId) || baseId == "N/A")
        {
            baseId = Guid.NewGuid().ToString("N")[..10];
        }

        var random = Guid.NewGuid().ToString("N")[..8];
        return $"{baseId}-{random}".ToLowerInvariant();
    }

    private async Task<long?> WaitForLinkedTelegramIdAsync(string token, TimeSpan timeout)
    {
        var started = DateTimeOffset.UtcNow;
        while (DateTimeOffset.UtcNow - started < timeout)
        {
            try
            {
                if (File.Exists(TelegramLinkResolutionPath))
                {
                    await using var stream = File.OpenRead(TelegramLinkResolutionPath);
                    var map = await JsonSerializer.DeserializeAsync<Dictionary<string, TelegramLinkResolution>>(
                        stream,
                        _jsonOptions);

                    if (map is not null && map.TryGetValue(token, out var resolution) && resolution.TelegramId > 0)
                    {
                        return resolution.TelegramId;
                    }
                }
            }
            catch
            {
            }

            await Task.Delay(1500);
        }

        return null;
    }

    private async Task SetPendingAsync()
    {
        _appState.LicenseStatus = new LicenseStatusDto(
            LicenseState.Pending,
            "Unknown",
            null,
            false,
            false,
            "Activation pending.");
        _appState.TelegramLinked = false;
        _appState.TelegramId = null;

        await SaveStateAsync();

        StatusText = "License reset to pending.";
    }

    private async Task LoadStateAsync()
    {
        var state = await _localStorageService.LoadAsync<LicenseLocalState>(StorageKey);
        if (state is null)
        {
            return;
        }

        LicenseKeyText = state.LicenseKey ?? string.Empty;
        TelegramIdText = state.TelegramId?.ToString() ?? string.Empty;
        var savedClientBot = (state.ClientBotUsername ?? string.Empty).Trim().TrimStart('@');
        ClientBotUsername = string.IsNullOrWhiteSpace(savedClientBot)
                            || savedClientBot.Equals("AustinXPowerClientBot", StringComparison.OrdinalIgnoreCase)
            ? "austinxfinalbot"
            : savedClientBot;
        _boundDeviceIdHash = string.IsNullOrWhiteSpace(state.BoundDeviceIdHash) ? null : state.BoundDeviceIdHash;
        _isLicenseGenerationLocked = state.IsLicenseGenerationLocked;
        RaisePropertyChanged(nameof(CanGenerateLicense));
        RaisePropertyChanged(nameof(CanConnectTelegram));

        var currentDeviceHash = _appState.DeviceInfo?.DeviceIdHash;
        var boundMismatch = !string.IsNullOrWhiteSpace(_boundDeviceIdHash)
                    && !string.IsNullOrWhiteSpace(currentDeviceHash)
                    && !string.Equals(_boundDeviceIdHash, currentDeviceHash, StringComparison.OrdinalIgnoreCase);

        _appState.TelegramLinked = state.TelegramId.HasValue;
        _appState.TelegramId = state.TelegramId;
        if (boundMismatch)
        {
            _appState.LicenseStatus = new LicenseStatusDto(
                LicenseState.Pending,
                "Unknown",
                null,
                false,
                state.IsTelegramBound,
                "Saved license is locked to another device.");
            StatusText = "Saved license belongs to another device.";
            return;
        }

        _appState.LicenseStatus = new LicenseStatusDto(
            state.Status,
            string.IsNullOrWhiteSpace(state.PlanName) ? "Unknown" : state.PlanName,
            state.ValidUntilUtc,
            state.IsDeviceBound || !string.IsNullOrWhiteSpace(_boundDeviceIdHash),
            state.IsTelegramBound,
            state.Message);

        var appliedApprovedDecision = await ApplyApprovedDecisionIfExistsAsync();
        if (appliedApprovedDecision)
        {
            return;
        }

        StatusText = _appState.LicenseStatus.Status == LicenseState.Active
            ? "Loaded active local license state."
            : "Loaded pending local license state.";
    }

    private async Task WaitForApprovedDecisionAsync()
    {
        if (_isWaitingForApproval)
        {
            return;
        }

        _isWaitingForApproval = true;
        try
        {
            var started = DateTimeOffset.UtcNow;
            var timeout = TimeSpan.FromMinutes(10);
            while (DateTimeOffset.UtcNow - started < timeout)
            {
                var applied = await ApplyApprovedDecisionIfExistsAsync();
                if (applied)
                {
                    return;
                }

                await Task.Delay(3000);
            }
        }
        finally
        {
            _isWaitingForApproval = false;
        }
    }

    private async Task<bool> ApplyApprovedDecisionIfExistsAsync()
    {
        try
        {
            if (!File.Exists(ActivationDecisionPath))
            {
                return false;
            }

            await using var stream = File.OpenRead(ActivationDecisionPath);
            var decisions = await JsonSerializer.DeserializeAsync<List<ActivationDecisionRecord>>(stream, _jsonOptions);
            if (decisions is null || decisions.Count == 0)
            {
                return false;
            }

            var licenseKey = (LicenseKeyText ?? string.Empty).Trim();
            var telegramText = (TelegramIdText ?? string.Empty).Trim();
            var deviceId = (_appState.DeviceInfo?.DeviceDisplayId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(licenseKey)
                || string.IsNullOrWhiteSpace(telegramText)
                || string.IsNullOrWhiteSpace(deviceId)
                || !long.TryParse(telegramText, out var telegramId)
                || telegramId <= 0)
            {
                return false;
            }

            var normalizedLicenseKey = NormalizeAlphaNumericUpper(licenseKey);
            var normalizedDeviceId = NormalizeAlphaNumericUpper(deviceId);

            var match = decisions
                .Where(x => x.Decision.Equals("approved", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.DecidedAtUtc)
                .FirstOrDefault(x => NormalizeAlphaNumericUpper(x.LicenseKey).Equals(normalizedLicenseKey, StringComparison.Ordinal)
                                     && x.TelegramId == telegramId
                                     && NormalizeAlphaNumericUpper(x.DeviceId).Equals(normalizedDeviceId, StringComparison.Ordinal));

            if (match is null)
            {
                return false;
            }

            _appState.LicenseStatus = new LicenseStatusDto(
                LicenseState.Active,
                _appState.LicenseStatus.PlanName,
                _appState.LicenseStatus.ValidUntilUtc,
                !string.IsNullOrWhiteSpace(_boundDeviceIdHash),
                true,
                $"License approved by admin ({match.DecidedBy}).");
            _appState.TelegramLinked = true;
            _appState.TelegramId = telegramId;

            await SaveStateAsync();
            StatusText = "License approved by admin and is now active.";
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadStateSafeAsync()
    {
        try
        {
            await LoadStateAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Saved license state could not be loaded ({ex.Message}). You can re-activate license.";
        }
    }

    private Task SaveStateAsync()
    {
        var state = new LicenseLocalState(
            (LicenseKeyText ?? string.Empty).Trim(),
            _appState.TelegramId,
            (ClientBotUsername ?? string.Empty).Trim(),
            string.Empty,
            _boundDeviceIdHash,
            _appState.LicenseStatus.Status,
            _appState.LicenseStatus.PlanName,
            _appState.LicenseStatus.ValidUntilUtc,
            _appState.LicenseStatus.IsDeviceBound,
            _appState.LicenseStatus.IsTelegramBound,
            _appState.LicenseStatus.Message,
            _isLicenseGenerationLocked);

        RaisePropertyChanged(nameof(CanGenerateLicense));
        RaisePropertyChanged(nameof(CanConnectTelegram));

        return _localStorageService.SaveAsync(StorageKey, state);
    }

    private static string NormalizeAlphaNumericUpper(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    public sealed record LicenseLocalState(
        string? LicenseKey,
        long? TelegramId,
        string? ClientBotUsername,
        string? AdminBotUsername,
        string? BoundDeviceIdHash,
        LicenseState Status,
        string? PlanName,
        DateTimeOffset? ValidUntilUtc,
        bool IsDeviceBound,
        bool IsTelegramBound,
        string? Message,
        bool IsLicenseGenerationLocked);

    private sealed record TelegramLinkResolution(long TelegramId, DateTimeOffset LinkedAtUtc, string? Username);
    private sealed record PendingClientActivation(string Message, DateTimeOffset RequestedAtUtc, string DeviceDisplayId, long TelegramId);
    private sealed record ActivationDecisionRecord(string LicenseKey, long TelegramId, string DeviceId, string Decision, string RequestId, string DecidedBy, DateTimeOffset DecidedAtUtc);
}
