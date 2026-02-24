using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
// Local notification DTO mirrors the API model to avoid project reference.
namespace AustinXPowerBot.Desktop.Services
{
    public class ApiNotificationDto
    {
        public string Id { get; set; }
        public string ClientId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class NotificationPollingService
    {
        private readonly HttpClient _http;
        private readonly string _apiBaseUrl;
        private CancellationTokenSource _cts;

        public event Action<ApiNotificationDto> NotificationReceived;

        public NotificationPollingService(string apiBaseUrl = "http://localhost:5000")
        {
            _apiBaseUrl = apiBaseUrl?.TrimEnd('/') ?? "http://localhost:5000";
            _http = new HttpClient();
        }

        public void Start(string clientId, TimeSpan pollInterval)
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => PollLoopAsync(clientId, pollInterval, _cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }

        private async Task PollLoopAsync(string clientId, TimeSpan pollInterval, CancellationToken ct)
        {
            var url = $"{_apiBaseUrl}/api/notifications/{Uri.EscapeDataString(clientId)}";
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        var list = await resp.Content.ReadFromJsonAsync<List<ApiNotificationDto>>(cancellationToken: ct).ConfigureAwait(false);
                        if (list != null)
                        {
                            foreach (var n in list)
                            {
                                NotificationReceived?.Invoke(n);
                            }
                        }
                    }
                }
                catch { /* swallow network errors; will retry */ }

                try { await Task.Delay(pollInterval, ct).ConfigureAwait(false); } catch { }
            }
        }
    }
}
