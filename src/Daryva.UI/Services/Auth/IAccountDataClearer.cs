namespace Daryva.Services.Auth;

/// <summary>
/// Clears all local data that is bound to the currently signed-in account.
/// Must be called on sign-out so the next user does not see the previous user's organisations or members.
/// </summary>
public interface IAccountDataClearer
{
    Task ClearAsync(CancellationToken cancellationToken = default);
}
