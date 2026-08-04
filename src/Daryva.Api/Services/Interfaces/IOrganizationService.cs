using Daryva.Api.Dtos;

namespace Daryva.Api.Services.Interfaces;

/// <summary>
/// Business logic for organization management and multi-tenancy.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Create a new organization. Current user becomes the Owner.
    /// </summary>
    /// <param name="callerEmail">Optional email from JWT/claims so the owner member shows the signed-in email; if null, resolved from profile.</param>
    Task<OrganizationResponse> CreateOrganizationAsync(
        string userId,
        CreateOrganizationRequest request,
        string? callerEmail = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all organizations the user belongs to.
    /// </summary>
    Task<IEnumerable<OrganizationResponse>> GetUserOrganizationsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// If the user has no organizations, create a default one (e.g. for first-time or Clerk-only users).
    /// Used so the app can skip "setup" and go straight to dashboard.
    /// </summary>
    Task EnsureDefaultOrganizationAsync(
        string userId,
        string? suggestedName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get organization by ID (if user is member).
    /// </summary>
    Task<OrganizationResponse?> GetOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update organization (e.g. rename). Caller must be Owner.
    /// </summary>
    Task<OrganizationResponse?> UpdateOrganizationAsync(
        Guid orgId,
        string userId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a member to an organization (by email).
    /// </summary>
    Task<OrganizationMemberResponse> AddMemberAsync(
        Guid orgId,
        string userId,
        AddMemberRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an organization (Owner only).
    /// </summary>
    Task<bool> DeleteOrganizationAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all members of an organization (if user is member).
    /// </summary>
    Task<IEnumerable<OrganizationMemberResponse>> GetOrganizationMembersAsync(
        Guid orgId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a single-use invite token for joining an organization.
    /// </summary>
    Task<CreateOrgInviteResponse> CreateInviteAsync(
        Guid orgId,
        string userId,
        CreateOrgInviteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Accept an invite token and join the referenced organization.
    /// </summary>
    Task<JoinOrganizationResponse> AcceptInviteAsync(
        string userId,
        AcceptOrgInviteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate (and rotate) a reusable org join code.
    /// </summary>
    Task<GenerateOrgJoinCodeResponse> GenerateJoinCodeAsync(
        Guid orgId,
        string userId,
        GenerateOrgJoinCodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Join an organization using an active join code.
    /// </summary>
    Task<JoinOrganizationResponse> JoinByCodeAsync(
        string userId,
        JoinOrgByCodeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Platform-admin org browse (Support Mode): every organization, not scoped to the caller's
    /// own memberships. Caller must already be authorized (controller enforces Platform.ManageOrganizations).
    /// </summary>
    Task<AdminOrganizationListResponse> GetAllOrganizationsAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
