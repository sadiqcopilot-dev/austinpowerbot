namespace AustinXPowerBot.Shared.Contracts;

public static class ApiRoutes
{
    public const string ApiBase = "api";

    public static class Auth
    {
        public const string Register = ApiBase + "/auth/register";
        public const string Login = ApiBase + "/auth/login";
    }

    public static class Device
    {
        public const string Bind = ApiBase + "/device/bind";
    }

    public static class License
    {
        public const string Status = ApiBase + "/license/status";
        public const string RequestActivation = ApiBase + "/license/request-activation";
        public const string AdminApprove = ApiBase + "/license/admin/approve";
    }

    public static class Signals
    {
        public const string Create = ApiBase + "/signals";
        public const string List = ApiBase + "/signals";
    }

    public static class Trades
    {
        public const string Create = ApiBase + "/trades";
        public const string List = ApiBase + "/trades";
    }

    public static class Telegram
    {
        public const string Link = ApiBase + "/telegram/link";
        public const string Status = ApiBase + "/telegram/status";
        public const string Command = ApiBase + "/telegram/command";
    }
}
