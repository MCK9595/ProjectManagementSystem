using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;
using ProjectManagementSystem.IdentityService.Controllers;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IdentityService.Tests.Controllers;

public class UserManagementControllerTests
{
    private readonly Mock<IUserManagementService> _mockUserManagementService;
    private readonly Mock<IUserDeletionService> _mockUserDeletionService;
    private readonly Mock<ILogger<UserManagementController>> _mockLogger;
    private readonly UserManagementController _controller;

    public UserManagementControllerTests()
    {
        _mockUserManagementService = new Mock<IUserManagementService>();
        _mockUserDeletionService = new Mock<IUserDeletionService>();
        _mockLogger = new Mock<ILogger<UserManagementController>>();

        _controller = new UserManagementController(
            _mockUserManagementService.Object,
            _mockUserDeletionService.Object,
            _mockLogger.Object);

        // Setup controller context with claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "123")
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = claimsPrincipal
            }
        };
    }

    [Fact]
    public async Task DeleteUser_SuccessfulDeletion_ReturnsOkResult()
    {
        // Arrange
        var userId = 456;
        var currentUserId = 123;
        var successResponse = ApiResponse<object>.SuccessResult(
            new { userId }, 
            "User deleted successfully with all dependencies cleaned up");

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(userId, currentUserId))
            .ReturnsAsync(successResponse);

        // Act
        var result = await _controller.DeleteUser(userId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Contains("deleted successfully", response.Message);
    }

    [Fact]
    public async Task DeleteUser_ValidationFails_ReturnsBadRequest()
    {
        // Arrange
        var userId = 456;
        var currentUserId = 123;
        var errorResponse = ApiResponse<object>.ErrorResult("Cannot delete this user. You cannot delete yourself or the last SystemAdmin.");

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(userId, currentUserId))
            .ReturnsAsync(errorResponse);

        // Act
        var result = await _controller.DeleteUser(userId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Cannot delete this user", response.Message);
    }

    [Fact]
    public async Task DeleteUser_DependencyCleanupFails_ReturnsBadRequest()
    {
        // Arrange
        var userId = 456;
        var currentUserId = 123;
        var errorResponse = ApiResponse<object>.ErrorResult("Failed to clean up user dependencies. Deletion aborted.");

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(userId, currentUserId))
            .ReturnsAsync(errorResponse);

        // Act
        var result = await _controller.DeleteUser(userId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Contains("Failed to clean up user dependencies", response.Message);
    }

    [Fact]
    public async Task DeleteUser_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var userId = 456;
        var currentUserId = 123;

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(userId, currentUserId))
            .ThrowsAsync(new Exception("Service error"));

        // Act
        var result = await _controller.DeleteUser(userId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        
        var response = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(response.Success);
        Assert.Contains("error occurred during the comprehensive user deletion process", response.Message);
    }

    [Fact]
    public async Task DeleteUser_InvalidCurrentUser_ReturnsInternalServerError()
    {
        // Arrange
        var userId = 456;
        
        // Setup controller with invalid claims
        var invalidClaims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "invalid")
        };
        var invalidIdentity = new ClaimsIdentity(invalidClaims, "TestAuthType");
        var invalidClaimsPrincipal = new ClaimsPrincipal(invalidIdentity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = invalidClaimsPrincipal
            }
        };

        // Act
        var result = await _controller.DeleteUser(userId);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
        var response = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(response.Success);
        Assert.Contains("error occurred during the comprehensive user deletion process", response.Message);
    }

    [Fact]
    public async Task DeleteUser_NoCurrentUserClaim_ParsesAsZero()
    {
        // Arrange
        var userId = 456;
        
        // Setup controller with no claims
        var noClaims = new List<Claim>();
        var noClaimsIdentity = new ClaimsIdentity(noClaims, "TestAuthType");
        var noClaimsPrincipal = new ClaimsPrincipal(noClaimsIdentity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = noClaimsPrincipal
            }
        };

        var errorResponse = ApiResponse<object>.ErrorResult("Cannot delete this user.");

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(userId, 0))
            .ReturnsAsync(errorResponse);

        // Act
        var result = await _controller.DeleteUser(userId);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<ApiResponse<object>>(badRequestResult.Value);
        Assert.False(response.Success);

        // Verify service was called with currentUserId = 0
        _mockUserDeletionService.Verify(
            x => x.DeleteUserWithDependenciesAsync(userId, 0),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUser_CallsUserDeletionServiceWithCorrectParameters()
    {
        // Arrange
        var userId = 456;
        var currentUserId = 123;
        var successResponse = ApiResponse<object>.SuccessResult(new { message = "Success" });

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(userId, currentUserId))
            .ReturnsAsync(successResponse);

        // Act
        await _controller.DeleteUser(userId);

        // Assert
        _mockUserDeletionService.Verify(
            x => x.DeleteUserWithDependenciesAsync(userId, currentUserId),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUser_LogsRequestInformation()
    {
        // Arrange
        var userId = 456;
        var successResponse = ApiResponse<object>.SuccessResult(new { message = "Success" });

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(successResponse);

        // Act
        await _controller.DeleteUser(userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("DELETE USER WITH DEPENDENCIES REQUEST")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeleteUser_SuccessfulDeletion_LogsSuccess()
    {
        // Arrange
        var userId = 456;
        var successResponse = ApiResponse<object>.SuccessResult(new { message = "Success" });

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(successResponse);

        // Act
        await _controller.DeleteUser(userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User deleted successfully with all dependencies cleaned up")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteUser_ValidationFailure_LogsWarning()
    {
        // Arrange
        var userId = 456;
        var errorResponse = ApiResponse<object>.ErrorResult("Cannot delete this user");

        _mockUserDeletionService
            .Setup(x => x.DeleteUserWithDependenciesAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(errorResponse);

        // Act
        await _controller.DeleteUser(userId);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("User deletion with dependencies failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}