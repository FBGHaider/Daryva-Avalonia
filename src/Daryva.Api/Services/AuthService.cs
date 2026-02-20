using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Daryva.Api.Data;
using Daryva.Api.Domain;
using Daryva.Api.Dtos;
using Daryva.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Daryva.Api.Services;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<VerifyEmailResponse> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task<RegisterResponse> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private const int PasswordIterations = 100_000;
    private static readonly TimeSpan EmailVerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResendThrottleWindow = TimeSpan.FromSeconds(30);
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var firstName = NormalizeName(request.FirstName, nameof(request.FirstName));
        var lastName = NormalizeName(request.LastName, nameof(request.LastName));
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);
        var now = DateTime.UtcNow;

        var existingUser = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
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
            await _dbContext.SaveChangesAsync(cancellationToken);
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

        _dbContext.AppUsers.Add(user);

        var sent = await TrySendVerificationEmailAsync(user.Email, tokenPlain, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(
            u => u.EmailVerificationTokenHash == tokenHash,
            cancellationToken);

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
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new VerifyEmailResponse
        {
            Verified = true,
            Message = "Email verified successfully. You can now log in."
        };
    }

    public async Task<RegisterResponse> ResendVerificationEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

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
        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        var allowAnyLogin = _configuration.GetValue<bool>("Auth:AllowAnyLogin");
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        if (!allowAnyLogin && !VerifyPassword(request.Password, user.PasswordHash))
            return null;

        user.LastLoginAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, clientIp, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var tokenHash = Sha256(refreshToken);
        var now = DateTime.UtcNow;

        var session = await _dbContext.AuthRefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (session == null || session.RevokedAt.HasValue || session.ExpiresAt <= now || session.User == null || !session.User.IsActive)
            return null;

        session.RevokedAt = now;

        var response = await IssueTokensAsync(session.User, clientIp, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var tokenHash = Sha256(refreshToken);
        var session = await _dbContext.AuthRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        if (session == null || session.RevokedAt.HasValue)
            return false;

        session.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var userGuid))
            return null;

        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Id == userGuid, cancellationToken);
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

        _dbContext.AuthRefreshTokens.Add(refreshSession);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, PasswordIterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{PasswordIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
            return false;

        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != "v1")
            return false;

        if (!int.TryParse(parts[1], out var iterations))
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
