namespace Daryva.Services.AppReset;

/// <summary>
/// Single place to reset app state to signed-out or post sign-in skeleton. Ensures no state leaks between accounts.
/// </summary>
public interface IAppResetService
{
    /// <summary>
    /// Resets app to signed-out state: session, org context, API auth, navigation stack, in-memory caches, and local files.
    /// Call after clearing tokens on sign-out; then show SignInView.
    /// </summary>
    Task ResetToSignedOutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Optional: minimal reset before loading /api/me and org context (e.g. clear navigation to a known state). Can be no-op.
    /// </summary>
    Task ResetToSignedInSkeletonAsync(CancellationToken cancellationToken = default);
}
