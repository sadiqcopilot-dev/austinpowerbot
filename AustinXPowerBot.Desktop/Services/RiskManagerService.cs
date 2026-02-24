using AustinXPowerBot.Shared.Dtos;
using AustinXPowerBot.Shared.Enums;

namespace AustinXPowerBot.Desktop.Services;

public sealed class RiskManagerService
{
    private DateTimeOffset _lastTradeTimeUtc = DateTimeOffset.MinValue;
    private int _tradesToday;
    private int _consecutiveLosses;
    private decimal _dailyLoss;

    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(5);
    public decimal MaxDailyLoss { get; set; } = 100m;
    public int MaxConsecutiveLosses { get; set; } = 5;
    public int MaxTradesPerDay { get; set; } = 100;

    public bool CanExecute(out string reason)
    {
        var now = DateTimeOffset.UtcNow;
        if (_lastTradeTimeUtc != DateTimeOffset.MinValue && now - _lastTradeTimeUtc < Cooldown)
        {
            reason = "Risk gate: cooldown active.";
            return false;
        }

        if (_dailyLoss >= MaxDailyLoss)
        {
            reason = "Risk gate: max daily loss reached.";
            return false;
        }

        if (_consecutiveLosses >= MaxConsecutiveLosses)
        {
            reason = "Risk gate: max consecutive losses reached.";
            return false;
        }

        if (_tradesToday >= MaxTradesPerDay)
        {
            reason = "Risk gate: max trades per day reached.";
            return false;
        }

        reason = "Risk gate: pass.";
        return true;
    }

    public void RegisterAttempt()
    {
        _lastTradeTimeUtc = DateTimeOffset.UtcNow;
        _tradesToday++;
    }

    public void RegisterResult(TradeLogDto log)
    {
        if (log.Result == TradeResult.Lost)
        {
            _consecutiveLosses++;
            _dailyLoss += Math.Abs(log.Profit);
        }
        else if (log.Result == TradeResult.Won)
        {
            _consecutiveLosses = 0;
        }
    }

    public void ResetDailyCounters()
    {
        _tradesToday = 0;
        _consecutiveLosses = 0;
        _dailyLoss = 0m;
    }
}
