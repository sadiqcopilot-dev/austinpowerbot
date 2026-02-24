using System;

namespace AustinXPowerBot.Api.Models
{
    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ClientId { get; set; } = "*"; // '*' means broadcast
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
