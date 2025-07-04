using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Abstractions;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.IdentityService.Tests.Services;

public class UserDeletionValidationTests
{
    private readonly Mock<IUserManagementService> _mockUserManagementService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<UserDeletionService>> _mockLogger;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly UserDeletionService _service;

    public UserDeletionValidationTests()
    {
        _mockUserManagementService = new Mock<IUserManagementService>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        _mockLogger = new Mock<ILogger<UserDeletionService>>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

        var httpClient = new HttpClient(_mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri("https://test-service/")
        };

        _mockHttpClientFactory.Setup(x => x.CreateClient("OrganizationService")).Returns(httpClient);
        _mockHttpClientFactory.Setup(x => x.CreateClient("ProjectService")).Returns(httpClient);
        _mockHttpClientFactory.Setup(x => x.CreateClient("TaskService")).Returns(httpClient);

        _service = new UserDeletionService(
            _mockUserManagementService.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            _mockDateTimeProvider.Object,
            _mockHttpClientFactory.Object);
    }

    [Theory]
    [InlineData(123, 123)] // Self deletion
    [InlineData(456, 789)] // Last SystemAdmin
    public async Task ValidateUserDeletionAsync_CannotDelete_ReturnsError(int userId, int currentUserId)
    {
        // Arrange
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cannot delete this user", result.Message);
    }

    [Fact]
    public async Task ValidateUserDeletionAsync_UserNotFound_ReturnsError()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync((ApplicationUser)null);

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Message);
    }

    [Fact]
    public async Task ValidateUserDeletionAsync_HasBlockingOrganizationRoles_ReturnsError()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = Roles.OrganizationAdmin
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupBlockingOrganizationRoles();

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("sole administrator", result.Message);
    }

    [Fact]
    public async Task ValidateUserDeletionAsync_HasBlockingProjectRoles_ReturnsError()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = Roles.ProjectManager
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupNoBlockingOrganizationRoles();
        SetupBlockingProjectRoles();

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("sole administrator", result.Message);
    }

    [Fact]
    public async Task ValidateUserDeletionAsync_NoBlockingRoles_ReturnsSuccess()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = Roles.OrganizationMember
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupNoBlockingOrganizationRoles();
        SetupNoBlockingProjectRoles();

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("can be safely deleted", result.Message);
    }

    [Fact]
    public async Task ValidateUserDeletionAsync_OrganizationServiceError_ReturnsSuccess()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = Roles.OrganizationMember
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupServiceError();

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        // HTTP errors are treated as "no blocking roles found" so validation succeeds
        Assert.True(result.Success);
        Assert.Contains("can be safely deleted", result.Message);
    }

    [Theory]
    [InlineData(Roles.SystemAdmin)]
    [InlineData(Roles.OrganizationOwner)]
    [InlineData(Roles.OrganizationAdmin)]
    [InlineData(Roles.OrganizationMember)]
    [InlineData(Roles.ProjectManager)]
    [InlineData(Roles.ProjectMember)]
    public async Task ValidateUserDeletionAsync_DifferentUserRoles_ValidatesCorrectly(string userRole)
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        var user = new ApplicationUser 
        { 
            Id = userId, 
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Role = userRole
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupNoBlockingOrganizationRoles();
        SetupNoBlockingProjectRoles();

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.True(result.Success);
    }

    private void SetupBlockingOrganizationRoles()
    {
        var errorResponse = new ApiResponse<object> 
        { 
            Success = false, 
            Message = "User is the sole administrator of one or more organizations" 
        };
        var json = JsonSerializer.Serialize(errorResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("organizations") && 
                    req.RequestUri.ToString().Contains("admin-roles")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }

    private void SetupBlockingProjectRoles()
    {
        var errorResponse = new ApiResponse<object> 
        { 
            Success = false, 
            Message = "User is the sole administrator of one or more projects" 
        };
        var json = JsonSerializer.Serialize(errorResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("projects") && 
                    req.RequestUri.ToString().Contains("admin-roles")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }

    private void SetupNoBlockingOrganizationRoles()
    {
        var successResponse = new ApiResponse<object> 
        { 
            Success = true, 
            Message = "No blocking admin roles found" 
        };
        var json = JsonSerializer.Serialize(successResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("organizations") && 
                    req.RequestUri.ToString().Contains("admin-roles")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }

    private void SetupNoBlockingProjectRoles()
    {
        var successResponse = new ApiResponse<object> 
        { 
            Success = true, 
            Message = "No blocking admin roles found" 
        };
        var json = JsonSerializer.Serialize(successResponse);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().Contains("projects") && 
                    req.RequestUri.ToString().Contains("admin-roles")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }

    private void SetupServiceError()
    {
        var httpResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }
}