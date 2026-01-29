namespace Daryva.Services.Platform
{
    /// <summary>
    /// Provides secure storage for sensitive data (e.g., passwords, API keys).
    /// Windows: Uses DPAPI (ProtectedData)
    /// macOS: Uses Keychain (TODO: implement proper Keychain integration)
    /// For now, macOS uses a simple encrypted file approach with a clear TODO.
    /// </summary>
    public interface ISecureStore
    {
        /// <summary>
        /// Stores a value securely.
        /// </summary>
        void Store(string key, string value);

        /// <summary>
        /// Retrieves a value securely.
        /// </summary>
        string? Retrieve(string key);

        /// <summary>
        /// Removes a stored value.
        /// </summary>
        void Remove(string key);
    }
}
