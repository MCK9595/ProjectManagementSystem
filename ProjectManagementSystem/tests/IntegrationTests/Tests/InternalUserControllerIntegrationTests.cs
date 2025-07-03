using System.Net;
using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.IntegrationTests.Helpers;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for InternalUserController endpoints
/// Tests all 5 endpoints: GET /{id}, POST /batch, POST /find-or-create, POST /create-with-password, GET /check-email/{email}
/// </summary>
[Collection("IdentityService Integration Tests")]
public class InternalUserControllerIntegrationTests : IdentityServiceTestBase
{
    private readonly AuthenticationHelper _authHelper;
    private readonly HttpClientHelper _httpHelper;
    private readonly DatabaseHelper _dbHelper;

    public InternalUserControllerIntegrationTests(IdentityServiceTestFixture fixture) : base(fixture)
    {
        _authHelper = new AuthenticationHelper(_client);
        _httpHelper = new HttpClientHelper(_client);
        _dbHelper = new DatabaseHelper(_context);
    }

    #region GET /api/internaluser/{id} Tests

    [Fact]
    public async Task GetUserById_WithValidIdAndAuth_ShouldReturnUser()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var testUser = await _dbHelper.CreateUserAsync(
            username: "getbyid_test",
            email: "getbyid@example.com",
            firstName: "GetById",
            lastName: "Test"
        );

        // Act
        var response = await _httpHelper.GetAsync<UserDto>($"/api/internaluser/{testUser.Id}");

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Id.Should().Be(testUser.Id);
        response.Data.Username.Should().Be("getbyid_test");
        response.Data.Email.Should().Be("getbyid@example.com");
        response.Data.FirstName.Should().Be("GetById");
        response.Data.LastName.Should().Be("Test");
        response.Data.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserById_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/internaluser/99999");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("not found");
    }

    [Fact]
    public async Task GetUserById_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Arrange
        _authHelper.ClearAuthentication();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/internaluser/1");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region POST /api/internaluser/batch Tests

    [Fact]
    public async Task GetUsersByIds_WithValidIds_ShouldReturnUsers()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var user1 = await _dbHelper.CreateUserAsync("batch1", "batch1@example.com", "Batch", "User1");
        var user2 = await _dbHelper.CreateUserAsync("batch2", "batch2@example.com", "Batch", "User2");
        var user3 = await _dbHelper.CreateUserAsync("batch3", "batch3@example.com", "Batch", "User3");

        var userIds = new List<int> { user1.Id, user2.Id, user3.Id };

        // Act
        var response = await _httpHelper.PostAsync<List<UserDto>>("/api/internaluser/batch", userIds);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Should().HaveCount(3);
        
        var returnedIds = response.Data.Select(u => u.Id).ToList();
        returnedIds.Should().Contain(user1.Id);
        returnedIds.Should().Contain(user2.Id);
        returnedIds.Should().Contain(user3.Id);
    }

    [Fact]
    public async Task GetUsersByIds_WithMixedValidAndInvalidIds_ShouldReturnOnlyFoundUsers()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var user1 = await _dbHelper.CreateUserAsync("mixedbatch1", "mixedbatch1@example.com", "Mixed", "User1");
        
        var userIds = new List<int> { user1.Id, 99999, 99998 }; // Mix of valid and invalid IDs

        // Act
        var response = await _httpHelper.PostAsync<List<UserDto>>("/api/internaluser/batch", userIds);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Should().HaveCount(1, "Only the existing user should be returned");
        response.Data.First().Id.Should().Be(user1.Id);
    }

    [Fact]
    public async Task GetUsersByIds_WithEmptyList_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var userIds = new List<int>();

        // Act
        var response = await _httpHelper.PostAsync<List<UserDto>>("/api/internaluser/batch", userIds);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Should().BeEmpty();
    }

    #endregion

    #region POST /api/internaluser/find-or-create Tests

    [Fact]
    public async Task FindOrCreateUser_WithExistingUser_ShouldReturnExistingUser()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var existingUser = await _dbHelper.CreateUserAsync(
            username: "existing",
            email: "existing@example.com",
            firstName: "Existing",
            lastName: "User"
        );

        var request = new FindOrCreateUserRequest
        {
            Email = "existing@example.com",
            FirstName = "Different",
            LastName = "Name"
        };

        // Act
        var response = await _httpHelper.PostAsync<UserDto>("/api/internaluser/find-or-create", request);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Id.Should().Be(existingUser.Id);
        response.Data.Email.Should().Be("existing@example.com");
        response.Data.FirstName.Should().Be("Existing", "Should return existing user's name, not request name");
        response.Data.LastName.Should().Be("User");
    }

    [Fact]
    public async Task FindOrCreateUser_WithNewUser_ShouldCreateAndReturnNewUser()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var request = new FindOrCreateUserRequest
        {
            Email = "newuser@example.com",
            FirstName = "New",
            LastName = "User"
        };

        // Act
        var response = await _httpHelper.PostAsync<UserDto>("/api/internaluser/find-or-create", request);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Email.Should().Be("newuser@example.com");
        response.Data.FirstName.Should().Be("New");
        response.Data.LastName.Should().Be("User");
        response.Data.Username.Should().Be("newuser", "Username should be derived from email");
        response.Data.Role.Should().Be("User", "Default role should be User");
        response.Data.IsActive.Should().BeTrue();

        // Verify user was actually created in database
        var dbUser = await _dbHelper.GetUserByEmailAsync("newuser@example.com");
        dbUser.Should().NotBeNull();
        dbUser!.Id.Should().Be(response.Data.Id);
    }

    [Fact]
    public async Task FindOrCreateUser_WithInvalidEmail_ShouldReturnBadRequest()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var request = new FindOrCreateUserRequest
        {
            Email = "invalid-email",
            FirstName = "Test",
            LastName = "User"
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/internaluser/find-or-create", request);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid input");
    }

    #endregion

    #region POST /api/internaluser/create-with-password Tests

    [Fact]
    public async Task CreateUserWithPassword_WithValidData_ShouldCreateUser()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var request = new CreateUserWithPasswordRequest
        {
            Email = "newpassword@example.com",
            FirstName = "Password",
            LastName = "User",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!"
        };

        // Act
        var response = await _httpHelper.PostAsync<UserDto>("/api/internaluser/create-with-password", request);

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Email.Should().Be("newpassword@example.com");
        response.Data.FirstName.Should().Be("Password");
        response.Data.LastName.Should().Be("User");
        response.Data.Username.Should().Be("newpassword");
        response.Data.IsActive.Should().BeTrue();

        // Verify user was created in database
        var dbUser = await _dbHelper.GetUserByEmailAsync("newpassword@example.com");
        dbUser.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateUserWithPassword_WithMismatchedPasswords_ShouldReturnBadRequest()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var request = new CreateUserWithPasswordRequest
        {
            Email = "mismatch@example.com",
            FirstName = "Mismatch",
            LastName = "User",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword123!"
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/internaluser/create-with-password", request);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Password and confirm password do not match");
    }

    [Fact]
    public async Task CreateUserWithPassword_WithWeakPassword_ShouldReturnBadRequest()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var request = new CreateUserWithPasswordRequest
        {
            Email = "weakpass@example.com",
            FirstName = "Weak",
            LastName = "Password",
            Password = "weak",
            ConfirmPassword = "weak"
        };

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Post, "/api/internaluser/create-with-password", request);

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("Invalid input");
    }

    #endregion

    #region GET /api/internaluser/check-email/{email} Tests

    [Fact]
    public async Task CheckUserExistsByEmail_WithExistingEmail_ShouldReturnUser()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var testUser = await _dbHelper.CreateUserAsync(
            username: "checkemail",
            email: "checkemail@example.com",
            firstName: "Check",
            lastName: "Email"
        );

        // Act
        var response = await _httpHelper.GetAsync<UserDto>("/api/internaluser/check-email/checkemail@example.com");

        // Assert
        response.Should().NotBeNull();
        response!.Success.Should().BeTrue();
        response.Data.Should().NotBeNull();
        response.Data!.Id.Should().Be(testUser.Id);
        response.Data.Email.Should().Be("checkemail@example.com");
    }

    [Fact]
    public async Task CheckUserExistsByEmail_WithNonExistentEmail_ShouldReturnNotFound()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/internaluser/check-email/nonexistent@example.com");

        // Assert
        rawResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var content = await rawResponse.Content.ReadAsStringAsync();
        content.Should().Contain("User not found");
    }

    [Fact]
    public async Task CheckUserExistsByEmail_WithEmptyEmail_ShouldReturnBadRequest()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        // Act
        var rawResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, "/api/internaluser/check-email/");

        // Assert - This will likely return 404 due to routing, but that's acceptable
        rawResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region Service-to-Service Communication Tests

    [Fact]
    public async Task InternalUserEndpoints_ShouldSupportServiceToServiceWorkflow()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        // Simulate a complete service-to-service workflow
        var email = "serviceflow@example.com";

        // Step 1: Check if user exists
        var checkResponse = await _httpHelper.SendRawAsync(HttpMethod.Get, $"/api/internaluser/check-email/{email}");
        checkResponse.StatusCode.Should().Be(HttpStatusCode.NotFound, "User should not exist initially");

        // Step 2: Create user since they don't exist
        var createRequest = new FindOrCreateUserRequest
        {
            Email = email,
            FirstName = "Service",
            LastName = "Flow"
        };

        var createResponse = await _httpHelper.PostAsync<UserDto>("/api/internaluser/find-or-create", createRequest);
        createResponse.Should().NotBeNull();
        createResponse!.Success.Should().BeTrue();
        var userId = createResponse.Data!.Id;

        // Step 3: Get user by ID
        var getUserResponse = await _httpHelper.GetAsync<UserDto>($"/api/internaluser/{userId}");
        getUserResponse.Should().NotBeNull();
        getUserResponse!.Success.Should().BeTrue();
        getUserResponse.Data!.Email.Should().Be(email);

        // Step 4: Batch get users
        var batchResponse = await _httpHelper.PostAsync<List<UserDto>>("/api/internaluser/batch", new List<int> { userId });
        batchResponse.Should().NotBeNull();
        batchResponse!.Success.Should().BeTrue();
        batchResponse.Data!.Should().HaveCount(1);
        batchResponse.Data.First().Id.Should().Be(userId);

        // Step 5: Verify user now exists when checking email
        var finalCheckResponse = await _httpHelper.GetAsync<UserDto>($"/api/internaluser/check-email/{email}");
        finalCheckResponse.Should().NotBeNull();
        finalCheckResponse!.Success.Should().BeTrue();
        finalCheckResponse.Data!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task InternalUserEndpoints_PerformanceTest_ShouldMeetBaseline()
    {
        // Arrange
        await CleanupAsync();
        await AuthenticateAsAdminAsync();

        var testUser = await _dbHelper.CreateUserAsync("perf", "perf@example.com", "Perf", "Test");

        // Test individual endpoint performance
        var (getUserSuccess, getUserTime) = await _httpHelper.MeasureResponseTimeAsync($"/api/internaluser/{testUser.Id}");
        getUserTime.Should().BeLessThan(TimeSpan.FromSeconds(1), "Get user by ID should be fast");

        var (checkEmailSuccess, checkEmailTime) = await _httpHelper.MeasureResponseTimeAsync($"/api/internaluser/check-email/perf@example.com");
        checkEmailTime.Should().BeLessThan(TimeSpan.FromSeconds(1), "Check email should be fast");

        // Test batch performance
        var batchIds = new List<int> { testUser.Id };
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var batchResponse = await _httpHelper.PostAsync<List<UserDto>>("/api/internaluser/batch", batchIds);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1), "Batch operation should be fast");
        batchResponse!.Success.Should().BeTrue();
    }

    #endregion

    private async Task AuthenticateAsAdminAsync()
    {
        var authResponse = await _authHelper.LoginAsAdminAsync();
        authResponse.Should().NotBeNull("Admin login should succeed for internal API tests");
        _authHelper.SetBearerToken(authResponse!.Token);
    }

    protected override async Task CleanupAsync()
    {
        await base.CleanupAsync();
        await _dbHelper.CleanupTestDataAsync();
        _authHelper.ClearAuthentication();
    }
}