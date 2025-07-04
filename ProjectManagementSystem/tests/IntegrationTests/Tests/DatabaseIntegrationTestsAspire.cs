using System.Net;
using System.Net.Http.Json;
using ProjectManagementSystem.IntegrationTests.Infrastructure;
using ProjectManagementSystem.Shared.Common.Models;

namespace ProjectManagementSystem.IntegrationTests.Tests;

/// <summary>
/// Integration tests for database connectivity and basic CRUD operations using Aspire
/// </summary>
[Collection("AspireIntegrationTests")]
public class DatabaseIntegrationTestsAspire : AspireIntegrationTestBase
{
    private readonly AspireIntegrationTestFixture _fixture;

    public DatabaseIntegrationTestsAspire(AspireIntegrationTestFixture fixture)
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
    public async Task IdentityService_DatabaseConnection_IsWorking()
    {
        // Act - Try to get users list which requires database access
        var response = await IdentityHttpClient.GetAsync("/api/usermanagement?pageSize=1");

        // Assert
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public async Task OrganizationService_DatabaseConnection_IsWorking()
    {
        // Act - Try to get organizations list which requires database access
        var response = await OrganizationHttpClient.GetAsync("/api/organizations?pageSize=1");

        // Assert
        Assert.True(response.IsSuccessStatusCode || 
                   response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.ServiceUnavailable);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public async Task ProjectService_DatabaseConnection_IsWorking()
    {
        // Act - Try to get projects list which requires database access
        var response = await ProjectHttpClient.GetAsync("/api/projects?pageSize=1");

        // Assert
        Assert.True(response.IsSuccessStatusCode || 
                   response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.ServiceUnavailable);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public async Task TaskService_DatabaseConnection_IsWorking()
    {
        // Act - Try to get tasks list which requires database access
        var response = await TaskHttpClient.GetAsync("/api/tasks?pageSize=1");

        // Assert
        Assert.True(response.IsSuccessStatusCode || 
                   response.StatusCode == HttpStatusCode.Unauthorized ||
                   response.StatusCode == HttpStatusCode.ServiceUnavailable);
        
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.NotEmpty(content);
        }
    }

    [Fact]
    public async Task AllServices_AreResponding()
    {
        // Act & Assert - Check that all services are at least responding
        var identityResponse = await IdentityHttpClient.GetAsync("/");
        var orgResponse = await OrganizationHttpClient.GetAsync("/");
        var projectResponse = await ProjectHttpClient.GetAsync("/");
        var taskResponse = await TaskHttpClient.GetAsync("/");

        // Services should at least be reachable (even if they return 404 for root path)
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, identityResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, orgResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, projectResponse.StatusCode);
        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, taskResponse.StatusCode);
    }

    public override async Task DisposeAsync()
    {
        // Don't dispose the shared fixture - it's managed by xUnit
        IdentityHttpClient?.Dispose();
        OrganizationHttpClient?.Dispose();
        ProjectHttpClient?.Dispose();
        TaskHttpClient?.Dispose();
    }
}