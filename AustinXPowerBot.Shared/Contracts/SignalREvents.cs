namespace AustinXPowerBot.Shared.Contracts;

public static class SignalREvents
{
    public const string RealtimeHubPath = "/hubs/realtime";

    public static class Client
    {
        public const string SignalReceived = "signal.received";
        public const string NotificationReceived = "notification.received";
        public const string RemoteCommandReceived = "remote-command.received";
    }

    public static class Groups
    {
        public const string UserPrefix = "user:";
        public const string DevicePrefix = "device:";
    }
}
