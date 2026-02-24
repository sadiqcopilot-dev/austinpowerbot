using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AustinXPowerBot.Shared.Contracts;
using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public sealed class ApiClientService : IApiClientService
{
    private readonly HttpClient _httpClient;

    public ApiClientService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5099/")
        };
    }

    public void SetAccessToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
            return;
        }

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    public Task<LoginResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        return PostAndReadAsync<LoginResponse>(ApiRoutes.Auth.Register, request, cancellationToken);
    }

    public Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        return PostAndReadAsync<LoginResponse>(ApiRoutes.Auth.Login, request, cancellationToken);
    }

    public Task<DeviceBindResponse?> BindDeviceAsync(DeviceBindRequest request, CancellationToken cancellationToken = default)
    {
        return PostAndReadAsync<DeviceBindResponse>(ApiRoutes.Device.Bind, request, cancellationToken);
    }

    public Task<LicenseStatusDto?> GetLicenseStatusAsync(string deviceIdHash, long telegramId, CancellationToken cancellationToken = default)
    {
        var route = $"{ApiRoutes.License.Status}?deviceIdHash={Uri.EscapeDataString(deviceIdHash)}&telegramId={telegramId}";
        return _httpClient.GetFromJsonAsync<LicenseStatusDto>(route, cancellationToken);
    }

    public async Task<IReadOnlyList<SignalDto>> GetSignalsAsync(CancellationToken cancellationToken = default)
    {
        var signals = await _httpClient.GetFromJsonAsync<List<SignalDto>>(ApiRoutes.Signals.List, cancellationToken);
        return signals ?? [];
    }

    public async Task<bool> PostTradeLogAsync(TradeLogDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(ApiRoutes.Trades.Create, request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<T?> PostAndReadAsync<T>(string route, object body, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(route, body, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }
}
