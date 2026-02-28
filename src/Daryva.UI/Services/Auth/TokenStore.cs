using System.Text.Json;
using Daryva.Services.Platform;

namespace Daryva.Services.Auth;

/// <summary>
/// Persists tokens using <see cref="ISecureStore"/> (DPAPI on Windows, encrypted file elsewhere).
/// </summary>
public sealed class TokenStore : ITokenStore
{
    private const string Key = "Daryva.SaaS.Tokens";
    private readonly ISecureStore _secureStore;

    public TokenStore(ISecureStore secureStore)
    {
        _secureStore = secureStore;
    }

    public Task SaveAsync(StoredToken token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = JsonSerializer.Serialize(token);
        _secureStore.Store(Key, json);
        return Task.CompletedTask;
    }

    public Task<StoredToken?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var raw = _secureStore.Retrieve(Key);
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult<StoredToken?>(null);
        try
        {
            var token = JsonSerializer.Deserialize<StoredToken>(raw);
            return Task.FromResult<StoredToken?>(token);
        }
        catch
        {
            _secureStore.Remove(Key);
            return Task.FromResult<StoredToken?>(null);
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _secureStore.Remove(Key);
        return Task.CompletedTask;
    }
}
