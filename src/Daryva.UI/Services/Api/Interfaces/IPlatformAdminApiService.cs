using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

/// <summary>One account holding AppUser.IsPlatformAdmin.</summary>
public class PlatformAdminDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Manage which accounts hold platform admin access. Granting only happens via server config
/// (Admin:BootstrapEmails) plus a restart -- there is deliberately no grant call here, only list
/// and revoke. Every method requires the signed-in account to already hold IsPlatformAdmin
/// server-side -- the API returns 403 for anyone else.
/// </summary>
public interface IPlatformAdminApiService
{
    Task<List<PlatformAdminDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Revokes platform admin from the given user. The server also refuses to let an
    /// admin revoke their own access -- this isn't re-validated client-side beyond disabling the
    /// button on the caller's own row.</summary>
    Task RevokeAsync(Guid userId, CancellationToken cancellationToken = default);
}
