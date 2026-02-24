using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace AustinXPowerBot.Api.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly ILogger<TelegramWebhookController> _logger;
        private readonly IConfiguration _config;

        public TelegramWebhookController(ILogger<TelegramWebhookController> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveUpdate([FromBody] Update update)
        {
            try
            {
                if (update == null)
                {
                    return Ok();
                }

                var botToken = _config["TELEGRAM_BOT_TOKEN"] ?? _config["Telegram:BotToken"];
                var adminChatIdRaw = _config["ADMIN_TELEGRAM_CHAT_ID"] ?? _config["Telegram:AdminChatId"];

                if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(adminChatIdRaw))
                {
                    _logger.LogWarning("Telegram webhook received but TELEGRAM_BOT_TOKEN or ADMIN_TELEGRAM_CHAT_ID not configured.");
                    return Ok();
                }

                if (!long.TryParse(adminChatIdRaw, out var adminChatId))
                {
                    _logger.LogWarning("ADMIN_TELEGRAM_CHAT_ID is not a valid chat id: {ChatIdRaw}", adminChatIdRaw);
                    return Ok();
                }

                var client = new TelegramBotClient(botToken);

                // Only handle incoming messages for now
                if (update.Message is { } message)
                {
                    var from = message.From?.Username ?? message.From?.FirstName ?? "unknown";
                    var chatId = message.Chat?.Id.ToString() ?? "unknown";
                    var text = message.Text ?? string.Empty;

                    var forward = $"Client message from {from} (chat:{chatId})\nTime: {DateTimeOffset.UtcNow:O}\nText: {text}";

                    await client.SendMessage(adminChatId, forward);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Telegram webhook update");
                return Ok();
            }
        }

        [HttpPost("setwebhook")]
        public async Task<IActionResult> SetWebhook([FromQuery] string? url)
        {
            var botToken = _config["TELEGRAM_BOT_TOKEN"] ?? _config["Telegram:BotToken"];
            var webhookUrl = string.IsNullOrWhiteSpace(url) ? _config["WEBHOOK_URL"] : url;

            if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(webhookUrl))
            {
                return BadRequest("TELEGRAM_BOT_TOKEN and WEBHOOK_URL (or ?url) are required to set webhook.");
            }

            var client = new TelegramBotClient(botToken);
            await client.SetWebhook(webhookUrl);
            return Ok(new { webhook = webhookUrl });
        }
    }
}
