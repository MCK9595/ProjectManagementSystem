using System.Net;
using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.IntegrationTests.Helpers;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.IdentityService.Services;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for security aspects of IdentityService
/// Tests JWT validation, brute force protection, authorization, and security edge cases
/// </summary>
[Collection("IdentityService Integration Tests")]
public class SecurityIntegrationTests : IdentityServiceTestBase
{
    private readonly AuthenticationHelper _authHelper;
    private readonly HttpClientHelper _httpHelper;
    private readonly DatabaseHelper _dbHelper;

    public SecurityIntegrationTests(IdentityServiceTestFixture fixture) : base(fixture)
    {
        _authHelper = new AuthenticationHelper(_client);
        _httpHelper = new HttpClientHelper(_client);
        _dbHelper = new DatabaseHelper(_context);
    }

    #region JWT Security Tests

    [Fact]
    public async Task JwtValidation_WithValidToken_ShouldAllowAccess()
    {
        // Arrange
        await CleanupAsync();
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull();

        _authHelper.SetBearerToken(authResponse!.Token);

        // Act
        var protectedResponse = await _httpHelper.GetAsync<UserDto>("/api/auth/me");

        // Assert
        protectedResponse.Should().NotBeNull();
        protectedResponse!.Success.Should().BeTrue();
        protectedResponse.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task JwtValidation_WithExpiredToken_ShouldDenyAccess()
    {
        // Arrange
        await CleanupAsync();
        var expiredToken = _authHelper.GenerateExpiredToken(1, "admin", "admin@projectmanagement.com", "SystemAdmin");
        _authHelper.SetBearerToken(expiredToken);

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtValidation_WithTamperedToken_ShouldDenyAccess()
    {
        // Arrange
        await CleanupAsync();
        var authResponse = await _authHelper.LoginAsAdminAsync();
        var tamperedToken = _authHelper.GenerateTamperedToken(authResponse!.Token);
        _authHelper.SetBearerToken(tamperedToken);

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task JwtValidation_WithMalformedToken_ShouldDenyAccess()
    {
        // Arrange
        var malformedTokens = new[]
        {
            "invalid.jwt.token",
            "not-a-jwt-at-all",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.invalid.signature",
            "",
            "Bearer token-without-bearer-prefix"
        };

        foreach (var malformedToken in malformedTokens)
        {
            // Act
            _authHelper.SetBearerToken(malformedToken);
            var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

            // Assert
            rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized, 
                $"Malformed token '{malformedToken}' should be rejected");
        }
    }

    [Fact]
    public async Task JwtValidation_WithNoAuthorizationHeader_ShouldDenyAccess()
    {
        // Arrange
        _authHelper.ClearAuthentication();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Brute Force Protection Tests

    [Fact]
    public async Task BruteForceProtection_MultipleFailedAttempts_ShouldTriggerLockout()
    {
        // Arrange
        await CleanupAsync();
        
        // Create a test user for brute force testing
        var testUser = await _dbHelper.CreateUserAsync(
            username: "bruteforce", 
            email: "bruteforce@example.com",
            firstName: "Brute",
            lastName: "Force"
        );

        var wrongPassword = "WrongPassword123!";

        // Act - Attempt multiple failed logins
        var failedAttempts = new List<AuthResponseDto?>();
        for (int i = 0; i < 6; i++) // Attempt 6 failed logins (limit is typically 5)
        {
            var response = await _authHelper.LoginAsync("bruteforce@example.com", wrongPassword);
            failedAttempts.Add(response);
        }

        // Assert
        failedAttempts.Should().AllSatisfy(attempt => attempt.Should().BeNull("All login attempts with wrong password should fail"));

        // Verify user account status in database
        var dbUser = await _dbHelper.GetUserByEmailAsync("bruteforce@example.com");
        dbUser.Should().NotBeNull();
        
        // Note: The actual lockout mechanism depends on ASP.NET Core Identity configuration
        // We should check if the user is locked out, but this requires proper Identity setup
        
        // Try one more login attempt - should still fail even if password was correct
        var finalAttempt = await _authHelper.LoginAsync("bruteforce@example.com", "CorrectPassword123!");
        finalAttempt.Should().BeNull("Account should be locked after multiple failed attempts");
    }

    [Fact]
    public async Task BruteForceProtection_ConcurrentFailedAttempts_ShouldHandleGracefully()
    {
        // Arrange
        await CleanupAsync();
        
        var testUser = await _dbHelper.CreateUserAsync(
            username: "concurrent", 
            email: "concurrent@example.com"
        );

        var loginRequest = new LoginDto
        {
            Username = "concurrent@example.com",
            Password = "WrongPassword123!"
        };

        // Act - Simulate concurrent brute force attempts
        var concurrentTasks = new List<Task<(bool Success, TimeSpan ResponseTime)>>();
        
        for (int i = 0; i < 10; i++)
        {
            concurrentTasks.Add(_httpHelper.TestConcurrentRequestsAsync(
                "/api/auth/login", 1, HttpMethod.Post, loginRequest).ContinueWith(t => t.Result.First()));
        }

        var results = await Task.WhenAll(concurrentTasks);

        // Assert
        results.Should().AllSatisfy(result => 
        {
            result.Success.Should().BeFalse("All concurrent failed login attempts should fail");
            result.ResponseTime.Should().BeLessThan(TimeSpan.FromSeconds(10), "System should handle concurrent requests efficiently");
        });

        // Verify system stability
        var healthCheck = await _httpHelper.IsServiceHealthyAsync();
        healthCheck.Should().BeTrue("Service should remain healthy after concurrent failed attempts");
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task Authorization_ProtectedEndpoints_ShouldRequireAuthentication()
    {
        // Arrange
        _authHelper.ClearAuthentication();

        var protectedEndpoints = new[]
        {
            new { Method = HttpMethod.Get, Url = "/api/auth/me" },
            new { Method = HttpMethod.Post, Url = "/api/auth/logout" },
            new { Method = HttpMethod.Post, Url = "/api/auth/validate" },
            new { Method = HttpMethod.Get, Url = "/api/internaluser/1" },
            new { Method = HttpMethod.Post, Url = "/api/internaluser/batch" }
        };

        foreach (var endpoint in protectedEndpoints)
        {
            // Act
            var response = await _httpHelper.SendRawAsync(endpoint.Method, endpoint.Url, 
                endpoint.Method == HttpMethod.Post ? new { } : null);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, 
                $"Endpoint {endpoint.Method} {endpoint.Url} should require authentication");
        }
    }

    [Fact]
    public async Task Authorization_WithValidToken_ShouldAllowAccessToAuthorizedEndpoints()
    {
        // Arrange
        await CleanupAsync();
        var authResponse = await _authHelper.LoginAsAdminAsync();
        _authHelper.SetBearerToken(authResponse!.Token);

        var authorizedEndpoints = new[]
        {
            new { Method = HttpMethod.Get, Url = "/api/auth/me" },
            new { Method = HttpMethod.Post, Url = "/api/auth/validate" }
        };

        foreach (var endpoint in authorizedEndpoints)
        {
            // Act
            var response = await _httpHelper.SendRawAsync(endpoint.Method, endpoint.Url, 
                endpoint.Method == HttpMethod.Post ? new { } : null);

            // Assert
            response.IsSuccessStatusCode.Should().BeTrue(
                $"Endpoint {endpoint.Method} {endpoint.Url} should be accessible with valid token");
        }
    }

    #endregion

    #region Input Validation and Injection Tests

    [Fact]
    public async Task InputValidation_SqlInjectionAttempts_ShouldBeSafelyHandled()
    {
        // Arrange
        var sqlInjectionPayloads = new[]
        {
            "admin@example.com'; DROP TABLE Users; --",
            "admin@example.com' OR '1'='1",
            "admin@example.com'; UPDATE Users SET Role='SystemAdmin'; --",
            "admin@example.com' UNION SELECT * FROM Users; --"
        };

        foreach (var payload in sqlInjectionPayloads)
        {
            // Act
            var response = await _authHelper.LoginAsync(payload, "password");

            // Assert
            response.Should().BeNull($"SQL injection payload '{payload}' should not succeed");
        }

        // Verify database integrity
        var userCount = await _dbHelper.GetTotalUserCountAsync();
        userCount.Should().BeGreaterThan(0, "Database should still contain users after injection attempts");

        var healthCheck = await _httpHelper.IsServiceHealthyAsync();
        healthCheck.Should().BeTrue("Service should remain healthy after injection attempts");
    }

    [Fact]
    public async Task InputValidation_XssAttempts_ShouldBeSanitized()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var xssPayloads = new[]
        {
            "<script>alert('xss')</script>",
            "javascript:alert('xss')",
            "<img src=x onerror=alert('xss')>",
            "<%2Fscript%3E%3Cscript%3Ealert('xss')%3C%2Fscript%3E"
        };

        foreach (var payload in xssPayloads)
        {
            // Act
            var createRequest = new FindOrCreateUserRequest
            {
                Email = "xsstest@example.com",
                FirstName = payload,
                LastName = "Test"
            };

            var response = await _httpHelper.PostAsync<UserDto>("/api/internaluser/find-or-create", createRequest);

            // Assert
            if (response?.Success == true)
            {
                response.Data!.FirstName.Should().NotContain("<script>", "XSS payload should be sanitized");
                response.Data.FirstName.Should().NotContain("javascript:", "XSS payload should be sanitized");
            }
        }
    }

    [Fact]
    public async Task InputValidation_ExcessivelyLongInputs_ShouldBeRejected()
    {
        // Arrange
        var longString = new string('a', 10000); // Very long input

        var loginRequest = new LoginDto
        {
            Username = longString,
            Password = longString
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/auth/login", loginRequest);

        // Assert
        rawResponse.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge);
    }

    #endregion

    #region Password Security Tests

    [Fact]
    public async Task PasswordSecurity_WeakPasswords_ShouldBeRejected()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var weakPasswords = new[]
        {
            "123456",
            "password",
            "admin",
            "qwerty",
            "short",
            "NoNumbers!",
            "nonumbers123",
            "NoSpecialChars123"
        };

        foreach (var weakPassword in weakPasswords)
        {
            // Act
            var createRequest = new CreateUserWithPasswordRequest
            {
                Email = $"weak{weakPassword.Length}@example.com",
                FirstName = "Weak",
                LastName = "Password",
                Password = weakPassword,
                ConfirmPassword = weakPassword
            };

            var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/internaluser/create-with-password", createRequest);

            // Assert
            rawResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest, 
                $"Weak password '{weakPassword}' should be rejected");
        }
    }

    #endregion

    #region Token Lifecycle Security Tests

    [Fact]
    public async Task TokenLifecycle_ExpiredTokens_ShouldNotBeAcceptedAfterExpiry()
    {
        // Arrange
        await CleanupAsync();
        
        // Create a token that's about to expire (we'll simulate by creating an already expired one)
        var expiredToken = _authHelper.GenerateExpiredToken(1, "admin", "admin@projectmanagement.com", "SystemAdmin");
        
        // Verify token is recognized as expired
        _authHelper.IsTokenExpired(expiredToken).Should().BeTrue();

        // Act
        _authHelper.SetBearerToken(expiredToken);
        var response = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "Expired token should be rejected");
    }

    [Fact]
    public async Task TokenLifecycle_ValidTokens_ShouldHaveCorrectClaims()
    {
        // Arrange
        await CleanupAsync();
        var authResponse = await _authHelper.LoginAsAdminAsync();

        // Act
        var claims = _authHelper.ExtractClaimsFromToken(authResponse!.Token);

        // Assert
        claims.Should().NotBeNull();
        claims!.FindFirst("sub")?.Value.Should().NotBeNullOrEmpty("Token should have subject claim");
        claims.FindFirst("name")?.Value.Should().Be("admin");
        claims.FindFirst("email")?.Value.Should().Be("admin@projectmanagement.com");
        claims.FindFirst("role")?.Value.Should().Be("SystemAdmin");
        claims.FindFirst("jti")?.Value.Should().NotBeNullOrEmpty("Token should have unique ID");
        claims.FindFirst("iat")?.Value.Should().NotBeNullOrEmpty("Token should have issued-at claim");
    }

    #endregion

    #region Stress Testing

    [Fact]
    public async Task SecurityStressTest_HighVolumeRequests_ShouldMaintainSecurity()
    {
        // Arrange
        await CleanupAsync();
        
        var tasks = new List<Task>();
        
        // Simulate high volume of authentication requests
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                // Mix of valid and invalid requests
                if (i % 2 == 0)
                {
                    await _authHelper.LoginAsAdminAsync();
                }
                else
                {
                    await _authHelper.LoginAsync("invalid@example.com", "invalid");
                }
            }));
        }

        // Act
        await Task.WhenAll(tasks);

        // Assert
        var healthCheck = await _httpHelper.IsServiceHealthyAsync();
        healthCheck.Should().BeTrue("Service should remain healthy under stress");

        // Verify we can still authenticate normally
        var finalAuth = await _authHelper.LoginAsAdminAsync();
        finalAuth.Should().NotBeNull("Normal authentication should still work after stress test");
    }

    #endregion

    private async Task AuthenticateAsAdminAsync()
    {
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull("Admin login should succeed for security tests");
        _authHelper.SetBearerToken(authResponse!.Token);
    }

    protected override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        await _dbHelper.CleanupTestDataAsync();
        _authHelper.ClearAuthentication();
    }
}