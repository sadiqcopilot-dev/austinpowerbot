using System;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using AustinXPowerBot.Desktop.Pages;
using AustinXPowerBot.Desktop.Services;
using AustinXPowerBot.Desktop.Utils;
using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Desktop.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private const string LicenseStorageKey = "license-state";
    private double _sidebarWidth = 240;
    private UserControl _currentPage = new DashboardView();

    public AppStateService AppState { get; }
    public bool IsAppUnlocked => AppState.LicenseStatus.Status != LicenseState.Pending;

    public double SidebarWidth
    {
        get => _sidebarWidth;
        set => SetProperty(ref _sidebarWidth, value);
    }

    public UserControl CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand NavigateCommand { get; }
    public RelayCommand CopyDeviceIdCommand { get; }
    public RelayCommand OpenNotificationsCommand { get; }

    private NotificationPollingService? _notificationPollingService;

    public ShellViewModel()
    {
        AppState = new AppStateService();
        AppState.PropertyChanged += OnAppStatePropertyChanged;
        InitializeDeviceInfo();
        _ = LoadPersistedLicenseStateAsync();

        ToggleSidebarCommand = new RelayCommand(_ =>
        {
            SidebarWidth = SidebarWidth > 100 ? 80 : 240;
        });

        OpenNotificationsCommand = new RelayCommand(_ =>
        {
            CurrentPage = CreatePageSafe("Notifications");
            AppState.MarkAllNotificationsRead();
        });

        NavigateCommand = new RelayCommand(param =>
        {
            var key = (param?.ToString() ?? "").Trim();
            if (!IsAppUnlocked && !string.Equals(key, "DeviceLicense", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            CurrentPage = CreatePageSafe(key);
        });

        CopyDeviceIdCommand = new RelayCommand(_ =>
        {
            if (!string.IsNullOrWhiteSpace(AppState.DeviceInfo.DeviceIdHash)
                && AppState.DeviceInfo.DeviceIdHash != "N/A")
            {
                Clipboard.SetText(AppState.DeviceInfo.DeviceIdHash);
            }
        });

        // Start notification polling (simple client-side polling against the API)
        try
        {
            _notificationPollingService = new NotificationPollingService("http://localhost:5000");
            _notificationPollingService.NotificationReceived += OnNotificationReceived;
            var clientId = Environment.MachineName; // choose a client identifier (can be user id later)
            _notificationPollingService.Start(clientId, TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignore startup polling errors
        }
    }

    private void OnNotificationReceived(AustinXPowerBot.Desktop.Services.ApiNotificationDto n)
    {
        try
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                var title = string.IsNullOrWhiteSpace(n.Title) ? "Notification" : n.Title;
                var popup = new AustinXPowerBot.Desktop.Views.NotificationsPopup();
                popup.SetContent(title, n.Message ?? string.Empty);
                await popup.ShowForAsync(TimeSpan.FromSeconds(6));
            });
        }
        catch
        {
        }
    }

    private void OnAppStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppStateService.LicenseStatus))
        {
            RaisePropertyChanged(nameof(IsAppUnlocked));
        }
    }

    private async Task LoadPersistedLicenseStateAsync()
    {
        try
        {
            var storage = new LocalStorageService();
            var state = await storage.LoadAsync<PersistedLicenseState>(LicenseStorageKey);
            if (state is null)
            {
                return;
            }

            var currentDeviceHash = AppState.DeviceInfo.DeviceIdHash;
            var hasBoundHash = !string.IsNullOrWhiteSpace(state.BoundDeviceIdHash);
            var deviceMismatch = hasBoundHash
                && !string.IsNullOrWhiteSpace(currentDeviceHash)
                && !string.Equals(state.BoundDeviceIdHash, currentDeviceHash, StringComparison.OrdinalIgnoreCase);

            AppState.TelegramId = state.TelegramId;
            AppState.TelegramLinked = state.TelegramId.HasValue;

            if (deviceMismatch)
            {
                AppState.LicenseStatus = new LicenseStatusDto(
                    LicenseState.Pending,
                    "Unknown",
                    null,
                    false,
                    state.IsTelegramBound,
                    "Saved license is locked to another device.");
                return;
            }

            AppState.LicenseStatus = new LicenseStatusDto(
                state.Status,
                string.IsNullOrWhiteSpace(state.PlanName) ? "Unknown" : state.PlanName,
                state.ValidUntilUtc,
                state.IsDeviceBound || hasBoundHash,
                state.IsTelegramBound,
                state.Message);
        }
        catch
        {
        }
    }

    private void InitializeDeviceInfo()
    {
        try
        {
            var fingerprintService = new DeviceFingerprintService();
            AppState.DeviceInfo = fingerprintService.GetDeviceInfo();
        }
        catch
        {
            AppState.DeviceInfo = new AustinXPowerBot.Shared.Dtos.DeviceInfoDto(
                Environment.MachineName,
                "Unknown Model",
                "N/A",
                "N/A",
                Environment.OSVersion.VersionString);
        }
    }

    private UserControl CreatePageSafe(string key)
    {
        try
        {
            return CreatePage(key);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open '{key}' page: {ex.Message}",
                "Navigation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return new DashboardView();
        }
    }

    private UserControl CreatePage(string key) => key switch
    {
        "Dashboard" => new DashboardView(),
        "AutoTrade" => new AutoTradeView
        {
            DataContext = new AutoTradeViewModel(AppState)
        },
        "Signals" => new SignalsView(),
        "Telegram" => new TelegramControlView(),
        "Risk" => new RiskManagerView(),
        "History" => new TradeHistoryView(),
        "ClaimBonus" => new ClaimBonusView
        {
            DataContext = new ClaimBonusViewModel(AppState)
        },
        "DeviceLicense" => new DeviceLicenseView
        {
            DataContext = new DeviceLicenseViewModel(AppState)
        },
        "Settings" => new SettingsView
        {
            DataContext = new SettingsViewModel()
        },
        "Help" => new HelpSupportView
        {
            DataContext = new HelpSupportViewModel(AppState)
        },
        "Notifications" => new NotificationsView(),
        _ => new DashboardView()
    };

    private sealed record PersistedLicenseState(
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
}
