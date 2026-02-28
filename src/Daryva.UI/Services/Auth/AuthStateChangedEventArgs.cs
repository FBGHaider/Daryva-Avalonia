namespace Daryva.Services.Auth;

public sealed class AuthStateChangedEventArgs : EventArgs
{
    public bool IsSignedIn { get; init; }
}
