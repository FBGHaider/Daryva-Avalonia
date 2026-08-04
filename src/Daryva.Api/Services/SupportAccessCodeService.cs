using System.Security.Cryptography;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security;
using Daryva.Api.Services.Interfaces;

namespace Daryva.Api.Services;

public class SupportAccessCodeService : ISupportAccessCodeService
{
    private const int CodeLength = 6;
    private const int ExpiryMinutes = 15;
    private const int MaxGenerateAttempts = 5;

    // Excludes 0/O and 1/I/L -- read-aloud/typed-over-the-phone friendly.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly ISupportAccessCodeRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;

    public SupportAccessCodeService(
        ISupportAccessCodeRepository repository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
    }

    public async Task<SupportAccessCodeResponse> GenerateAsync(string userId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organizationName = await _repository.GetOrganizationNameAsync(organizationId, cancellationToken);
        if (organizationName == null)
            throw new ArgumentException("Organization not found.", nameof(organizationId));

        string code;
        var attempts = 0;
        do
        {
            if (++attempts > MaxGenerateAttempts)
                throw new InvalidOperationException("Could not generate a unique code. Try again.");
            code = GenerateRandomCode();
        }
        while (await _repository.GetByCodeAsync(code, cancellationToken) != null);

        var now = DateTime.UtcNow;
        var accessCode = new SupportAccessCode
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Code = code,
            CreatedByUserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ExpiryMinutes)
        };

        _repository.Add(accessCode);

        if (Guid.TryParse(userId, out var actorId))
        {
            _auditLogger.Log(actorId, Domain.OrganizationMember.Roles.Landlord, AuditEventTypes.SupportAccessCodeGenerated,
                organizationId: organizationId, targetType: nameof(SupportAccessCode), targetId: accessCode.Id.ToString());
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SupportAccessCodeResponse { Code = accessCode.Code, ExpiresAt = accessCode.ExpiresAt };
    }

    public async Task<ResolvedSupportAccessCodeResponse?> ResolveAsync(string adminUserId, string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var accessCode = await _repository.GetByCodeAsync(code, cancellationToken);
        if (accessCode == null)
            return null;

        var now = DateTime.UtcNow;
        if (accessCode.ResolvedAt != null || accessCode.ExpiresAt <= now)
            return null;

        var organizationName = await _repository.GetOrganizationNameAsync(accessCode.OrganizationId, cancellationToken);
        if (organizationName == null)
            return null;

        if (Guid.TryParse(adminUserId, out var adminGuid))
        {
            accessCode.ResolvedByAdminId = adminGuid;
            accessCode.ResolvedAt = now;

            _auditLogger.Log(adminGuid, Roles.Admin, AuditEventTypes.SupportAccessCodeResolved,
                organizationId: accessCode.OrganizationId, targetType: nameof(SupportAccessCode), targetId: accessCode.Id.ToString());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ResolvedSupportAccessCodeResponse { OrganizationId = accessCode.OrganizationId, OrganizationName = organizationName };
    }

    private static string GenerateRandomCode()
    {
        Span<char> buffer = stackalloc char[CodeLength];
        for (var i = 0; i < CodeLength; i++)
        {
            var index = RandomNumberGenerator.GetInt32(CodeAlphabet.Length);
            buffer[i] = CodeAlphabet[index];
        }
        return new string(buffer);
    }
}
