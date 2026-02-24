using System.Collections.ObjectModel;
using System.Linq;
using AustinXPowerBot.Desktop.Utils;
using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Desktop.Services;

public sealed class AppStateService : ObservableObject
{
    private string? _currentUser;
    private LicenseStatusDto _licenseStatus = new(LicenseState.Pending, "Unknown", null, false, false, "Awaiting activation");
    private DeviceInfoDto _deviceInfo = new(Environment.MachineName, "Unknown Model", "N/A", "N/A", Environment.OSVersion.VersionString);
    private bool _telegramLinked;
    private long? _telegramId;
    private bool _brokerConnected;
    private decimal? _liveBalance;
    private decimal? _demoBalance;
    private decimal _winRate;
    private decimal _demoWinRate;
    private int _openTrades;
    private int _closedTrades;
    private int _wins;
    private int _losses;
    private bool _isDemoMode;

    public AppStateService()
    {
        Notifications = new ObservableCollection<NotificationDto>();
    }

    public string? CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    public LicenseStatusDto LicenseStatus
    {
        get => _licenseStatus;
        set
        {
            if (SetProperty(ref _licenseStatus, value))
            {
                RaisePropertyChanged(nameof(LicenseBadgeText));
                RaisePropertyChanged(nameof(DeviceLockText));
            }
        }
    }

    public DeviceInfoDto DeviceInfo
    {
        get => _deviceInfo;
        set => SetProperty(ref _deviceInfo, value);
    }

    public bool TelegramLinked
    {
        get => _telegramLinked;
        set
        {
            if (SetProperty(ref _telegramLinked, value))
            {
                RaisePropertyChanged(nameof(TelegramStatusText));
                RaisePropertyChanged(nameof(DeviceLockText));
            }
        }
    }

    public long? TelegramId
    {
        get => _telegramId;
        set
        {
            if (SetProperty(ref _telegramId, value))
            {
                RaisePropertyChanged(nameof(TelegramStatusText));
            }
        }
    }

    public bool BrokerConnected
    {
        get => _brokerConnected;
        set
        {
            if (SetProperty(ref _brokerConnected, value))
            {
                RaisePropertyChanged(nameof(BrokerStatusText));
            }
        }
    }

    public decimal? LiveBalance
    {
        get => _liveBalance;
        set
        {
            if (SetProperty(ref _liveBalance, value))
            {
                RaisePropertyChanged(nameof(LiveBalanceText));
            }
        }
    }

    public decimal? DemoBalance
    {
        get => _demoBalance;
        set
        {
            if (SetProperty(ref _demoBalance, value))
            {
                RaisePropertyChanged(nameof(DemoBalanceText));
            }
        }
    }

    public decimal WinRate
    {
        get => _winRate;
        set
        {
            if (SetProperty(ref _winRate, value))
            {
                RaisePropertyChanged(nameof(WinRateText));
            }
        }
    }

    public decimal DemoWinRate
    {
        get => _demoWinRate;
        set
        {
            if (SetProperty(ref _demoWinRate, value))
            {
                RaisePropertyChanged(nameof(DemoWinRateText));
            }
        }
    }

    public int OpenTrades
    {
        get => _openTrades;
        set
        {
            if (SetProperty(ref _openTrades, value))
            {
                RaisePropertyChanged(nameof(OpenTradesText));
            }
        }
    }

    public int ClosedTrades
    {
        get => _closedTrades;
        set
        {
            if (SetProperty(ref _closedTrades, value))
            {
                RaisePropertyChanged(nameof(ClosedTradesText));
            }
        }
    }

    public int Wins
    {
        get => _wins;
        set
        {
            if (SetProperty(ref _wins, value))
            {
                RaisePropertyChanged(nameof(WinLossText));
            }
        }
    }

    public int Losses
    {
        get => _losses;
        set
        {
            if (SetProperty(ref _losses, value))
            {
                RaisePropertyChanged(nameof(WinLossText));
            }
        }
    }

    public bool IsDemoMode
    {
        get => _isDemoMode;
        set
        {
            if (SetProperty(ref _isDemoMode, value))
            {
                RaisePropertyChanged(nameof(LiveBalanceText));
            }
        }
    }

    public string BrokerStatusText => BrokerConnected ? "Pocket Option • Connected" : "Pocket Option • Disconnected";
    public string LiveBalanceText => IsDemoMode ? "N/A" : (LiveBalance.HasValue ? $"${LiveBalance.Value:N2}" : "N/A");
    public string DemoBalanceText => DemoBalance.HasValue ? $"${DemoBalance.Value:N2}" : "N/A";
    public string WinRateText => $"{WinRate:0.##}%";
    public string DemoWinRateText => $"{DemoWinRate:0.##}%";
    public string OpenTradesText => OpenTrades.ToString();
    public string ClosedTradesText => ClosedTrades.ToString();
    public string WinLossText => $"W:{Wins} / L:{Losses}";
    public string LicenseBadgeText => LicenseStatus.Status.ToString();
    public string TelegramStatusText => TelegramLinked && TelegramId.HasValue
        ? $"Connected • Telegram ID: {TelegramId.Value}"
        : "Disconnected";

    public string DeviceLockText => LicenseStatus.Status == LicenseState.Active
                                    && LicenseStatus.IsDeviceBound
                                    && LicenseStatus.IsTelegramBound
                                    && TelegramLinked
        ? "Device: Locked"
        : "Device: Unlocked";

    public ObservableCollection<NotificationDto> Notifications { get; }

    public int UnreadNotificationCount => Notifications.Count(x => !x.IsRead);

    public void AddNotification(NotificationDto notification)
    {
        Notifications.Insert(0, notification);
        RaisePropertyChanged(nameof(UnreadNotificationCount));
    }

    public void MarkAllNotificationsRead()
    {
        for (var index = 0; index < Notifications.Count; index++)
        {
            var item = Notifications[index];
            if (!item.IsRead)
            {
                Notifications[index] = item with { IsRead = true };
            }
        }

        RaisePropertyChanged(nameof(UnreadNotificationCount));
    }
}
