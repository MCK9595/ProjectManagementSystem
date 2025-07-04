using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ProjectManagementSystem.IntegrationTests.Infrastructure;

/// <summary>
/// Shared fixture for Aspire integration tests to efficiently manage app host lifecycle
/// </summary>
public class AspireIntegrationTestFixture : IAsyncLifetime
{
    private DistributedApplication? _app;
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    /// <summary>
    /// Gets the distributed application instance
    /// </summary>
    public DistributedApplication App => _app ?? throw new InvalidOperationException("App not initialized");

    /// <summary>
    /// Create HTTP client for a specific service
    /// </summary>
    public HttpClient CreateHttpClient(string serviceName)
    {
        if (_app == null)
            throw new InvalidOperationException("App not initialized");

        var client = _app.CreateHttpClient(serviceName);
        
        // Configure default settings for test clients
        client.Timeout = TimeSpan.FromMinutes(5);
        
        return client;
    }

    /// <summary>
    /// Initialize the distributed application once for all tests
    /// </summary>
    public async Task InitializeAsync()
    {
        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_app != null)
                return; // Already initialized

            // Create the distributed application testing builder
            var appHost = await DistributedApplicationTestingBuilder
                .CreateAsync<Projects.ProjectManagementSystem_AppHost>();

            // Configure HTTP clients with resilience handlers
            appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
            {
                clientBuilder.AddStandardResilienceHandler(options =>
                {
                    // Increase timeout for integration tests  
                    options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
                    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                    // Circuit breaker sampling duration must be at least double the attempt timeout
                    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
                });
            });

            // Add additional test configuration if needed
            appHost.Services.Configure<AspireHostingOptions>(options =>
            {
                // Disable dashboard for tests
                options.DashboardEnabled = false;
            });

            // Build the application
            _app = await appHost.BuildAsync();

            // Start the application and wait for all services to be ready
            await _app.StartAsync();
            await WaitForAllServicesAsync();
        }
        finally
        {
            _initializationSemaphore.Release();
        }
    }

    /// <summary>
    /// Wait for all services to be ready
    /// </summary>
    private async Task WaitForAllServicesAsync()
    {
        if (_app == null)
            return;

        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        var timeout = TimeSpan.FromMinutes(5); // Generous timeout for CI environments

        try
        {
            // Wait for all core services to be running
            var servicesToWait = new[]
            {
                "identity-service",
                "organization-service", 
                "project-service",
                "task-service"
            };

            var waitTasks = servicesToWait.Select(async serviceName =>
            {
                try
                {
                    await resourceNotificationService
                        .WaitForResourceAsync(serviceName, KnownResourceStates.Running)
                        .WaitAsync(timeout);
                }
                catch (TimeoutException)
                {
                    // Log but don't fail - some services might not be critical for all tests
                    Console.WriteLine($"Warning: Service {serviceName} did not reach running state within timeout");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error waiting for service {serviceName}: {ex.Message}");
                }
            });

            await Task.WhenAll(waitTasks);

            // Additional wait to ensure services are fully ready for HTTP requests
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during service readiness check: {ex.Message}");
            // Continue anyway - tests may still work
        }
    }

    /// <summary>
    /// Dispose the distributed application
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_app != null)
        {
            try
            {
                await _app.DisposeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error disposing app: {ex.Message}");
            }
            finally
            {
                _app = null;
            }
        }

        _initializationSemaphore.Dispose();
    }
}

/// <summary>
/// Collection definition for Aspire integration tests
/// </summary>
[CollectionDefinition("AspireIntegrationTests")]
public class AspireIntegrationTestCollection : ICollectionFixture<AspireIntegrationTestFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

/// <summary>
/// Options for Aspire hosting configuration in tests
/// </summary>
public class AspireHostingOptions
{
    public bool DashboardEnabled { get; set; } = false;
}