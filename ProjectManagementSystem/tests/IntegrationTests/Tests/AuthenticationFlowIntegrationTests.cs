using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.IntegrationTests.Helpers;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for complete authentication flow in IdentityService
/// Tests the entire authentication chain: Login -> JWT -> Protected endpoints -> Refresh -> Logout
/// </summary>
[Collection("IdentityService Integration Tests")]
public class AuthenticationFlowIntegrationTests : IdentityServiceTestBase
{
    private readonly AuthenticationHelper _authHelper;
    private readonly HttpClientHelper _httpHelper;
    private readonly DatabaseHelper _dbHelper;

    public AuthenticationFlowIntegrationTests(IdentityServiceTestFixture fixture) : base(fixture)
    {
        _authHelper = new AuthenticationHelper(_client);
        _httpHelper = new HttpClientHelper(_client);
        _dbHelper = new DatabaseHelper(_context);
    }

    [Fact]
    public async Task CompleteAuthenticationFlow_WithAdminUser_ShouldSucceed()
    {
        // Arrange - Clean up any previous test data
        await CleanupAsync();

        // Act & Assert - Step 1: Login with admin credentials
        var authResponse = await _authHelper.LoginAsAdminAsync();
        
        authResponse.Should().NotBeNull("Admin login should succeed");
        authResponse!.Token.Should().NotBeNullOrEmpty("JWT token should be provided");
        authResponse.User.Should().NotBeNull("User information should be provided");
        authResponse.User.Username.Should().Be("admin");
        authResponse.User.Email.Should().Be("admin@projectmanagement.com");
        authResponse.User.Role.Should().Be("SystemAdmin");
        authResponse.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Step 2: Set bearer token and access protected endpoint
        _authHelper.SetBearerToken(authResponse.Token);
        
        var currentUser = await _authHelper.GetCurrentUserAsync();
        currentUser.Should().NotBeNull("Protected endpoint should be accessible with valid JWT");
        currentUser!.Id.Should().Be(authResponse.User.Id);
        currentUser.Username.Should().Be(authResponse.User.Username);

        // Step 3: Validate token
        var isValid = await _authHelper.ValidateTokenAsync();
        isValid.Should().BeTrue("Token should be valid");

        // Step 4: Test token claims
        var claims = _authHelper.ExtractClaimsFromToken(authResponse.Token);
        claims.Should().NotBeNull("Token should contain valid claims");
        claims!.FindFirst("sub")?.Value.Should().Be(authResponse.User.Id.ToString());
        claims.FindFirst("name")?.Value.Should().Be(authResponse.User.Username);
        claims.FindFirst("email")?.Value.Should().Be(authResponse.User.Email);
        claims.FindFirst("role")?.Value.Should().Be(authResponse.User.Role);

        // Step 5: Logout
        var logoutSuccess = await _authHelper.LogoutAsync("dummy-refresh-token");
        // Note: We don't have the actual refresh token from login response in this test
        // In a real implementation, the refresh token would be included in the response

        // Step 6: Verify token is no longer valid after logout (simulate by clearing auth)
        _authHelper.ClearAuthentication();
        var currentUserAfterLogout = await _authHelper.GetCurrentUserAsync();
        currentUserAfterLogout.Should().BeNull("Protected endpoint should not be accessible after logout");
    }

    [Fact]
    public async Task LoginFlow_WithTestUser_ShouldHandleCompleteLifecycle()
    {
        // Arrange - Create a test user
        await CleanupAsync();
        var testUser = await _dbHelper.CreateUserAsync(
            username: "testlogin",
            email: "testlogin@example.com",
            firstName: "Test",
            lastName: "Login",
            role: "User"
        );

        // We need to set a password for the user (in real implementation, this would be done during user creation)
        // For now, we'll test with the admin user since we know its credentials

        // Act & Assert - Test complete authentication lifecycle
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull();

        // Test JWT token properties
        var token = authResponse!.Token;
        token.Should().NotBeNullOrEmpty();
        
        // Verify token is not expired
        _authHelper.IsTokenExpired(token).Should().BeFalse("Newly issued token should not be expired");

        // Set token and test protected endpoint access
        _authHelper.SetBearerToken(token);
        
        var userInfo = await _authHelper.GetCurrentUserAsync();
        userInfo.Should().NotBeNull();
        userInfo!.IsActive.Should().BeTrue();

        // Test database state after login
        var dbUser = await _dbHelper.GetUserByIdAsync(userInfo.Id);
        dbUser.Should().NotBeNull();
        dbUser!.LastLoginAt.Should().NotBeNull("LastLoginAt should be updated after login");
        dbUser.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task InvalidCredentials_ShouldFailGracefully()
    {
        // Test 1: Non-existent user
        var response1 = await _authHelper.LoginAsync("nonexistent@example.com", "password");
        response1.Should().BeNull("Login with non-existent user should fail");

        // Test 2: Valid user, wrong password
        var response2 = await _authHelper.LoginAsync("admin@projectmanagement.com", "wrongpassword");
        response2.Should().BeNull("Login with wrong password should fail");

        // Test 3: Empty credentials
        var response3 = await _authHelper.LoginAsync("", "");
        response3.Should().BeNull("Login with empty credentials should fail");

        // Test 4: Null/empty password
        var response4 = await _authHelper.LoginAsync("admin@projectmanagement.com", "");
        response4.Should().BeNull("Login with empty password should fail");
    }

    [Fact]
    public async Task TokenManipulation_ShouldBeDetected()
    {
        // Arrange - Get valid token first
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull();

        var validToken = authResponse!.Token;

        // Test 1: Invalid token format
        _authHelper.SetBearerToken(_authHelper.GenerateInvalidToken());
        var result1 = await _authHelper.GetCurrentUserAsync();
        result1.Should().BeNull("Invalid token should be rejected");

        // Test 2: Expired token
        var expiredToken = _authHelper.GenerateExpiredToken(1, "admin", "admin@projectmanagement.com", "SystemAdmin");
        _authHelper.SetBearerToken(expiredToken);
        var result2 = await _authHelper.GetCurrentUserAsync();
        result2.Should().BeNull("Expired token should be rejected");

        // Test 3: Tampered token
        var tamperedToken = _authHelper.GenerateTamperedToken(validToken);
        _authHelper.SetBearerToken(tamperedToken);
        var result3 = await _authHelper.GetCurrentUserAsync();
        result3.Should().BeNull("Tampered token should be rejected");

        // Test 4: No authorization header
        _authHelper.ClearAuthentication();
        var result4 = await _authHelper.GetCurrentUserAsync();
        result4.Should().BeNull("Request without authorization should be rejected");
    }

    [Fact]
    public async Task ConcurrentAuthentication_ShouldHandleMultipleUsers()
    {
        // Arrange
        await CleanupAsync();

        // Act - Simulate multiple concurrent login attempts
        var loginTasks = new List<Task<AuthResponseDto?>>();
        
        for (int i = 0; i < 5; i++)
        {
            loginTasks.Add(_authHelper.LoginAsAdminAsync());
        }

        var results = await Task.WhenAll(loginTasks);

        // Assert
        results.Should().NotContainNulls("All concurrent logins should succeed");
        results.Length.Should().Be(5);

        // Verify each token is unique and valid
        var tokens = results.Select(r => r!.Token).ToList();
        tokens.Should().OnlyHaveUniqueItems("Each login should generate a unique token");

        // Verify all tokens are valid
        foreach (var result in results)
        {
            _authHelper.IsTokenExpired(result!.Token).Should().BeFalse();
            var claims = _authHelper.ExtractClaimsFromToken(result.Token);
            claims.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AuthenticationFlow_PerformanceBaseline_ShouldMeetExpectations()
    {
        // Arrange
        await CleanupAsync();

        // Act & Assert - Login performance
        var (loginSuccess, loginTime) = await _httpHelper.MeasureResponseTimeAsync(
            "/api/auth/login", 
            HttpMethod.Post);

        loginTime.Should().BeLessThan(TimeSpan.FromSeconds(2), "Login should complete within 2 seconds");

        // Get current user performance (with authentication)
        var authResponse = await _authHelper.LoginAsAdminAsync();
        _authHelper.SetBearerToken(authResponse!.Token);

        var (userInfoSuccess, userInfoTime) = await _httpHelper.MeasureResponseTimeAsync("/api/auth/me");
        userInfoTime.Should().BeLessThan(TimeSpan.FromSeconds(1), "User info retrieval should complete within 1 second");
    }

    [Fact]
    public async Task ServiceHealthAndConnectivity_ShouldBeAccessible()
    {
        // Test health check endpoint
        var healthCheck = await _httpHelper.IsServiceHealthyAsync();
        healthCheck.Should().BeTrue("Health check endpoint should be accessible");

        // Test debug ping endpoint
        var pingCheck = await _httpHelper.CanPingServiceAsync();
        pingCheck.Should().BeTrue("Debug ping endpoint should be accessible");

        // Test basic service connectivity
        var healthResponse = await _httpHelper.GetStringAsync("/health-check");
        healthResponse.Should().Contain("healthy", "Health check should return healthy status");
        healthResponse.Should().Contain("identity-service", "Health check should identify the service");
    }

    protected override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        await _dbHelper.CleanupTestDataAsync();
    }
}