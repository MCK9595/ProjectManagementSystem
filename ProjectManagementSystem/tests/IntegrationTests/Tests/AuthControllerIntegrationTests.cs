using System.Net;
using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.IntegrationTests.Helpers;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for AuthController endpoints
/// Tests all 5 endpoints: /login, /refresh, /logout, /me, /validate
/// </summary>
[Collection("IdentityService Integration Tests")]
public class AuthControllerIntegrationTests : IdentityServiceTestBase
{
    private readonly AuthenticationHelper _authHelper;
    private readonly HttpClientHelper _httpHelper;
    private readonly DatabaseHelper _dbHelper;

    public AuthControllerIntegrationTests(IdentityServiceTestFixture fixture) : base(fixture)
    {
        _authHelper = new AuthenticationHelper(_client);
        _httpHelper = new HttpClientHelper(_client);
        _dbHelper = new DatabaseHelper(_context);
    }

    #region POST /api/auth/login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnSuccessResponse()
    {
        // Arrange
        await CleanupAsync();
        var loginRequest = new LoginDto
        {
            Username = "admin@projectmanagement.com",
            Password = "AdminPassword123!"
        };

        // Act
        var response = await _httpHelper.PostAsync<AuthResponseDto>("/api/auth/login", loginRequest);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("Login successful");
        response.Data.Should().NotBeNull();
        response.Data!.Token.Should().NotBeNullOrEmpty();
        response.Data.User.Should().NotBeNull();
        response.Data.User.Username.Should().Be("admin");
        response.Data.User.Email.Should().Be("admin@projectmanagement.com");
        response.Data.User.Role.Should().Be("SystemAdmin");
        response.Data.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Verify JWT token structure
        var claims = _authHelper.ExtractClaimsFromToken(response.Data.Token);
        claims.Should().NotBeNull();
        claims!.FindFirst("sub")?.Value.Should().Be(response.Data.User.Id.ToString());
        claims.FindFirst("name")?.Value.Should().Be(response.Data.User.Username);
    }

    [Fact]
    public async Task Login_WithInvalidUsername_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginDto
        {
            Username = "nonexistent@example.com",
            Password = "SomePassword123!"
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/login", loginRequest);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginDto
        {
            Username = "admin@projectmanagement.com",
            Password = "WrongPassword123!"
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/login", loginRequest);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_ShouldReturnBadRequest()
    {
        // Arrange
        var loginRequest = new LoginDto
        {
            Username = "",
            Password = ""
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/login", loginRequest);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid input");
    }

    [Fact]
    public async Task Login_ShouldUpdateLastLoginTimestamp()
    {
        // Arrange
        await CleanupAsync();
        var beforeLogin = DateTime.UtcNow;

        var loginRequest = new LoginDto
        {
            Username = "admin@projectmanagement.com",
            Password = "AdminPassword123!"
        };

        // Act
        var response = await _httpHelper.PostAsync<AuthResponseDto>("/api/auth/login", loginRequest);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();

        // Verify database was updated
        var user = await _dbHelper.GetUserByEmailAsync("admin@projectmanagement.com");
        user.Should().NotBeNull();
        user!.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeAfter(beforeLogin);
        user.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region POST /api/auth/refresh Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldReturnNewTokens()
    {
        // Arrange
        await CleanupAsync();
        
        // First, login to get a refresh token
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull();

        // For this test, we'll create a mock refresh token in the database
        var user = await _dbHelper.GetUserByEmailAsync("admin@projectmanagement.com");
        var refreshToken = await _dbHelper.CreateRefreshTokenAsync(user!.Id, "test-refresh-token");

        var refreshRequest = new { RefreshToken = refreshToken.Token };

        // Act
        var response = await _httpHelper.PostAsync<AuthResponseDto>("/api/auth/refresh", refreshRequest);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("Token refreshed successfully");
        response.Data.Should().NotBeNull();
        response.Data!.Token.Should().NotBeNullOrEmpty();
        response.Data.Token.Should().NotBe(authResponse!.Token, "New token should be different from original");
        response.Data.User.Should().NotBeNull();
        response.Data.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var refreshRequest = new { RefreshToken = "invalid-refresh-token" };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/refresh", refreshRequest);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid or expired refresh token");
    }

    [Fact]
    public async Task RefreshToken_WithEmptyToken_ShouldReturnBadRequest()
    {
        // Arrange
        var refreshRequest = new { RefreshToken = "" };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/refresh", refreshRequest);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Refresh token is required");
    }

    #endregion

    #region POST /api/auth/logout Tests

    [Fact]
    public async Task Logout_WithValidTokenAndAuth_ShouldReturnSuccess()
    {
        // Arrange
        await CleanupAsync();
        
        var authResponse = await _authHelper.LoginAsAdminAsync();
        _authHelper.SetBearerToken(authResponse!.Token);

        // Create a mock refresh token
        var user = await _dbHelper.GetUserByEmailAsync("admin@projectmanagement.com");
        var refreshToken = await _dbHelper.CreateRefreshTokenAsync(user!.Id, "logout-test-token");

        var logoutRequest = new { RefreshToken = refreshToken.Token };

        // Act
        var response = await _httpHelper.PostAsync<object>("/api/auth/logout", logoutRequest);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("Logout successful");
    }

    [Fact]
    public async Task Logout_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        // Arrange
        _authHelper.ClearAuthentication();
        var logoutRequest = new { RefreshToken = "some-token" };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/logout", logoutRequest);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GET /api/auth/me Tests

    [Fact]
    public async Task GetCurrentUser_WithValidAuth_ShouldReturnUserInfo()
    {
        // Arrange
        await CleanupAsync();
        
        var authResponse = await _authHelper.LoginAsAdminAsync();
        _authHelper.SetBearerToken(authResponse!.Token);

        // Act
        var response = await _httpHelper.GetAsync<UserDto>("/api/auth/me");

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("User information retrieved successfully");
        response.Data.Should().NotBeNull();
        response.Data!.Id.Should().Be(authResponse.User.Id);
        response.Data.Username.Should().Be("admin");
        response.Data.Email.Should().Be("admin@projectmanagement.com");
        response.Data.Role.Should().Be("SystemAdmin");
        response.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentUser_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        _authHelper.ClearAuthentication();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentUser_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        _authHelper.SetBearerToken("invalid.jwt.token");

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region POST /api/auth/validate Tests

    [Fact]
    public async Task ValidateToken_WithValidAuth_ShouldReturnTokenInfo()
    {
        // Arrange
        await CleanupAsync();
        
        var authResponse = await _authHelper.LoginAsAdminAsync();
        _authHelper.SetBearerToken(authResponse!.Token);

        // Act
        var response = await _httpHelper.PostAsync<object>("/api/auth/validate");

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Message.Should().Be("Token is valid");
        response.Data.Should().NotBeNull();
        
        // The response should contain token information
        var tokenInfo = response.Data.ToString();
        tokenInfo.Should().Contain(authResponse.User.Id.ToString());
        tokenInfo.Should().Contain("admin");
        tokenInfo.Should().Contain("SystemAdmin");
    }

    [Fact]
    public async Task ValidateToken_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        _authHelper.ClearAuthentication();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/validate");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ValidateToken_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var expiredToken = _authHelper.GenerateExpiredToken(1, "admin", "admin@projectmanagement.com", "SystemAdmin");
        _authHelper.SetBearerToken(expiredToken);

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/validate");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Performance and Load Tests

    [Fact]
    public async Task AuthEndpoints_PerformanceTest_ShouldMeetBaseline()
    {
        // Arrange
        await CleanupAsync();
        
        var loginRequest = new LoginDto
        {
            Username = "admin@projectmanagement.com",
            Password = "AdminPassword123!"
        };

        // Test login performance
        var (loginSuccess, loginTime) = await _httpHelper.MeasureResponseTimeAsync(
            "/api/auth/login", HttpMethod.Post);
        
        loginTime.Should().BeLessThan(TimeSpan.FromSeconds(3), "Login should complete within 3 seconds");

        // Test authenticated endpoint performance
        var authResponse = await _authHelper.LoginAsAdminAsync();
        _authHelper.SetBearerToken(authResponse!.Token);

        var (meSuccess, meTime) = await _httpHelper.MeasureResponseTimeAsync("/api/auth/me");
        meTime.Should().BeLessThan(TimeSpan.FromSeconds(1), "User info should load within 1 second");

        var (validateSuccess, validateTime) = await _httpHelper.MeasureResponseTimeAsync(
            "/api/auth/validate", HttpMethod.Post);
        validateTime.Should().BeLessThan(TimeSpan.FromMilliseconds(500), "Token validation should be very fast");
    }

    [Fact]
    public async Task ConcurrentLogin_ShouldHandleMultipleRequests()
    {
        // Arrange
        await CleanupAsync();
        
        var loginRequest = new LoginDto
        {
            Username = "admin@projectmanagement.com",
            Password = "AdminPassword123!"
        };

        // Act - Test 10 concurrent login requests
        var results = await _httpHelper.TestConcurrentRequestsAsync(
            "/api/auth/login", 10, HttpMethod.Post, loginRequest);

        // Assert
        results.Should().HaveCount(10);
        results.All(r => r.Success).Should().BeTrue("All concurrent logins should succeed");
        results.All(r => r.ResponseTime < TimeSpan.FromSeconds(5)).Should().BeTrue("All requests should complete within 5 seconds");
    }

    #endregion

    protected override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        await _dbHelper.CleanupTestDataAsync();
        _authHelper.ClearAuthentication();
    }
}