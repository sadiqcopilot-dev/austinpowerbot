namespace AustinXPowerBot.Desktop.Services;

public interface ILocalStorageService
{
    Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default);
}
