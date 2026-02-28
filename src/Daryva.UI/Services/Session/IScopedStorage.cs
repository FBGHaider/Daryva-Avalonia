namespace Daryva.Services.Session;

/// <summary>
/// Key-value storage namespaced by current user id (sub). Use for last-selected org and other per-user preferences.
/// When no user is signed in, Get returns null and Set/Remove are no-ops.
/// </summary>
public interface IScopedStorage
{
    /// <summary>Gets a value for the given key in the current user's namespace (e.g. user:userId:key).</summary>
    string? Get(string key);

    /// <summary>Sets a value for the given key in the current user's namespace.</summary>
    void Set(string key, string value);

    /// <summary>Removes the key for the current user's namespace.</summary>
    void Remove(string key);
}
