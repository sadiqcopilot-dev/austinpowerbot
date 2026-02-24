using AustinXPowerBot.Shared.Dtos;

namespace AustinXPowerBot.Desktop.Services;

public interface IApiClientService
{
    void SetAccessToken(string? accessToken);
    Task<LoginResponse?> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<DeviceBindResponse?> BindDeviceAsync(DeviceBindRequest request, CancellationToken cancellationToken = default);
    Task<LicenseStatusDto?> GetLicenseStatusAsync(string deviceIdHash, long telegramId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SignalDto>> GetSignalsAsync(CancellationToken cancellationToken = default);
    Task<bool> PostTradeLogAsync(TradeLogDto request, CancellationToken cancellationToken = default);
}
