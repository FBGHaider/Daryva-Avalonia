using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResponse?> RefreshAsync(string refreshToken, string? clientIp, CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<MeResponse?> GetMeAsync(string userId, CancellationToken cancellationToken = default);
}

public class AuthService : IAuthService
{
    private const int PasswordIterations = 100_000;
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public AuthService(AppDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        var exists = await _dbContext.AppUsers.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
            throw new InvalidOperationException("An account with this email already exists.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow
        };

        _dbContext.AppUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, clientIp, cancellationToken);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, string? clientIp, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _dbContext.AppUsers.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        if (!VerifyPassword(request.Password, user.PasswordHash))
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
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
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
