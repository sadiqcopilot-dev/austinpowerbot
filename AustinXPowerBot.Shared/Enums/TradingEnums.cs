namespace AustinXPowerBot.Shared.Enums;

public enum SignalDirection
{
    Buy = 0,
    Sell = 1
}

public enum SignalStrength
{
    Weak = 0,
    Medium = 1,
    Strong = 2
}

public enum TradeResult
{
    Pending = 0,
    Won = 1,
    Lost = 2,
    Cancelled = 3
}
