using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AustinXPowerBot.Api.Models;
using System.Linq;

namespace AustinXPowerBot.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationsController : ControllerBase
    {
        // In-memory store. Simple and works for small deployments; can be replaced with DB later.
        private static readonly ConcurrentDictionary<string, List<Notification>> _store = new();

        [HttpPost]
        public IActionResult Post([FromBody] Notification notification)
        {
            if (notification == null || string.IsNullOrWhiteSpace(notification.Message))
                return BadRequest("Invalid notification payload.");

            var key = notification.ClientId ?? "*";
            var list = _store.GetOrAdd(key, _ => new List<Notification>());
            lock (list)
            {
                list.Add(notification);
            }

            return Accepted(notification);
        }

        [HttpGet("{clientId}")]
        public IActionResult Get(string clientId)
        {
            // Return and remove notifications for the client plus broadcasts
            var result = new List<Notification>();

            if (_store.TryRemove(clientId, out var clientList))
            {
                lock (clientList) { result.AddRange(clientList); }
            }

            if (_store.TryRemove("*", out var broadcast))
            {
                lock (broadcast) { result.AddRange(broadcast); }
            }

            return Ok(result.OrderBy(n => n.CreatedAt));
        }
    }
}
