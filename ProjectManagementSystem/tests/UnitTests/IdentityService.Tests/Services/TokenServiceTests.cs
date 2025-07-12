using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.IdentityService.Abstractions;
using ProjectManagementSystem.Shared.Common.Configuration;

namespace ProjectManagementSystem.IdentityService.Tests.Services;

public class TokenServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IOptions<JwtSettings>> _jwtOptionsMock;
    private readonly Mock<ILogger<TokenService>> _loggerMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly Mock<IRandomGenerator> _randomGeneratorMock;
    private readonly Mock<IGuidGenerator> _guidGeneratorMock;
    private readonly TokenService _tokenService;
    private readonly DateTime _fixedDateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

    public TokenServiceTests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup JWT Options mock
        _jwtOptionsMock = new Mock<IOptions<JwtSettings>>();
        var jwtSettings = new JwtSettings
        {
            SecretKey = "ThisIsAVeryLongSecretKeyForJWTTokenGeneration123456789",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpiryMinutes = 15,
            RefreshTokenExpiryDays = 7
        };
        _jwtOptionsMock.Setup(x => x.Value).Returns(jwtSettings);

        // Setup other mocks
        _loggerMock = new Mock<ILogger<TokenService>>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _randomGeneratorMock = new Mock<IRandomGenerator>();
        _guidGeneratorMock = new Mock<IGuidGenerator>();

        // Setup fixed time and values for consistent testing
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_fixedDateTime);
        _randomGeneratorMock.Setup(x => x.GenerateBase64String(64)).Returns("MockedBase64RandomString");
        _guidGeneratorMock.Setup(x => x.NewGuidString()).Returns("mocked-guid-string");

        _tokenService = new TokenService(
            _jwtOptionsMock.Object,
            _context,
            _loggerMock.Object,
            _dateTimeProviderMock.Object,
            _randomGeneratorMock.Object,
            _guidGeneratorMock.Object);
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ReturnsValidJWT()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            SecurityStamp = "security-stamp-123"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        // Validate JWT structure and claims
        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadJwtToken(token);

        jsonToken.Should().NotBeNull();
        jsonToken.Issuer.Should().Be("TestIssuer");
        jsonToken.Audiences.Should().Contain("TestAudience");
        jsonToken.ValidTo.Should().Be(_fixedDateTime.AddMinutes(15));

        // Verify claims
        var claims = jsonToken.Claims.ToList();
        claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "1");
        claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "testuser");
        claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        claims.Should().Contain(c => c.Type == ClaimTypes.GivenName && c.Value == "Test");
        claims.Should().Contain(c => c.Type == ClaimTypes.Surname && c.Value == "User");
        claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
        claims.Should().Contain(c => c.Type == "security_stamp" && c.Value == "security-stamp-123");
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti && c.Value == "mocked-guid-string");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnMockedValue()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().Be("MockedBase64RandomString");
        _randomGeneratorMock.Verify(x => x.GenerateBase64String(64), Times.Once);
    }

    [Fact]
    public async Task CreateRefreshTokenAsync_ValidUserId_CreatesAndReturnsToken()
    {
        // Arrange
        var userId = 1;

        // Act
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(userId);

        // Assert
        refreshToken.Should().NotBeNull();
        refreshToken.Token.Should().Be("MockedBase64RandomString");
        refreshToken.UserId.Should().Be(userId);
        refreshToken.ExpiresAt.Should().Be(_fixedDateTime.AddDays(7));
        refreshToken.CreatedAt.Should().Be(_fixedDateTime);
        refreshToken.RevokedAt.Should().BeNull();

        // Verify token was saved to database
        var savedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == refreshToken.Token);
        savedToken.Should().NotBeNull();
        savedToken!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateRefreshTokenAsync_UserWithExistingTokens_RevokesOldTokensAndCreatesNew()
    {
        // Arrange
        var userId = 1;
        
        // Add existing active refresh tokens
        var existingToken1 = new RefreshToken
        {
            Token = "existing-token-1",
            UserId = userId,
            ExpiresAt = _fixedDateTime.AddDays(7),
            CreatedAt = _fixedDateTime.AddDays(-1),
            RevokedAt = null
        };
        var existingToken2 = new RefreshToken
        {
            Token = "existing-token-2",
            UserId = userId,
            ExpiresAt = _fixedDateTime.AddDays(7),
            CreatedAt = _fixedDateTime.AddDays(-2),
            RevokedAt = null
        };

        _context.RefreshTokens.AddRange(existingToken1, existingToken2);
        await _context.SaveChangesAsync();

        // Act
        var newRefreshToken = await _tokenService.CreateRefreshTokenAsync(userId);

        // Assert
        newRefreshToken.Should().NotBeNull();
        newRefreshToken.Token.Should().Be("MockedBase64RandomString");

        // Verify old tokens were revoked
        var revokedTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt != null)
            .ToListAsync();
        
        revokedTokens.Should().HaveCount(2);
        revokedTokens.Should().AllSatisfy(token => token.RevokedAt.Should().Be(_fixedDateTime));
    }

    [Fact]
    public async Task GetRefreshTokenAsync_ExistingToken_ReturnsToken()
    {
        // Arrange
        var tokenValue = "test-refresh-token";
        var refreshToken = new RefreshToken
        {
            Token = tokenValue,
            UserId = 1,
            ExpiresAt = _fixedDateTime.AddDays(7),
            CreatedAt = _fixedDateTime,
            RevokedAt = null
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tokenService.GetRefreshTokenAsync(tokenValue);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(tokenValue);
        result.UserId.Should().Be(1);
    }

    [Fact]
    public async Task GetRefreshTokenAsync_NonExistentToken_ReturnsNull()
    {
        // Arrange
        var nonExistentToken = "non-existent-token";

        // Act
        var result = await _tokenService.GetRefreshTokenAsync(nonExistentToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ExistingToken_RevokesToken()
    {
        // Arrange
        var tokenValue = "token-to-revoke";
        var replacementToken = "replacement-token";
        var refreshToken = new RefreshToken
        {
            Token = tokenValue,
            UserId = 1,
            ExpiresAt = _fixedDateTime.AddDays(7),
            CreatedAt = _fixedDateTime,
            RevokedAt = null
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        // Act
        await _tokenService.RevokeRefreshTokenAsync(tokenValue, replacementToken);

        // Assert
        var revokedToken = await _context.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == tokenValue);
        revokedToken.Should().NotBeNull();
        revokedToken!.RevokedAt.Should().Be(_fixedDateTime);
        revokedToken.ReplacedByToken.Should().Be(replacementToken);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_NonExistentToken_DoesNotThrow()
    {
        // Arrange
        var nonExistentToken = "non-existent-token";

        // Act & Assert
        var act = async () => await _tokenService.RevokeRefreshTokenAsync(nonExistentToken);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAllUserRefreshTokensAsync_UserWithActiveTokens_RevokesAllTokens()
    {
        // Arrange
        var userId = 1;
        var otherUserId = 2;

        var userTokens = new[]
        {
            new RefreshToken { Token = "user1-token1", UserId = userId, ExpiresAt = _fixedDateTime.AddDays(7), CreatedAt = _fixedDateTime, RevokedAt = null },
            new RefreshToken { Token = "user1-token2", UserId = userId, ExpiresAt = _fixedDateTime.AddDays(7), CreatedAt = _fixedDateTime, RevokedAt = null },
            new RefreshToken { Token = "user1-token3", UserId = userId, ExpiresAt = _fixedDateTime.AddDays(7), CreatedAt = _fixedDateTime, RevokedAt = _fixedDateTime.AddDays(-1) }, // Already revoked
            new RefreshToken { Token = "user2-token1", UserId = otherUserId, ExpiresAt = _fixedDateTime.AddDays(7), CreatedAt = _fixedDateTime, RevokedAt = null } // Different user
        };

        _context.RefreshTokens.AddRange(userTokens);
        await _context.SaveChangesAsync();

        // Act
        await _tokenService.RevokeAllUserRefreshTokensAsync(userId);

        // Assert
        var userActiveTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        userActiveTokens.Should().BeEmpty(); // All user's active tokens should be revoked

        // Verify other user's tokens are not affected
        var otherUserActiveTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == otherUserId && rt.RevokedAt == null)
            .ToListAsync();

        otherUserActiveTokens.Should().HaveCount(1);

        // Verify the previously revoked token is not touched
        var alreadyRevokedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == "user1-token3");
        
        alreadyRevokedToken!.RevokedAt.Should().Be(_fixedDateTime.AddDays(-1)); // Should remain the same
    }

    [Fact]
    public async Task RevokeAllUserRefreshTokensAsync_UserWithNoActiveTokens_DoesNotThrow()
    {
        // Arrange
        var userId = 999; // User with no tokens

        // Act & Assert
        var act = async () => await _tokenService.RevokeAllUserRefreshTokensAsync(userId);
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void GenerateAccessToken_UserWithInvalidSecurityStamp_HandlesGracefully(string? securityStamp)
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            SecurityStamp = securityStamp
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        // Verify security_stamp claim is handled properly
        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadJwtToken(token);
        
        var securityStampClaim = jsonToken.Claims.FirstOrDefault(c => c.Type == "security_stamp");
        securityStampClaim.Should().NotBeNull();
        securityStampClaim!.Value.Should().Be(string.Empty); // Should default to empty string
    }

    [Fact]
    public void GenerateAccessToken_WithCustomConfiguration_UsesConfigurationValues()
    {
        // Arrange
        var customExpiryMinutes = 30;
        var customIssuer = "CustomIssuer";
        var customAudience = "CustomAudience";

        var customJwtOptions = new Mock<IOptions<JwtSettings>>();
        var customJwtSettings = new JwtSettings
        {
            SecretKey = "ThisIsAVeryLongSecretKeyForJWTTokenGeneration123456789",
            Issuer = customIssuer,
            Audience = customAudience,
            AccessTokenExpiryMinutes = customExpiryMinutes,
            RefreshTokenExpiryDays = 7
        };
        customJwtOptions.Setup(x => x.Value).Returns(customJwtSettings);

        var customTokenService = new TokenService(
            customJwtOptions.Object,
            _context,
            _loggerMock.Object,
            _dateTimeProviderMock.Object,
            _randomGeneratorMock.Object,
            _guidGeneratorMock.Object);

        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User"
        };

        // Act
        var token = customTokenService.GenerateAccessToken(user);

        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var jsonToken = tokenHandler.ReadJwtToken(token);

        jsonToken.Issuer.Should().Be(customIssuer);
        jsonToken.Audiences.Should().Contain(customAudience);
        jsonToken.ValidTo.Should().Be(_fixedDateTime.AddMinutes(customExpiryMinutes));
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}