using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Repositories.Interfaces;
using Daryva.Api.Security;
using Daryva.Api.Services.Interfaces;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Daryva.Api.Services;

public class AuthService : IAuthService
{
    // OWASP-recommended Argon2id baseline: 19 MiB memory, 2 iterations, 1 degree of parallelism.
    private const int Argon2MemoryKb = 19_456;
    private const int Argon2Iterations = 2;
    private const int Argon2Parallelism = 1;
    private const int Argon2SaltLength = 16;
    private const int Argon2HashLength = 32;
    private static readonly TimeSpan EmailVerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendThrottleWindow = TimeSpan.FromSeconds(30);
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);
    private readonly IAppUserRepository _appUserRepository;
    private readonly IAuthRefreshTokenRepository _authRefreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly JwtOptions _jwtOptions;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppUserRepository appUserRepository,
        IAuthRefreshTokenRepository authRefreshTokenRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        IOptions<JwtOptions> jwtOptions,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _appUserRepository = appUserRepository;
        _authRefreshTokenRepository = authRefreshTokenRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _jwtOptions = jwtOptions.Value;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        var firstName = NormalizeName(request.FirstName, nameof(request.FirstName));
        var lastName = NormalizeName(request.LastName, nameof(request.LastName));
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);
        var now = DateTime.UtcNow;

        var existingUser = await _appUserRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser != null)
        {
            if (existingUser.EmailVerifiedAt.HasValue)
            {
                throw new InvalidOperationException("An account with this email already exists.");
            }

            existingUser.FirstName = firstName;
            existingUser.LastName = lastName;
            existingUser.PasswordHash = HashPassword(request.Password);
            existingUser.IsActive = true;

            var resendResult = await RotateVerificationTokenAndSendAsync(existingUser, now, cancellationToken);
            _auditLogger.Log(existingUser.Id, AuditActorRoles.User, AuditEventTypes.AuthRegister,
                targetType: nameof(AppUser), targetId: existingUser.Id.ToString(),
                metadata: new { reRegistered = true }, ipAddress: clientIp);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return resendResult;
        }

        var tokenPlain = GenerateVerificationToken();

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PasswordHash = HashPassword(request.Password),
            EmailVerificationTokenHash = Sha256(tokenPlain),
            EmailVerificationTokenExpiresAt = now.Add(EmailVerificationTokenLifetime),
            EmailVerificationSentAt = now,
            IsActive = true,
            CreatedAt = now
        };

        _appUserRepository.Add(user);

        var sent = await TrySendVerificationEmailAsync(user.Email, tokenPlain, cancellationToken);
        _auditLogger.Log(user.Id, AuditActorRoles.User, AuditEventTypes.AuthRegister,
            targetType: nameof(AppUser), targetId: user.Id.ToString(), ipAddress: clientIp);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildRegisterResponse(user.Email, sent, "Account created. Please verify your email before logging in.");
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new VerifyEmailResponse
            {
                Verified = false,
                Message = "Verification token is required."
            };
        }

        var now = DateTime.UtcNow;
        var tokenHash = Sha256(token.Trim());

        var user = await _appUserRepository.GetByEmailVerificationTokenHashAsync(tokenHash, cancellationToken);

        if (user == null)
        {
            return new VerifyEmailResponse
            {
                Verified = false,
                Message = "Invalid verification token."
            };
        }

        if (user.EmailVerifiedAt.HasValue)
        {
            return new VerifyEmailResponse
            {
                Verified = true,
                Message = "Email is already verified."
            };
        }

        if (!user.EmailVerificationTokenExpiresAt.HasValue || user.EmailVerificationTokenExpiresAt.Value <= now)
        {
            return new VerifyEmailResponse
            {
                Verified = false,
                Message = "Verification token has expired. Please request a new one."
            };
        }

        user.EmailVerifiedAt = now;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAt = null;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new VerifyEmailResponse
        {
            Verified = true,
            Message = "Email verified successfully. You can now log in."
        };
    }

    public async Task<RegisterResponse> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _appUserRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user == null)
        {
            return BuildRegisterResponse(normalizedEmail, true, "If the account exists, a verification email has been sent.");
        }

        if (user.EmailVerifiedAt.HasValue)
        {
            return BuildRegisterResponse(normalizedEmail, true, "Email is already verified. Please log in.");
        }

        var now = DateTime.UtcNow;
        if (user.EmailVerificationSentAt.HasValue && now - user.EmailVerificationSentAt.Value < ResendThrottleWindow)
        {
            return BuildRegisterResponse(normalizedEmail, true, "Please wait a few seconds before requesting another email.");
        }

        var result = await RotateVerificationTokenAndSendAsync(user, now, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        var allowAnyLogin = _configuration.GetValue<bool>("Auth:AllowAnyLogin");
        var email = NormalizeEmail(request.Email);
        var user = await _appUserRepository.GetByEmailAsync(email, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        var now = DateTime.UtcNow;
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > now)
        {
            var minutesRemaining = (int)Math.Ceiling((user.LockedUntil.Value - now).TotalMinutes);
            _auditLogger.Log(user.Id, AuditActorRoles.User, AuditEventTypes.AuthLoginBlocked,
                targetType: nameof(AppUser), targetId: user.Id.ToString(), ipAddress: clientIp);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException($"Account is temporarily locked due to too many failed login attempts. Try again in {minutesRemaining} minute(s).");
        }

        if (!allowAnyLogin && !VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            var justLocked = user.FailedLoginCount >= MaxFailedLoginAttempts;
            if (justLocked)
            {
                user.LockedUntil = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
            }
            _auditLogger.Log(user.Id, AuditActorRoles.User, AuditEventTypes.AuthLoginFailed,
                targetType: nameof(AppUser), targetId: user.Id.ToString(),
                metadata: new { locked = justLocked }, ipAddress: clientIp);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;
        _auditLogger.Log(user.Id, AuditActorRoles.User, AuditEventTypes.AuthLogin,
            targetType: nameof(AppUser), targetId: user.Id.ToString(), ipAddress: clientIp);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, clientIp, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var tokenHash = Sha256(refreshToken);
        var now = DateTime.UtcNow;

        var session = await _authRefreshTokenRepository.GetByTokenHashWithUserAsync(tokenHash, cancellationToken);

        if (session == null || session.RevokedAt.HasValue || session.ExpiresAt <= now || session.User == null || !session.User.IsActive)
            return null;

        session.RevokedAt = now;

        var response = await IssueTokensAsync(session.User, clientIp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var tokenHash = Sha256(refreshToken);
        var session = await _authRefreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (session == null || session.RevokedAt.HasValue)
            return false;

        session.RevokedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return null;

        var user = await _appUserRepository.GetByIdAsync(userGuid, cancellationToken);
        if (user == null)
            return null;

        return new MeResponse
        {
            UserId = user.Id.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            EmailVerified = user.EmailVerifiedAt.HasValue,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(string email, string? clientIp, CancellationToken cancellationToken = default)
    {
        const string genericMessage = "If an account with that email exists, a password reset link has been sent.";

        var normalizedEmail = NormalizeEmail(email);
        var user = await _appUserRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (user == null || !user.IsActive)
            return new ForgotPasswordResponse { Message = genericMessage };

        var now = DateTime.UtcNow;
        if (user.PasswordResetSentAt.HasValue && now - user.PasswordResetSentAt.Value < ResendThrottleWindow)
            return new ForgotPasswordResponse { Message = genericMessage };

        var tokenPlain = GenerateVerificationToken();
        user.PasswordResetTokenHash = Sha256(tokenPlain);
        user.PasswordResetTokenExpiresAt = now.Add(PasswordResetTokenLifetime);
        user.PasswordResetSentAt = now;

        await TrySendPasswordResetEmailAsync(user.Email, tokenPlain, cancellationToken);
        _auditLogger.Log(user.Id, AuditActorRoles.User, AuditEventTypes.AuthPasswordResetRequested,
            targetType: nameof(AppUser), targetId: user.Id.ToString(), ipAddress: clientIp);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ForgotPasswordResponse { Message = genericMessage };
    }

    public async Task<ResetPasswordResponse> ResetPasswordAsync(string token, string newPassword, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new ResetPasswordResponse { Success = false, Message = "Reset token is required." };

        ValidatePassword(newPassword);

        var now = DateTime.UtcNow;
        var tokenHash = Sha256(token.Trim());

        var user = await _appUserRepository.GetByPasswordResetTokenHashAsync(tokenHash, cancellationToken);
        if (user == null)
            return new ResetPasswordResponse { Success = false, Message = "Invalid or expired reset link." };

        if (!user.PasswordResetTokenExpiresAt.HasValue || user.PasswordResetTokenExpiresAt.Value <= now)
            return new ResetPasswordResponse { Success = false, Message = "Reset link has expired. Please request a new one." };

        user.PasswordHash = HashPassword(newPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.FailedLoginCount = 0;
        user.LockedUntil = null;

        // A password reset invalidates every existing session, not just the one that requested it.
        var activeSessions = await _authRefreshTokenRepository.GetActiveSessionsByUserIdAsync(user.Id, cancellationToken);
        foreach (var session in activeSessions)
            session.RevokedAt = now;

        _auditLogger.Log(user.Id, AuditActorRoles.User, AuditEventTypes.AuthPasswordReset,
            targetType: nameof(AppUser), targetId: user.Id.ToString(), ipAddress: clientIp);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResponse { Success = true, Message = "Password has been reset. You can now log in with your new password." };
    }

    private async Task<bool> TrySendPasswordResetEmailAsync(string email, string token, CancellationToken cancellationToken)
    {
        var resetUrl = BuildResetPasswordUrl(token);
        var subject = "Reset your Daryva password";
        var body = $"We received a request to reset your Daryva password.\n\nOpen this link to choose a new password:\n{resetUrl}\n\nThis link expires in 1 hour. If you did not request this, you can ignore this email.";

        try
        {
            return await _emailSender.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send password reset email to {Email}", email);
            return false;
        }
    }

    private string BuildResetPasswordUrl(string token)
    {
        var template = _configuration["Auth:ResetPasswordUrlTemplate"];
        if (!string.IsNullOrWhiteSpace(template) && template.Contains("{token}", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(template, "\\{token\\}", Uri.EscapeDataString(token), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }

        var baseUrl = _configuration["Auth:ResetPasswordUrlBase"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:5257/api/auth/reset-password";
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }

    private async Task<RegisterResponse> RotateVerificationTokenAndSendAsync(AppUser user, DateTime now, CancellationToken cancellationToken)
    {
        var tokenPlain = GenerateVerificationToken();
        user.EmailVerificationTokenHash = Sha256(tokenPlain);
        user.EmailVerificationTokenExpiresAt = now.Add(EmailVerificationTokenLifetime);
        user.EmailVerificationSentAt = now;

        var sent = await TrySendVerificationEmailAsync(user.Email, tokenPlain, cancellationToken);
        var message = sent
            ? "Verification email sent. Please verify your email before logging in."
            : "Account created, but verification email could not be sent right now. Please request a new verification email.";

        return BuildRegisterResponse(user.Email, sent, message);
    }

    private RegisterResponse BuildRegisterResponse(string email, bool verificationEmailSent, string message)
    {
        return new RegisterResponse
        {
            Email = email,
            RequiresEmailVerification = true,
            VerificationEmailSent = verificationEmailSent,
            Message = message
        };
    }

    private async Task<bool> TrySendVerificationEmailAsync(string email, string token, CancellationToken cancellationToken)
    {
        var verifyUrl = BuildVerifyEmailUrl(token);
        var subject = "Verify your Daryva account";
        var body = $"Welcome to Daryva.\n\nPlease verify your email by opening this link:\n{verifyUrl}\n\nIf you did not create this account, you can ignore this email.";

        try
        {
            return await _emailSender.SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send verification email to {Email}", email);
            return false;
        }
    }

    private string BuildVerifyEmailUrl(string token)
    {
        var template = _configuration["Auth:VerificationUrlTemplate"];
        if (!string.IsNullOrWhiteSpace(template) && template.Contains("{token}", StringComparison.OrdinalIgnoreCase))
        {
            return Regex.Replace(template, "\\{token\\}", Uri.EscapeDataString(token), RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        }

        var baseUrl = _configuration["Auth:VerificationUrlBase"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://localhost:5257/api/auth/verify-email";
        }

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}token={Uri.EscapeDataString(token)}";
    }

    private static string GenerateVerificationToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return raw.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, string? clientIp, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessExpires = now.AddMinutes(Math.Max(1, _jwtOptions.AccessTokenMinutes));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
        };

        var keyBytes = Encoding.UTF8.GetBytes(_jwtOptions.SigningKey);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        var refreshTokenPlain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshTokenHash = Sha256(refreshTokenPlain);

        var refreshSession = new AuthRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(Math.Max(1, _jwtOptions.RefreshTokenDays)),
            CreatedByIp = clientIp
        };

        _authRefreshTokenRepository.Add(refreshSession);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenPlain,
            AccessTokenExpiresAt = accessExpires,
            UserId = user.Id.ToString(),
            Email = user.Email
        };
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        return email.Trim().ToLowerInvariant();
    }

    private static string NormalizeName(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name is required.", paramName);

        var normalized = value.Trim();
        if (normalized.Length > 128)
            throw new ArgumentException("Name must be 128 characters or less.", paramName);

        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.", nameof(password));
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(Argon2SaltLength);
        var hash = ComputeArgon2Hash(password, salt, Argon2Iterations, Argon2MemoryKb, Argon2Parallelism, Argon2HashLength);
        return $"v2.argon2id.{Argon2MemoryKb}.{Argon2Iterations}.{Argon2Parallelism}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 7 || parts[0] != "v2" || parts[1] != "argon2id")
            return false;

        if (!int.TryParse(parts[2], out var memoryKb) ||
            !int.TryParse(parts[3], out var iterations) ||
            !int.TryParse(parts[4], out var parallelism))
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[5]);
            expected = Convert.FromBase64String(parts[6]);
        }
        catch
        {
            return false;
        }

        var actual = ComputeArgon2Hash(password, salt, iterations, memoryKb, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] ComputeArgon2Hash(string password, byte[] salt, int iterations, int memoryKb, int parallelism, int hashLength)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb
        };
        return argon2.GetBytes(hashLength);
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
