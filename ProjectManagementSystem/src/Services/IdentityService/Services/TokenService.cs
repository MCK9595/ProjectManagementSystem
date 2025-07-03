using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Abstractions;

namespace ProjectManagementSystem.IdentityService.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TokenService> _logger;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRandomGenerator _randomGenerator;
    private readonly IGuidGenerator _guidGenerator;

    public TokenService(
        IConfiguration configuration,
        ApplicationDbContext context,
        ILogger<TokenService> logger,
        IDateTimeProvider dateTimeProvider,
        IRandomGenerator randomGenerator,
        IGuidGenerator guidGenerator)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _dateTimeProvider = dateTimeProvider;
        _randomGenerator = randomGenerator;
        _guidGenerator = guidGenerator;
    }

    public string GenerateAccessToken(ApplicationUser user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        var issuer = jwtSettings["Issuer"] ?? "ProjectManagementSystem.IdentityService";
        var audience = jwtSettings["Audience"] ?? "ProjectManagementSystem.Users";
        var expiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName!),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("security_stamp", string.IsNullOrWhiteSpace(user.SecurityStamp) ? string.Empty : user.SecurityStamp),
            new Claim(JwtRegisteredClaimNames.Jti, _guidGenerator.NewGuidString()),
            new Claim(JwtRegisteredClaimNames.Iat, 
                new DateTimeOffset(_dateTimeProvider.UtcNow).ToUnixTimeSeconds().ToString(), 
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: _dateTimeProvider.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        _logger.LogInformation("Generated access token for user {UserId} ({Username})", user.Id, user.UserName);
        
        return tokenString;
    }

    public string GenerateRefreshToken()
    {
        return _randomGenerator.GenerateBase64String(64);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(int userId)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var expiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");

        // Revoke any existing active refresh tokens for this user
        await RevokeAllUserRefreshTokensAsync(userId);

        var refreshToken = new RefreshToken
        {
            Token = GenerateRefreshToken(),
            UserId = userId,
            ExpiresAt = _dateTimeProvider.UtcNow.AddDays(expiryDays),
            CreatedAt = _dateTimeProvider.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created refresh token for user {UserId}", userId);
        
        return refreshToken;
    }

    public async Task<RefreshToken?> GetRefreshTokenAsync(string token)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token);
    }

    public async Task RevokeRefreshTokenAsync(string token, string? replacedByToken = null)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == token);

        if (refreshToken != null)
        {
            refreshToken.RevokedAt = _dateTimeProvider.UtcNow;
            refreshToken.ReplacedByToken = replacedByToken;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Revoked refresh token for user {UserId}", refreshToken.UserId);
        }
    }

    public async Task RevokeAllUserRefreshTokensAsync(int userId)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = _dateTimeProvider.UtcNow;
        }

        if (activeTokens.Any())
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Revoked {Count} active refresh tokens for user {UserId}", activeTokens.Count, userId);
        }
    }
}