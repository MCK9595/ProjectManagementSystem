using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using FluentAssertions;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.IdentityService.Abstractions;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IdentityService.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly AuthService _authService;
    private readonly DateTime _fixedDateTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

    public AuthServiceTests()
    {
        // Setup In-Memory Database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);

        // Setup UserManager mock
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null, null, null, null, null, null, null, null);

        // Setup SignInManager mock
        var contextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var userPrincipalFactoryMock = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            _userManagerMock.Object, contextAccessorMock.Object, userPrincipalFactoryMock.Object,
            null, null, null, null);

        // Setup other mocks
        _tokenServiceMock = new Mock<ITokenService>();
        _loggerMock = new Mock<ILogger<AuthService>>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        
        // Setup fixed time for consistent testing
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(_fixedDateTime);

        _authService = new AuthService(
            _context,
            _userManagerMock.Object,
            _signInManagerMock.Object,
            _tokenServiceMock.Object,
            _loggerMock.Object,
            _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Username = "testuser",
            Password = "TestPassword123!"
        };

        var user = new ApplicationUser
        {
            Id = 1,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true
        };

        var expectedToken = "test-jwt-token";
        var expectedRefreshToken = new RefreshToken { Token = "refresh-token", UserId = 1 };

        // Setup context with user
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Setup mocks
        _signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.Success);

        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(false);

        _userManagerMock
            .Setup(x => x.UpdateAsync(user))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { user.Role });

        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(user))
            .Returns(expectedToken);

        _tokenServiceMock
            .Setup(x => x.CreateRefreshTokenAsync(user.Id))
            .ReturnsAsync(expectedRefreshToken);

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(expectedToken);
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be(user.UserName);
        result.User.Email.Should().Be(user.Email);
        result.ExpiresAt.Should().Be(_fixedDateTime.AddMinutes(15));

        // Verify LastLoginAt was updated
        user.LastLoginAt.Should().Be(_fixedDateTime);

        // Verify method calls
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(user), Times.Once);
        _tokenServiceMock.Verify(x => x.CreateRefreshTokenAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Username = "nonexistent",
            Password = "TestPassword123!"
        };

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().BeNull();
        
        // Verify no token generation occurred
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()), Times.Never);
        _tokenServiceMock.Verify(x => x.CreateRefreshTokenAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_InactiveUser_ReturnsNull()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Username = "inactiveuser",
            Password = "TestPassword123!"
        };

        var user = new ApplicationUser
        {
            Id = 2,
            UserName = "inactiveuser",
            Email = "inactive@example.com",
            FirstName = "Inactive",
            LastName = "User",
            Role = "User",
            IsActive = false // Inactive user
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().BeNull();
        
        // Verify no password check occurred
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_LockedOutUser_ReturnsNull()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Username = "lockeduser",
            Password = "TestPassword123!"
        };

        var user = new ApplicationUser
        {
            Id = 3,
            UserName = "lockeduser",
            Email = "locked@example.com",
            FirstName = "Locked",
            LastName = "User",
            Role = "User",
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(true); // User is locked out

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().BeNull();
        
        // Verify password check was not performed
        _signInManagerMock.Verify(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Username = "testuser",
            Password = "WrongPassword123!"
        };

        var user = new ApplicationUser
        {
            Id = 4,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(false);

        _signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.Failed); // Wrong password

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().BeNull();
        
        // Verify no token generation occurred
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_AccountLockedDuringSignIn_ReturnsNull()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Username = "testuser",
            Password = "TestPassword123!"
        };

        var user = new ApplicationUser
        {
            Id = 5,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _userManagerMock
            .Setup(x => x.IsLockedOutAsync(user))
            .ReturnsAsync(false);

        _signInManagerMock
            .Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.LockedOut); // Account gets locked during sign-in

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().BeNull();
        
        // Verify no token generation occurred
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewAuthResponse()
    {
        // Arrange
        var refreshTokenValue = "valid-refresh-token";
        var user = new ApplicationUser
        {
            Id = 6,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-30),
            UpdatedAt = _fixedDateTime.AddDays(-30)
        };

        var storedRefreshToken = new RefreshToken
        {
            Token = refreshTokenValue,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7), // Use real current time for expiry
            RevokedAt = null
        };

        var newAccessToken = "new-access-token";
        var newRefreshToken = new RefreshToken 
        { 
            Token = "new-refresh-token", 
            UserId = user.Id, 
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _tokenServiceMock
            .Setup(x => x.GetRefreshTokenAsync(refreshTokenValue))
            .ReturnsAsync(storedRefreshToken);

        _tokenServiceMock
            .Setup(x => x.GenerateAccessToken(user))
            .Returns(newAccessToken);

        _tokenServiceMock
            .Setup(x => x.CreateRefreshTokenAsync(user.Id))
            .ReturnsAsync(newRefreshToken);

        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { user.Role });

        // Act
        var result = await _authService.RefreshTokenAsync(refreshTokenValue);

        // Assert
        result.Should().NotBeNull();
        result!.Token.Should().Be(newAccessToken);
        result.User.Should().NotBeNull();
        result.User.Username.Should().Be(user.UserName);
        result.ExpiresAt.Should().Be(_fixedDateTime.AddMinutes(15));

        // Verify old token was revoked and new tokens were generated
        _tokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(refreshTokenValue, newRefreshToken.Token), Times.Once);
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(user), Times.Once);
        _tokenServiceMock.Verify(x => x.CreateRefreshTokenAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ReturnsNull()
    {
        // Arrange
        var invalidToken = "invalid-refresh-token";

        _tokenServiceMock
            .Setup(x => x.GetRefreshTokenAsync(invalidToken))
            .ReturnsAsync((RefreshToken?)null); // Token not found

        // Act
        var result = await _authService.RefreshTokenAsync(invalidToken);

        // Assert
        result.Should().BeNull();
        
        // Verify no new tokens were generated
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<ApplicationUser>()), Times.Never);
        _tokenServiceMock.Verify(x => x.CreateRefreshTokenAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ValidUserId_ReturnsUserDto()
    {
        // Arrange
        var userId = 7;
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = "User",
            IsActive = true,
            CreatedAt = _fixedDateTime.AddDays(-30),
            LastLoginAt = _fixedDateTime.AddHours(-1)
        };

        _userManagerMock
            .Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(x => x.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { user.Role });

        // Act
        var result = await _authService.GetCurrentUserAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(userId);
        result.Username.Should().Be(user.UserName);
        result.Email.Should().Be(user.Email);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
        result.Role.Should().Be(user.Role);
        result.IsActive.Should().Be(user.IsActive);
        result.CreatedAt.Should().Be(user.CreatedAt);
        result.LastLoginAt.Should().Be(user.LastLoginAt);
    }

    [Fact]
    public async Task GetCurrentUserAsync_UserNotFound_ReturnsNull()
    {
        // Arrange
        var nonExistentUserId = 999;

        _userManagerMock
            .Setup(x => x.FindByIdAsync(nonExistentUserId.ToString()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _authService.GetCurrentUserAsync(nonExistentUserId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_ValidRefreshToken_ReturnsTrue()
    {
        // Arrange
        var refreshToken = "valid-refresh-token";
        var storedToken = new RefreshToken { Token = refreshToken, UserId = 1 };

        _tokenServiceMock
            .Setup(x => x.GetRefreshTokenAsync(refreshToken))
            .ReturnsAsync(storedToken);

        // Act
        var result = await _authService.LogoutAsync(refreshToken);

        // Assert
        result.Should().BeTrue();
        
        // Verify token was revoked
        _tokenServiceMock.Verify(x => x.RevokeRefreshTokenAsync(refreshToken, null), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_InvalidRefreshToken_ReturnsFalse()
    {
        // Arrange
        var invalidToken = "invalid-refresh-token";

        _tokenServiceMock
            .Setup(x => x.GetRefreshTokenAsync(invalidToken))
            .ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _authService.LogoutAsync(invalidToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_EmptyRefreshToken_ReturnsFalse()
    {
        // Arrange
        var emptyToken = "";

        // Act
        var result = await _authService.LogoutAsync(emptyToken);

        // Assert
        result.Should().BeFalse();
        
        // Verify no service calls were made
        _tokenServiceMock.Verify(x => x.GetRefreshTokenAsync(It.IsAny<string>()), Times.Never);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}