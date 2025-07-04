using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using ProjectManagementSystem.Shared.Common.Models;
using ProjectManagementSystem.Shared.Models.DTOs;

namespace ProjectManagementSystem.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for Aspire integration tests using DistributedApplicationTestingBuilder
/// </summary>
public abstract class AspireIntegrationTestBase : IAsyncLifetime
{
    protected DistributedApplication App { get; set; } = null!;
    protected HttpClient IdentityHttpClient { get; set; } = null!;
    protected HttpClient OrganizationHttpClient { get; set; } = null!;
    protected HttpClient ProjectHttpClient { get; set; } = null!;
    protected HttpClient TaskHttpClient { get; set; } = null!;
    protected string AdminToken { get; private set; } = string.Empty;

    public virtual async Task InitializeAsync()
    {
        // Create the distributed application testing builder
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.ProjectManagementSystem_AppHost>();

        // Configure HTTP clients with resilience handlers
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
        {
            clientBuilder.AddStandardResilienceHandler();
        });

        // Build and start the application
        App = await appHost.BuildAsync();
        
        // Wait for the application to be ready
        await App.StartAsync();

        // Create HTTP clients for each service
        IdentityHttpClient = App.CreateHttpClient("identity-service");
        OrganizationHttpClient = App.CreateHttpClient("organization-service");
        ProjectHttpClient = App.CreateHttpClient("project-service");
        TaskHttpClient = App.CreateHttpClient("task-service");

        // Wait for services to be ready
        await WaitForServicesAsync();
        
        // Authenticate and get admin token
        await AuthenticateAsAdminAsync();
    }

    /// <summary>
    /// Wait for all services to be ready and accessible
    /// </summary>
    protected virtual async Task WaitForServicesAsync()
    {
        // Wait for services to be available using resource notification service
        var resourceNotificationService = App.Services.GetRequiredService<ResourceNotificationService>();
        
        await resourceNotificationService.WaitForResourceAsync("identity-service", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));
            
        await resourceNotificationService.WaitForResourceAsync("organization-service", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));
            
        await resourceNotificationService.WaitForResourceAsync("project-service", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));
            
        await resourceNotificationService.WaitForResourceAsync("task-service", KnownResourceStates.Running)
            .WaitAsync(TimeSpan.FromMinutes(3));
    }

    /// <summary>
    /// Authenticate as the default admin user and get the access token
    /// </summary>
    protected virtual async Task AuthenticateAsAdminAsync()
    {
        var loginRequest = new
        {
            Username = "admin",
            Password = "AdminPassword123!"
        };

        try
        {
            var loginResponse = await IdentityHttpClient.PostAsJsonAsync("/api/auth/login", loginRequest);
            
            if (loginResponse.IsSuccessStatusCode)
            {
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>();
                AdminToken = loginResult?.Data?.Token ?? throw new InvalidOperationException("Failed to get admin token from response");
                
                // Set the authorization header for all HTTP clients
                SetAuthorizationHeader(AdminToken);
            }
            else
            {
                var errorContent = await loginResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Failed to authenticate admin user. Status: {loginResponse.StatusCode}, Content: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to authenticate admin user during test setup", ex);
        }
    }

    /// <summary>
    /// Set the authorization header for all HTTP clients
    /// </summary>
    protected virtual void SetAuthorizationHeader(string token)
    {
        IdentityHttpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        OrganizationHttpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        ProjectHttpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        TaskHttpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Create a test user and return the user ID
    /// </summary>
    protected virtual async Task<int> CreateTestUserAsync(
        string username = "testuser",
        string email = "testuser@example.com",
        string firstName = "Test",
        string lastName = "User",
        string role = ProjectManagementSystem.Shared.Common.Constants.Roles.User,
        bool isActive = true)
    {
        // Generate unique username and email to avoid conflicts between test runs
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var uniqueUsername = $"{username}_{uniqueId}";
        var uniqueEmail = email.Replace("@", $"_{uniqueId}@");

        var createUserRequest = new ProjectManagementSystem.Shared.Models.DTOs.CreateUserRequest
        {
            Username = uniqueUsername,
            Email = uniqueEmail,
            FirstName = firstName,
            LastName = lastName,
            Password = "TestPassword123!",
            Role = role,
            IsActive = isActive
        };

        var response = await IdentityHttpClient.PostAsJsonAsync("/api/usermanagement", createUserRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Failed to create test user. Status: {response.StatusCode}, Content: {errorContent}");
        }
        
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        
        return result?.Data?.Id ?? throw new InvalidOperationException("Failed to create test user - no user ID in response");
    }

    /// <summary>
    /// Clean up test data after each test
    /// </summary>
    protected virtual async Task CleanupTestDataAsync()
    {
        // Override in derived classes to implement specific cleanup logic
        await Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        try
        {
            await CleanupTestDataAsync();
        }
        catch
        {
            // Ignore cleanup errors during disposal
        }

        IdentityHttpClient?.Dispose();
        OrganizationHttpClient?.Dispose();
        ProjectHttpClient?.Dispose();
        TaskHttpClient?.Dispose();
        
        if (App != null)
        {
            await App.DisposeAsync();
        }
    }
}

