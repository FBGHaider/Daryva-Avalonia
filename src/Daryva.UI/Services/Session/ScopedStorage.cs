using Daryva.Services;

namespace Daryva.Services.Session;

/// <summary>
/// Storage that namespaces keys by current user id so each account has its own last-selected org etc.
/// </summary>
public sealed class ScopedStorage : IScopedStorage
{
    private const string KeyPrefix = "user:";
    private const string KeySeparator = ":";

    private readonly ISessionContext _sessionContext;
    private readonly IConfigurationService _configuration;

    public ScopedStorage(ISessionContext sessionContext, IConfigurationService configuration)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    private string GetScopedKey(string key)
    {
        var userId = _sessionContext.UserId;
        if (string.IsNullOrEmpty(userId))
            return string.Empty;
        return KeyPrefix + userId + KeySeparator + key;
    }

    public string? Get(string key)
    {
        if (string.IsNullOrEmpty(key) || !_sessionContext.IsAuthenticated)
            return null;
        var scopedKey = GetScopedKey(key);
        return string.IsNullOrEmpty(scopedKey) ? null : _configuration.GetValue(scopedKey);
    }

    public void Set(string key, string value)
    {
        if (string.IsNullOrEmpty(key) || !_sessionContext.IsAuthenticated)
            return;
        var scopedKey = GetScopedKey(key);
        if (string.IsNullOrEmpty(scopedKey)) return;
        _configuration.SetLocalValue(scopedKey, value ?? string.Empty);
        System.Diagnostics.Debug.WriteLine($"[ScopedStorage] Set key={key} (namespaced by user), value length={value?.Length ?? 0}");
    }

    public void Remove(string key)
    {
        if (string.IsNullOrEmpty(key) || !_sessionContext.IsAuthenticated)
            return;
        var scopedKey = GetScopedKey(key);
        if (string.IsNullOrEmpty(scopedKey)) return;
        _configuration.SetLocalValue(scopedKey, string.Empty);
    }
}
