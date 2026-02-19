namespace Daryva.Services.Api;

/// <summary>
/// HTTP client for communicating with Daryva.Api backend.
/// Manages the base HttpClient and organization context (X-Org-Id header).
/// </summary>
public interface IApiClient
{
    /// <summary>
    /// Get the current organization ID.
    /// </summary>
    Guid? CurrentOrgId { get; }

    /// <summary>
    /// Set the current organization ID for subsequent requests.
    /// Adds X-Org-Id header to all requests.
    /// </summary>
    void SetCurrentOrgId(Guid orgId);

    /// <summary>
    /// Clear the current organization ID.
    /// Removes X-Org-Id header from requests.
    /// </summary>
    void ClearCurrentOrgId();

    /// <summary>
    /// Get the underlying HttpClient for making API requests.
    /// </summary>
    HttpClient HttpClient { get; }
}
