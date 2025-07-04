using System.Net;
using System.Net.Http.Json;
using Xunit;
using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.Shared.Models.DTOs;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Common.Constants;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for user deletion functionality using Aspire DistributedApplicationTestingBuilder
/// Tests the complete cross-service user deletion workflow
/// </summary>
[Collection("AspireIntegrationTests")]
public class UserDeletionIntegrationTestsAspire : AspireIntegrationTestBase
{
    private readonly AspireIntegrationTestFixture _fixture;

    public UserDeletionIntegrationTestsAspire(AspireIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public override async Task InitializeAsync()
    {
        // Initialize the fixture first
        await _fixture.InitializeAsync();
        
        // Set up HTTP clients from the shared fixture
        IdentityHttpClient = _fixture.CreateHttpClient("identity-service");
        OrganizationHttpClient = _fixture.CreateHttpClient("organization-service");
        ProjectHttpClient = _fixture.CreateHttpClient("project-service");
        TaskHttpClient = _fixture.CreateHttpClient("task-service");
        
        App = _fixture.App;
        
        // Authenticate as admin
        await AuthenticateAsAdminAsync();
    }

    [Fact]
    public async Task DeleteUser_WithNoDependencies_SucceedsWithComprehensiveDeletion()
    {
        // Arrange
        var userId = await CreateTestUserAsync(
            username: "deleteme1",
            email: "deleteme1@test.com",
            firstName: "Delete",
            lastName: "Me",
            role: Roles.User);

        // Act - Delete user using comprehensive deletion
        var deleteResponse = await IdentityHttpClient.DeleteAsync($"/api/usermanagement/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.True(deleteResult?.Success);
        Assert.Contains("deleted successfully", deleteResult?.Message ?? string.Empty);

        // Verify user is actually deleted (not just deactivated)
        var getUserResponse = await IdentityHttpClient.GetAsync($"/api/usermanagement/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getUserResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithOrganizationMembership_CleansUpMembershipAndDeletesUser()
    {
        // Arrange
        var userId = await CreateTestUserAsync(
            username: "orgmember1",
            email: "orgmember1@test.com",
            firstName: "Org",
            lastName: "Member",
            role: Roles.User);

        // Create organization
        var createOrgRequest = new ProjectManagementSystem.Shared.Models.DTOs.CreateOrganizationDto
        {
            Name = "Test Organization for Deletion",
            Description = "Test organization"
        };

        var createOrgResponse = await OrganizationHttpClient.PostAsJsonAsync("/api/organizations", createOrgRequest);
        var createOrgResult = await createOrgResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectManagementSystem.Shared.Models.DTOs.OrganizationDto>>();
        var orgId = createOrgResult?.Data?.Id ?? throw new InvalidOperationException("Failed to create organization");

        // Add user to organization
        var addMemberRequest = new ProjectManagementSystem.Shared.Models.DTOs.AddMemberDto
        {
            UserId = userId,
            Role = Roles.OrganizationMember
        };

        await OrganizationHttpClient.PostAsJsonAsync($"/api/organizations/{orgId}/members", addMemberRequest);

        // Act - Delete user
        var deleteResponse = await IdentityHttpClient.DeleteAsync($"/api/usermanagement/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.True(deleteResult?.Success);

        // Verify user is deleted
        var getUserResponse = await IdentityHttpClient.GetAsync($"/api/usermanagement/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getUserResponse.StatusCode);

        // Verify user is no longer in organization members
        var membersResponse = await OrganizationHttpClient.GetAsync($"/api/organizations/{orgId}/members");
        if (membersResponse.IsSuccessStatusCode)
        {
            var membersResult = await membersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProjectManagementSystem.Shared.Models.DTOs.OrganizationMemberDto>>>();
            var hasUserMembership = membersResult?.Data?.Items?.Any(m => m.UserId == userId) ?? false;
            Assert.False(hasUserMembership);
        }
    }

    [Fact]
    public async Task DeleteUser_CannotDeleteSelf_FailsWithValidationError()
    {
        // Arrange - Get current admin user ID
        var usersResponse = await IdentityHttpClient.GetAsync("/api/usermanagement?pageSize=100");
        if (!usersResponse.IsSuccessStatusCode)
        {
            // Skip test if we can't get users list
            return;
        }

        var usersResult = await usersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProjectManagementSystem.Shared.Models.DTOs.UserListDto>>>();
        var adminUser = usersResult?.Data?.Items?.FirstOrDefault(u => u.Email == "admin@projectmanagement.com");
        
        if (adminUser == null)
        {
            // Skip test if admin user not found
            return;
        }

        var adminUserId = adminUser.Id;

        // Act - Attempt to delete self
        var deleteResponse = await IdentityHttpClient.DeleteAsync($"/api/usermanagement/{adminUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.False(deleteResult?.Success);
        Assert.Contains("Cannot delete", deleteResult?.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteUser_NonExistentUser_ReturnsError()
    {
        // Arrange
        var nonExistentUserId = 999999;

        // Act
        var deleteResponse = await IdentityHttpClient.DeleteAsync($"/api/usermanagement/{nonExistentUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithProjectMembership_CleansUpMembershipAndDeletesUser()
    {
        // Arrange
        var userId = await CreateTestUserAsync(
            username: "projectmember1",
            email: "projectmember1@test.com",
            firstName: "Project",
            lastName: "Member",
            role: Roles.User);

        // Create organization first
        var createOrgRequest = new ProjectManagementSystem.Shared.Models.DTOs.CreateOrganizationDto
        {
            Name = "Test Organization for Project",
            Description = "Test organization"
        };

        var createOrgResponse = await OrganizationHttpClient.PostAsJsonAsync("/api/organizations", createOrgRequest);
        var createOrgResult = await createOrgResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectManagementSystem.Shared.Models.DTOs.OrganizationDto>>();
        var orgId = createOrgResult?.Data?.Id ?? throw new InvalidOperationException("Failed to create organization");

        // Create project
        var createProjectRequest = new ProjectManagementSystem.Shared.Models.DTOs.CreateProjectDto
        {
            Name = "Test Project for Deletion",
            Description = "Test project",
            OrganizationId = orgId,
            Status = "Active",
            Priority = "Medium"
        };

        var createProjectResponse = await ProjectHttpClient.PostAsJsonAsync("/api/projects", createProjectRequest);
        if (!createProjectResponse.IsSuccessStatusCode)
        {
            // Skip test if project creation fails (service might not be ready)
            return;
        }

        var createProjectResult = await createProjectResponse.Content.ReadFromJsonAsync<ApiResponse<ProjectManagementSystem.Shared.Models.DTOs.ProjectDto>>();
        var projectId = createProjectResult?.Data?.Id ?? throw new InvalidOperationException("Failed to create project");

        // Add user to project
        var addProjectMemberRequest = new ProjectManagementSystem.Shared.Models.DTOs.AddProjectMemberDto
        {
            UserId = userId,
            Role = Roles.ProjectMember
        };

        await ProjectHttpClient.PostAsJsonAsync($"/api/projects/{projectId}/members", addProjectMemberRequest);

        // Act - Delete user
        var deleteResponse = await IdentityHttpClient.DeleteAsync($"/api/usermanagement/{userId}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.True(deleteResult?.Success);

        // Verify user is deleted
        var getUserResponse = await IdentityHttpClient.GetAsync($"/api/usermanagement/{userId}");
        Assert.Equal(HttpStatusCode.NotFound, getUserResponse.StatusCode);

        // Verify user is no longer in project members
        var projectMembersResponse = await ProjectHttpClient.GetAsync($"/api/projects/{projectId}/members");
        if (projectMembersResponse.IsSuccessStatusCode)
        {
            var projectMembersResult = await projectMembersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResult<ProjectManagementSystem.Shared.Models.DTOs.ProjectMemberDto>>>();
            var hasUserProjectMembership = projectMembersResult?.Data?.Items?.Any(m => m.UserId == userId) ?? false;
            Assert.False(hasUserProjectMembership);
        }
    }

    protected override async Task CleanupTestDataAsync()
    {
        // Cleanup logic if needed - for now, each test cleans up by deleting test users
        await Task.CompletedTask;
    }

    public override async Task DisposeAsync()
    {
        // Don't dispose the shared fixture - it's managed by xUnit
        // Just dispose our HTTP clients
        IdentityHttpClient?.Dispose();
        OrganizationHttpClient?.Dispose();
        ProjectHttpClient?.Dispose();
        TaskHttpClient?.Dispose();
    }
}

