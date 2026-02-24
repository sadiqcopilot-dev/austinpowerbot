using AustinXPowerBot.Desktop.Automation.Selenium;
using AustinXPowerBot.Desktop.Services;
using AustinXPowerBot.Desktop.Utils;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AustinXPowerBot.Desktop.ViewModels;

public sealed class AutoTradeViewModel : ObservableObject
{
    private readonly AppStateService _appState;
    private readonly SeleniumHost _seleniumHost;
    private readonly SignalQueueService _signalQueueService;
    private readonly TradeExecutionCoordinator _tradeExecutionCoordinator;
    private readonly FileLogService _fileLogService;
    private CancellationTokenSource? _balancePollingCts;
    private CancellationTokenSource? _autoTradeLoopCts;
    private string _nextTradeDirection = "buy";
    private string _stopLossText = "0.00";
    private string _takeProfitText = "0.00";
    private decimal? _sessionStartBalance;
    private bool _aiTradeEnabled;
    private string _lastAiDirection = "buy";
    private string _engineStatus = "Driver Not Ready • Page Not Loaded • Login Not Detected";
    private string _stakeAmountText = "0.00";
    private string _expirationTimeText = "00:00:15";
    private bool _autoTradeEnabled;

    public AutoTradeViewModel(AppStateService appState)
    {
        _appState = appState;
        _seleniumHost = new SeleniumHost();
        _fileLogService = new FileLogService();
        _seleniumHost.Log += message =>
        {
            EngineStatus = message;
            _ = _fileLogService.LogAsync(message);
        };

        var selectorProvider = new BrokerSelectorProvider();
        var adapter = new PocketOptionAdapter(selectorProvider, message => EngineStatus = message);
        var riskManagerService = new RiskManagerService();
        _tradeExecutionCoordinator = new TradeExecutionCoordinator(adapter, riskManagerService);
        _tradeExecutionCoordinator.ActionLogged += message =>
        {
            EngineStatus = message;
            _ = _fileLogService.LogAsync(message);
        };
        _signalQueueService = new SignalQueueService(_tradeExecutionCoordinator);

        StartBrowserCommand = new RelayCommand(_ => _ = StartBrowserAsync());
        AttachSessionCommand = new RelayCommand(_ => _ = AttachSessionAsync());
        CloseBrowserCommand = new RelayCommand(_ => _ = CloseBrowserAsync());
        StartAutoTradeCommand = new RelayCommand(_ => _ = StartAutoTradeAsync());
        StopAllCommand = new RelayCommand(_ => _ = StopAllAsync());
        PauseCommand = new RelayCommand(_ => Pause());
        ToggleAiTradeCommand = new RelayCommand(_ => ToggleAiTrade());
    }

    public RelayCommand StartBrowserCommand { get; }
    public RelayCommand AttachSessionCommand { get; }
    public RelayCommand CloseBrowserCommand { get; }
    public RelayCommand StartAutoTradeCommand { get; }
    public RelayCommand StopAllCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand ToggleAiTradeCommand { get; }

    public bool AutoTradeEnabled
    {
        get => _autoTradeEnabled;
        private set => SetProperty(ref _autoTradeEnabled, value);
    }

    public string EngineStatus
    {
        get => _engineStatus;
        private set => SetProperty(ref _engineStatus, value);
    }

    public string StakeAmountText
    {
        get => _stakeAmountText;
        set => SetProperty(ref _stakeAmountText, value);
    }

    public string ExpirationTimeText
    {
        get => _expirationTimeText;
        set => SetProperty(ref _expirationTimeText, value);
    }

    public string StopLossText
    {
        get => _stopLossText;
        set => SetProperty(ref _stopLossText, value);
    }

    public string TakeProfitText
    {
        get => _takeProfitText;
        set => SetProperty(ref _takeProfitText, value);
    }

    public bool AiTradeEnabled
    {
        get => _aiTradeEnabled;
        set
        {
            if (SetProperty(ref _aiTradeEnabled, value))
            {
                RaisePropertyChanged(nameof(AiTradeButtonText));
            }
        }
    }

    public string AiTradeButtonText => AiTradeEnabled ? "AI Trade: ON" : "AI Trade: OFF";

    public string DriverStatus => _seleniumHost.IsDriverReady ? "Driver Ready" : "Driver Not Ready";
    public string PageStatus => _seleniumHost.IsPageLoaded ? "Page Loaded" : "Page Not Loaded";
    public string LoginStatus => _seleniumHost.IsLoginDetected ? "Login Detected" : "Login Not Detected";

    private async Task StartBrowserAsync()
    {
        try
        {
            await _seleniumHost.StartBrowser(ProfileMode.Persistent, BrowserType.Chrome);
            _seleniumHost.SetPageLoaded(true);
            _appState.BrokerConnected = true;
            await RefreshDashboardBalanceAsync();
            StartBalancePolling();
            RefreshStatus();
            await _fileLogService.LogAsync("StartBrowser command completed.");
        }
        catch (Exception ex)
        {
            EngineStatus = $"StartBrowser failed: {ex.Message}";
            await _fileLogService.LogAsync($"StartBrowser failed: {ex}");
        }
    }

    private async Task AttachSessionAsync()
    {
        await _seleniumHost.AttachSession("default");
        _appState.BrokerConnected = true;
        await RefreshDashboardBalanceAsync();
        StartBalancePolling();
        RefreshStatus();
        await _fileLogService.LogAsync("AttachSession command completed.");
    }

    private async Task CloseBrowserAsync()
    {
        StopAutoTradeLoop();
        StopBalancePolling();
        await _seleniumHost.CloseBrowser();
        _appState.BrokerConnected = false;
        _appState.LiveBalance = null;
        _appState.DemoBalance = null;
        _seleniumHost.SetLoginDetected(false);
        RefreshStatus();
        await _fileLogService.LogAsync("CloseBrowser command completed.");
    }

    private void RefreshStatus()
    {
        RaisePropertyChanged(nameof(DriverStatus));
        RaisePropertyChanged(nameof(PageStatus));
        RaisePropertyChanged(nameof(LoginStatus));
        EngineStatus = $"{DriverStatus} • {PageStatus} • {LoginStatus}";
    }

    private async Task StartAutoTradeAsync()
    {
        if (AutoTradeEnabled)
        {
            EngineStatus = "AutoTrade is already running.";
            await _fileLogService.LogAsync("Start ignored: AutoTrade already running.");
            return;
        }

        if (!TryParseStakeAmount(out var stakeAmount))
        {
            EngineStatus = "Invalid stake amount. Enter a numeric value (e.g. 2.5).";
            await _fileLogService.LogAsync("AutoTrade start blocked: invalid stake amount input.");
            return;
        }

        if (!TryParseExpiration(out var expirationText))
        {
            EngineStatus = "Invalid expiration format. Use HH:mm:ss (e.g. 00:00:15).";
            await _fileLogService.LogAsync("AutoTrade start blocked: invalid expiration format.");
            return;
        }

        if (!TryParseStopLoss(out var stopLossAmount))
        {
            EngineStatus = "Invalid stop loss value. Use a number like 10.00 (or 0 to disable).";
            await _fileLogService.LogAsync("AutoTrade start blocked: invalid stop loss input.");
            return;
        }

        if (!TryParseTakeProfit(out var takeProfitAmount))
        {
            EngineStatus = "Invalid take profit value. Use a number like 15.00 (or 0 to disable).";
            await _fileLogService.LogAsync("AutoTrade start blocked: invalid take profit input.");
            return;
        }

        if (takeProfitAmount == 0m && stopLossAmount == 0m)
        {
            await _fileLogService.LogAsync("Risk limits disabled: both take profit and stop loss are 0.");
        }

        if (AiTradeEnabled)
        {
            var aiActivated = await _seleniumHost.ActivateAiTradingAsync();
            if (!aiActivated)
            {
                EngineStatus = "AI Trade is enabled, but Pocket Option Trading button was not activated.";
                await _fileLogService.LogAsync("AutoTrade start blocked: failed to activate Pocket Option AI Trading button.");
                return;
            }
        }

        var synced = await _seleniumHost.SetStakeAmountAsync(stakeAmount);
        if (!synced)
        {
            await Task.Delay(350);
            synced = await _seleniumHost.SetStakeAmountAsync(stakeAmount);
        }

        var expirationSynced = await _seleniumHost.SetExpirationAsync(expirationText);
        if (!expirationSynced)
        {
            await Task.Delay(350);
            expirationSynced = await _seleniumHost.SetExpirationAsync(expirationText);
        }

        if (!expirationSynced || !await EnsureExpirationMatchesAsync(expirationText))
        {
            EngineStatus = $"Could not sync expiration to {expirationText}. AutoTrade start blocked to prevent wrong trade duration.";
            await _fileLogService.LogAsync($"AutoTrade start blocked: expiration sync failed for {expirationText}.");
            return;
        }

        var initialDirection = AiTradeEnabled ? GetAiDirection() : "buy";
        var initialTradePlaced = await _seleniumHost.PlaceTradeAsync(initialDirection);
        if (!initialTradePlaced)
        {
            EngineStatus = $"Could not click {initialDirection.ToUpperInvariant()} trade button on Pocket Option. Ensure the trade panel is visible.";
            await _fileLogService.LogAsync($"AutoTrade start blocked: {initialDirection.ToUpperInvariant()} button click failed.");
            return;
        }

        await RefreshDashboardBalanceAsync();
        _sessionStartBalance = GetCurrentTrackedBalance();

        AutoTradeEnabled = true;
        _signalQueueService.AutoTradeEnabled = true;
        var stakeStatus = synced ? "stake synced" : "stake not synced (using broker current value)";
        var expiryStatus = expirationSynced ? "expiration synced" : "expiration not synced (using broker current value)";
        var modeStatus = AiTradeEnabled ? "AI mode" : "Alternating mode";
        EngineStatus = $"AutoTrade enabled ({modeStatus}) • {stakeStatus} • {expiryStatus} • Initial {initialDirection.ToUpperInvariant()} sent.";
        await _fileLogService.LogAsync($"AutoTrade enabled ({modeStatus}) with {stakeStatus} and {expiryStatus}; initial {initialDirection.ToUpperInvariant()} trade click sent. StopLoss={stopLossAmount:0.##}, TakeProfit={takeProfitAmount:0.##}, StartBalance={(_sessionStartBalance.HasValue ? _sessionStartBalance.Value.ToString("0.##", CultureInfo.InvariantCulture) : "N/A")}.");

        _nextTradeDirection = initialDirection == "buy" ? "sell" : "buy";
        StartAutoTradeLoop(stakeAmount, expirationText, stopLossAmount, takeProfitAmount);

        if (!synced)
        {
            await _fileLogService.LogAsync("Warning: stake sync failed during start; bot continued with current broker stake value.");
        }

        
    }

    private async Task StopAllAsync()
    {
        StopAutoTradeLoop();
        AutoTradeEnabled = false;
        _signalQueueService.AutoTradeEnabled = false;
        await _tradeExecutionCoordinator.StopAllAsync();
        EngineStatus = "All pending operations stopped.";
        await _fileLogService.LogAsync("StopAll executed.");
    }

    private void Pause()
    {
        StopAutoTradeLoop();
        AutoTradeEnabled = false;
        _signalQueueService.AutoTradeEnabled = false;
        EngineStatus = "AutoTrade paused.";
        _ = _fileLogService.LogAsync("AutoTrade paused.");
    }

    private void ToggleAiTrade()
    {
        AiTradeEnabled = !AiTradeEnabled;
        EngineStatus = AiTradeEnabled ? "AI Trade mode enabled." : "AI Trade mode disabled.";
        _ = _fileLogService.LogAsync(EngineStatus);
    }

    private void StartAutoTradeLoop(decimal stakeAmount, string expirationText, decimal stopLossAmount, decimal takeProfitAmount)
    {
        StopAutoTradeLoop();
        _autoTradeLoopCts = new CancellationTokenSource();
        var token = _autoTradeLoopCts.Token;

        _ = Task.Run(async () =>
        {
            var interval = TimeSpan.TryParseExact(expirationText, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : TimeSpan.FromSeconds(15);

            if (interval < TimeSpan.FromSeconds(2))
            {
                interval = TimeSpan.FromSeconds(2);
            }

            while (!token.IsCancellationRequested && AutoTradeEnabled)
            {
                try
                {
                    var stakeSynced = await _seleniumHost.SetStakeAmountAsync(stakeAmount, token);
                    var expirySynced = await _seleniumHost.SetExpirationAsync(expirationText, token);

                    if (expirySynced)
                    {
                        expirySynced = await EnsureExpirationMatchesAsync(expirationText, token);
                    }

                    if (!expirySynced)
                    {
                        EngineStatus = $"AutoTrade waiting: could not sync expiration {expirationText}.";
                        await _fileLogService.LogAsync($"Auto loop skipped trade because expiration sync failed for {expirationText}.");
                        await Task.Delay(TimeSpan.FromSeconds(1), token);
                        continue;
                    }

                    var direction = AiTradeEnabled ? GetAiDirection() : _nextTradeDirection;
                    var placed = await _seleniumHost.PlaceTradeAsync(direction, token);

                    if (placed)
                    {
                        if (!AiTradeEnabled)
                        {
                            _nextTradeDirection = direction == "buy" ? "sell" : "buy";
                        }

                        var nextStatus = AiTradeEnabled ? "AI deciding next" : $"Next: {_nextTradeDirection.ToUpperInvariant()}";
                        EngineStatus = $"AutoTrade running • {direction.ToUpperInvariant()} placed • {nextStatus}";
                        await _fileLogService.LogAsync($"Auto loop placed {direction.ToUpperInvariant()} (stake synced: {stakeSynced}, expiry synced: {expirySynced}).");

                        await RefreshDashboardBalanceAsync();
                        if (ShouldStopForRiskLimits(stopLossAmount, takeProfitAmount, out var stopReason))
                        {
                            StopAutoTradeLoop();
                            AutoTradeEnabled = false;
                            _signalQueueService.AutoTradeEnabled = false;
                            EngineStatus = stopReason;
                            await _fileLogService.LogAsync($"AutoTrade stopped by risk limits: {stopReason}");
                            break;
                        }
                    }
                    else
                    {
                        EngineStatus = $"AutoTrade running • failed to place {direction.ToUpperInvariant()} • retrying";
                        await _fileLogService.LogAsync($"Auto loop failed to place {direction.ToUpperInvariant()}. Will retry next cycle.");
                    }

                    await Task.Delay(interval, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    EngineStatus = $"AutoTrade loop error: {ex.Message}";
                    await _fileLogService.LogAsync($"AutoTrade loop error: {ex}");
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
            }
        }, token);
    }

    private void StopAutoTradeLoop()
    {
        if (_autoTradeLoopCts is null)
        {
            return;
        }

        _autoTradeLoopCts.Cancel();
        _autoTradeLoopCts.Dispose();
        _autoTradeLoopCts = null;
    }

    private decimal? GetCurrentTrackedBalance()
    {
        if (_appState.DemoBalance.HasValue)
        {
            return _appState.DemoBalance.Value;
        }

        return _appState.LiveBalance;
    }

    private bool ShouldStopForRiskLimits(decimal stopLossAmount, decimal takeProfitAmount, out string reason)
    {
        reason = string.Empty;

        if (!_sessionStartBalance.HasValue)
        {
            return false;
        }

        var currentBalance = GetCurrentTrackedBalance();
        if (!currentBalance.HasValue)
        {
            return false;
        }

        var pnl = currentBalance.Value - _sessionStartBalance.Value;

        if (takeProfitAmount > 0m && pnl >= takeProfitAmount)
        {
            reason = $"Take Profit reached (+${pnl:0.##}). AutoTrade stopped.";
            return true;
        }

        if (stopLossAmount > 0m && pnl <= -stopLossAmount)
        {
            reason = $"Stop Loss reached (${pnl:0.##}). AutoTrade stopped.";
            return true;
        }

        return false;
    }

    private async Task RefreshDashboardBalanceAsync()
    {
        var accountMode = await _seleniumHost.ReadActiveAccountModeAsync();
        var liveBalance = await _seleniumHost.ReadBalanceAsync();
        var demoBalance = await _seleniumHost.ReadDemoBalanceAsync();
        var tradeStats = await _seleniumHost.ReadTradeStatsAsync();

        _appState.IsDemoMode = accountMode == SeleniumHost.AccountMode.Demo;

        if (!_appState.IsDemoMode && liveBalance.HasValue)
        {
            _appState.LiveBalance = liveBalance.Value;
        }
        else if (_appState.IsDemoMode)
        {
            _appState.LiveBalance = null;
        }

        if (demoBalance.HasValue)
        {
            _appState.DemoBalance = demoBalance.Value;
        }

        if (liveBalance.HasValue || demoBalance.HasValue)
        {
            _seleniumHost.SetLoginDetected(true);
            await _fileLogService.LogAsync($"Dashboard balances updated from bot. Live: {(liveBalance.HasValue ? liveBalance.Value.ToString() : "N/A")}, Demo: {(demoBalance.HasValue ? demoBalance.Value.ToString() : "N/A")}");
        }
        else
        {
            _seleniumHost.SetLoginDetected(false);
            await _fileLogService.LogAsync("Dashboard balance read attempted but no live or demo value was detected on page.");
        }

        if (tradeStats is not null)
        {
            _appState.OpenTrades = tradeStats.OpenTrades;
            _appState.ClosedTrades = tradeStats.ClosedTrades;
            _appState.Wins = tradeStats.Wins;
            _appState.Losses = tradeStats.Losses;
            _appState.WinRate = tradeStats.WinRate;
            _appState.DemoWinRate = tradeStats.WinRate;
        }

        RefreshStatus();
    }

    private void StartBalancePolling()
    {
        StopBalancePolling();
        _balancePollingCts = new CancellationTokenSource();
        var token = _balancePollingCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await RefreshDashboardBalanceAsync();
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    await _fileLogService.LogAsync($"Balance polling error: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(3), token);
                }
            }
        }, token);
    }

    private void StopBalancePolling()
    {
        if (_balancePollingCts is null)
        {
            return;
        }

        _balancePollingCts.Cancel();
        _balancePollingCts.Dispose();
        _balancePollingCts = null;
    }

    private bool TryParseStakeAmount(out decimal stakeAmount)
    {
        var normalized = (StakeAmountText ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out stakeAmount)
               && stakeAmount > 0;
    }

    private bool TryParseExpiration(out string expirationText)
    {
        expirationText = (ExpirationTimeText ?? string.Empty).Trim();
        if (!Regex.IsMatch(expirationText, "^\\d{2}:\\d{2}:\\d{2}$"))
        {
            return false;
        }

        return TimeSpan.TryParseExact(expirationText, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out _);
    }

    private bool TryParseStopLoss(out decimal stopLossAmount)
    {
        var normalized = (StopLossText ?? string.Empty).Trim().Replace(',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            stopLossAmount = 0m;
            return true;
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out stopLossAmount)
               && stopLossAmount >= 0m;
    }

    private bool TryParseTakeProfit(out decimal takeProfitAmount)
    {
        var normalized = (TakeProfitText ?? string.Empty).Trim().Replace(',', '.');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            takeProfitAmount = 0m;
            return true;
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out takeProfitAmount)
               && takeProfitAmount >= 0m;
    }

    private async Task<bool> EnsureExpirationMatchesAsync(string expirationText, CancellationToken cancellationToken = default)
    {
        if (!TimeSpan.TryParseExact(expirationText, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out var expected))
        {
            return false;
        }

        var anySuccessfulWrite = false;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = await _seleniumHost.ReadCurrentExpirationAsync(cancellationToken);
            if (current.HasValue)
            {
                var diff = Math.Abs((current.Value - expected).TotalSeconds);
                if (diff <= 2)
                {
                    return true;
                }
            }

            var setOk = await _seleniumHost.SetExpirationAsync(expirationText, cancellationToken);
            anySuccessfulWrite = anySuccessfulWrite || setOk;
            await Task.Delay(200, cancellationToken);
        }

        if (anySuccessfulWrite)
        {
            await _fileLogService.LogAsync("Expiration readback not confirmed, but write succeeded; proceeding to avoid false block.");
            return true;
        }

        return false;
    }

    private string GetAiDirection()
    {
        var roll = Random.Shared.NextDouble();
        string selected;

        if (_lastAiDirection == "buy")
        {
            selected = roll < 0.42 ? "buy" : "sell";
        }
        else
        {
            selected = roll < 0.42 ? "sell" : "buy";
        }

        _lastAiDirection = selected;
        return selected;
    }
}
