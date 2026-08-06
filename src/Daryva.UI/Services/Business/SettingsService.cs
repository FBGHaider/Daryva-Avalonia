using Daryva.Services.Api;

namespace Daryva.Services.Business;

/// <summary>
/// API-first implementation of application settings that stores values in local config,
/// without relying on the local SQLite settings table.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IConfigurationService _configurationService;
    private readonly IApiClient _apiClient;

    public SettingsService(IConfigurationService configurationService, IApiClient apiClient)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default) where T : struct
    {
        var raw = _configurationService.GetValue(key);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Task.FromResult(defaultValue);
        }

        try
        {
            if (typeof(T) == typeof(bool) && bool.TryParse(raw, out var boolValue))
                return Task.FromResult<T?>((T)(object)boolValue);

            if (typeof(T) == typeof(int) && int.TryParse(raw, out var intValue))
                return Task.FromResult<T?>((T)(object)intValue);

            if (typeof(T) == typeof(decimal) && decimal.TryParse(raw, out var decimalValue))
                return Task.FromResult<T?>((T)(object)decimalValue);

            if (typeof(T) == typeof(DateTime) && DateTime.TryParse(raw, out var dateValue))
                return Task.FromResult<T?>((T)(object)dateValue);

            if (typeof(T).IsEnum && Enum.TryParse(typeof(T), raw, true, out var enumValue) && enumValue != null)
                return Task.FromResult<T?>((T)enumValue);
        }
        catch
        {
        }

        return Task.FromResult(defaultValue);
    }

    public Task<string?> GetSettingAsync(string key, string? defaultValue = null)
    {
        var value = _configurationService.GetValue(key);
        return Task.FromResult(string.IsNullOrWhiteSpace(value) ? defaultValue : value);
    }

    public Task<Dictionary<string, string>> GetCategorySettingsAsync(string category)
    {
        return Task.FromResult(new Dictionary<string, string>());
    }

    public Task SetSettingAsync<T>(string key, T value)
    {
        var stringValue = value switch
        {
            bool b => b.ToString().ToLowerInvariant(),
            decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O"),
            _ => value?.ToString() ?? string.Empty
        };

        _configurationService.SetLocalValue(key, stringValue);
        return Task.CompletedTask;
    }

    public Task SetSettingsAsync(Dictionary<string, string> settings)
    {
        foreach (var kvp in settings)
        {
            _configurationService.SetLocalValue(kvp.Key, kvp.Value ?? string.Empty);
        }

        return Task.CompletedTask;
    }

    public Task<decimal> GetDatabaseSizeAsync()
    {
        return Task.FromResult(0m);
    }

    public async Task<bool> IsDatabaseConnectedAsync()
    {
        try
        {
            var response = await _apiClient.HttpClient.GetAsync("health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
