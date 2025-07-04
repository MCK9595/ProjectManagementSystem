using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Abstractions;
using ProjectManagementSystem.Shared.Common.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace ProjectManagementSystem.IdentityService.Tests.Services;

public class UserDeletionServiceTests
{
    private readonly Mock<IUserManagementService> _mockUserManagementService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<UserDeletionService>> _mockLogger;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly UserDeletionService _service;

    public UserDeletionServiceTests()
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

    [Fact]
    public async Task DeleteUserWithDependenciesAsync_SuccessfulDeletion_ReturnsSuccess()
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
            Role = "User"
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupSuccessfulHttpResponses();

        _mockUserManagementService.Setup(x => x.DeleteUserAsync(userId))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteUserWithDependenciesAsync(userId, currentUserId);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("deleted successfully", result.Message);
    }

    [Fact]
    public async Task DeleteUserWithDependenciesAsync_ValidationFails_ReturnsError()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeleteUserWithDependenciesAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cannot delete this user", result.Message);
    }

    [Fact]
    public async Task DeleteUserWithDependenciesAsync_DependencyCleanupFails_ReturnsError()
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
            Role = "User"
        };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupFailedHttpResponse();

        // Act
        var result = await _service.DeleteUserWithDependenciesAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to clean up user dependencies", result.Message);
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
    public async Task ValidateUserDeletionAsync_CannotDelete_ReturnsError()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Cannot delete this user", result.Message);
    }

    [Fact]
    public async Task ValidateUserDeletionAsync_HasBlockingRoles_ReturnsError()
    {
        // Arrange
        var userId = 123;
        var currentUserId = 456;
        var user = new ApplicationUser { Id = userId, FirstName = "Test", LastName = "User", UserName = "testuser", Email = "test@example.com" };
        
        _mockUserManagementService.Setup(x => x.CanDeleteUserAsync(userId, currentUserId))
            .ReturnsAsync(true);

        _mockUserManager.Setup(x => x.FindByIdAsync(userId.ToString()))
            .ReturnsAsync(user);

        SetupBlockingRolesHttpResponse();

        // Act
        var result = await _service.ValidateUserDeletionAsync(userId, currentUserId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("sole administrator", result.Message);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_AllServicesSucceed_ReturnsSuccess()
    {
        // Arrange
        var userId = 123;
        SetupSuccessfulHttpResponses();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("dependencies cleaned up successfully", result.Message);
    }

    [Fact]
    public async Task CleanupUserDependenciesAsync_TaskServiceFails_ReturnsError()
    {
        // Arrange
        var userId = 123;
        SetupTaskServiceFailure();

        // Act
        var result = await _service.CleanupUserDependenciesAsync(userId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to cleanup task dependencies", result.Message);
    }

    [Fact]
    public async Task InvalidateUserSessionsAsync_ReturnsTrue()
    {
        // Arrange
        var userId = 123;

        // Act
        var result = await _service.InvalidateUserSessionsAsync(userId);

        // Assert
        Assert.True(result);
    }

    private void SetupSuccessfulHttpResponses()
    {
        var successResponse = new ApiResponse<object> { Success = true };
        var json = JsonSerializer.Serialize(successResponse);

        // Setup for organization admin roles check (GET)
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains("admin-roles") &&
                    req.RequestUri.ToString().Contains("organizations")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Setup for project admin roles check (GET)
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains("admin-roles") &&
                    req.RequestUri.ToString().Contains("projects")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Setup for task dependencies cleanup (DELETE)
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains("tasks") &&
                    req.RequestUri.ToString().Contains("dependencies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Setup for project dependencies cleanup (DELETE)
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains("projects") &&
                    req.RequestUri.ToString().Contains("dependencies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Setup for organization dependencies cleanup (DELETE)
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains("organizations") &&
                    req.RequestUri.ToString().Contains("dependencies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }

    private void SetupFailedHttpResponse()
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

    private void SetupBlockingRolesHttpResponse()
    {
        var errorResponse = new ApiResponse<object> { Success = false, Message = "User is the sole administrator" };
        var json = JsonSerializer.Serialize(errorResponse);

        // Setup for organization admin roles check returning blocking error
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains("admin-roles") &&
                    req.RequestUri.ToString().Contains("organizations")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        // Setup for project admin roles check returning success (to test organization blocking)
        var successResponse = new ApiResponse<object> { Success = true };
        var successJson = JsonSerializer.Serialize(successResponse);
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains("admin-roles") &&
                    req.RequestUri.ToString().Contains("projects")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson, Encoding.UTF8, "application/json")
            });
    }

    private void SetupTaskServiceFailure()
    {
        var successResponse = new ApiResponse<object> { Success = true };
        var successJson = JsonSerializer.Serialize(successResponse);

        // Setup organization admin roles check to succeed
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains("admin-roles") &&
                    req.RequestUri.ToString().Contains("organizations")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson, Encoding.UTF8, "application/json")
            });

        // Setup project admin roles check to succeed
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Get && 
                    req.RequestUri.ToString().Contains("admin-roles") &&
                    req.RequestUri.ToString().Contains("projects")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson, Encoding.UTF8, "application/json")
            });

        // Setup task service dependency cleanup to fail
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains("tasks") &&
                    req.RequestUri.ToString().Contains("dependencies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        // Setup other services to succeed (not reached due to task service failure)
        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains("projects") &&
                    req.RequestUri.ToString().Contains("dependencies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson, Encoding.UTF8, "application/json")
            });

        _mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => 
                    req.Method == HttpMethod.Delete && 
                    req.RequestUri.ToString().Contains("organizations") &&
                    req.RequestUri.ToString().Contains("dependencies")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successJson, Encoding.UTF8, "application/json")
            });
    }
}