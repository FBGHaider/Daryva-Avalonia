namespace Daryva.Services.Auth;

/// <summary>
/// Secure persistence for OAuth/OIDC tokens.
/// Windows: DPAPI via <see cref="Platform.ISecureStore"/>; macOS/Linux: best effort.
/// </summary>
public interface ITokenStore
{
    Task SaveAsync(StoredToken token, CancellationToken cancellationToken = default);
    Task<StoredToken?> LoadAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
