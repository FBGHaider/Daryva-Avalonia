using System.Text.Json.Serialization;

namespace Daryva.Services.Api;

/// <summary>
/// Organization data transfer object from API.
/// </summary>
public class OrganizationDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("currentUserRole")]
    public string CurrentUserRole { get; set; } = string.Empty;
}

public class JoinOrganizationResultDto
{
    [JsonPropertyName("organizationId")]
    public Guid OrganizationId { get; set; }

    [JsonPropertyName("organizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("alreadyMember")]
    public bool AlreadyMember { get; set; }
}

/// <summary>
/// Member list item from GET /api/orgs/{orgId}/members.
/// </summary>
public class OrganizationMemberDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("joinedAt")]
    public DateTime JoinedAt { get; set; }
}

/// <summary>
/// Service for organization-related API operations.
/// </summary>
public interface IOrganizationApiService
{
    /// <summary>
    /// Get all organizations the current user belongs to.
    /// </summary>
    Task<List<OrganizationDto>> GetUserOrganizationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Update organization (e.g. rename). Owner only. PATCH /api/orgs/{orgId}.
    /// </summary>
    Task<OrganizationDto> UpdateOrganizationAsync(Guid orgId, string newName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all members of an organization (current user must be a member).
    /// Uses existing X-Org-Id on the client; call after setting current org.
    /// </summary>
    Task<List<OrganizationMemberDto>> GetOrganizationMembersAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new organization. Current user becomes the Owner.
    /// </summary>
    Task<OrganizationDto> CreateOrganizationAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific organization by ID (if user is member).
    /// </summary>
    Task<OrganizationDto> GetOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an organization (Owner only).
    /// </summary>
    Task DeleteOrganizationAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Join organization by invite token.
    /// </summary>
    Task<JoinOrganizationResultDto> JoinByInviteAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Join organization by code.
    /// </summary>
    Task<JoinOrganizationResultDto> JoinByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore data from a backup JSON file (Daryva API backup format). Imports into the current org (X-Org-Id).
    /// </summary>
    Task<ImportBackupResultDto> ImportBackupAsync(string backupJson, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of POST /api/import (backup restore).
/// </summary>
public class ImportBackupResultDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public ImportStatsDto? Stats { get; set; }

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }
}

public class ImportStatsDto
{
    [JsonPropertyName("housesImported")]
    public int HousesImported { get; set; }

    [JsonPropertyName("tenantsImported")]
    public int TenantsImported { get; set; }

    [JsonPropertyName("tenanciesImported")]
    public int TenanciesImported { get; set; }

    [JsonPropertyName("expensesImported")]
    public int ExpensesImported { get; set; }

    [JsonPropertyName("documentsImported")]
    public int DocumentsImported { get; set; }

    [JsonPropertyName("rentPaymentsImported")]
    public int RentPaymentsImported { get; set; }

    [JsonPropertyName("depositPaymentsImported")]
    public int DepositPaymentsImported { get; set; }

    [JsonPropertyName("depositReturnsImported")]
    public int DepositReturnsImported { get; set; }

    [JsonPropertyName("totalItemsImported")]
    public int TotalItemsImported { get; set; }
}
