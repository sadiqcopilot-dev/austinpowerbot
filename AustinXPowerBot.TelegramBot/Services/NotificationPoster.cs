using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace AustinXPowerBot.TelegramBot.Services
{
    public class NotificationPoster
    {
        private readonly HttpClient _http;
        private readonly string _apiBaseUrl;

        public NotificationPoster(string apiBaseUrl = "http://localhost:5000")
        {
            _apiBaseUrl = apiBaseUrl?.TrimEnd('/') ?? "http://localhost:5000";
            _http = new HttpClient();
        }

        public record Payload(string ClientId, string Title, string Message);

        public async Task<bool> PostAsync(string clientId, string title, string message)
        {
            var url = $"{_apiBaseUrl}/api/notifications";
            var payload = new Payload(clientId ?? "*", title, message);
            try
            {
                var resp = await _http.PostAsJsonAsync(url, new { ClientId = payload.ClientId, Title = payload.Title, Message = payload.Message }).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
