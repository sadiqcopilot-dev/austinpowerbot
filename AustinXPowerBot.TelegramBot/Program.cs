using System;
using System.Threading.Tasks;
using Telegram.Bot;

namespace AustinXPowerBot.TelegramBot;

internal static class Program
{
    // Small webhook helper: sets Telegram webhook to the API endpoint and exits.
    public static async Task<int> Main()
    {
        var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
        var apiBase = Environment.GetEnvironmentVariable("API_BASE_URL") ?? string.Empty;
        var webhookPath = Environment.GetEnvironmentVariable("WEBHOOK_PATH") ?? "/api/telegram/webhook";
        var webhookUrl = Environment.GetEnvironmentVariable("WEBHOOK_URL") ?? (string.IsNullOrWhiteSpace(apiBase) ? string.Empty : (apiBase.TrimEnd('/') + webhookPath));

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("TELEGRAM_BOT_TOKEN is not configured. Exiting.");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            Console.WriteLine("WEBHOOK_URL or API_BASE_URL+WEBHOOK_PATH must be set. Exiting.");
            return 0;
        }

        try
        {
            Console.WriteLine($"Setting webhook to: {webhookUrl}");
            var client = new TelegramBotClient(token);
            await client.SetWebhookAsync(webhookUrl);
            Console.WriteLine("SetWebhook completed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set webhook: {ex.Message}");
            return 2;
        }
    }
}
