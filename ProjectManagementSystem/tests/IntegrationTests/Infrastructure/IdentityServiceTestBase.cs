using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using Testcontainers.MsSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace ProjectManagementSystem.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for IdentityService integration tests providing common test infrastructure
/// </summary>
public abstract class IdentityServiceTestBase : IClassFixture<IdentityServiceTestFixture>, IDisposable
{
    protected readonly IdentityServiceTestFixture _fixture;
    protected readonly HttpClient _client;
    protected readonly IServiceScope _scope;
    protected readonly ApplicationDbContext _context;

    protected IdentityServiceTestBase(IdentityServiceTestFixture fixture)
    {
        _fixture = fixture;
        _client = _fixture.CreateClient();
        _scope = _fixture.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Clean up test data after each test
    /// </summary>
    protected virtual async Task CleanupAsync()
    {
        // Remove test data in reverse dependency order
        _context.RefreshTokens.RemoveRange(_context.RefreshTokens);
        _context.Users.RemoveRange(_context.Users.Where(u => u.Email.Contains("test") && u.Email != "admin@projectmanagement.com"));
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Create a test user for testing purposes
    /// </summary>
    protected async Task<ApplicationUser> CreateTestUserAsync(
        string username = "testuser",
        string email = "testuser@example.com",
        string firstName = "Test",
        string lastName = "User",
        string role = "User",
        bool isActive = true)
    {
        var user = new ApplicationUser
        {
            UserName = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            IsActive = isActive,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        
        return user;
    }

    /// <summary>
    /// Get the default admin user
    /// </summary>
    protected async Task<ApplicationUser?> GetAdminUserAsync()
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == "admin@projectmanagement.com");
    }

    public virtual void Dispose()
    {
        _scope?.Dispose();
    }
}

/// <summary>
/// Test fixture for IdentityService integration tests
/// </summary>
public class IdentityServiceTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private MsSqlContainer? _sqlServerContainer;
    private string _connectionString = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:identitydb"] = _connectionString,
                ["JwtSettings:SecretKey"] = "ThisIsAVeryLongSecretKeyForJWTTokenGenerationInTests123456789",
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience",
                ["JwtSettings:AccessTokenExpiryMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpiryDays"] = "7"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove DbContext related registrations - simplified approach
            var dbContextDescriptors = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                (d.ServiceType.IsGenericType && d.ServiceType.GetGenericArguments().Any(arg => arg == typeof(ApplicationDbContext))))
                .ToList();
            
            foreach (var descriptor in dbContextDescriptors)
            {
                services.Remove(descriptor);
            }

            // Add test database context with explicit configuration
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(_connectionString);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }, ServiceLifetime.Scoped);

            // Override logging for testing
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Warning);
                builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
                builder.AddFilter("ProjectManagementSystem", LogLevel.Information);
            });
        });
    }

    public async Task InitializeAsync()
    {
        // Start SQL Server container for testing
        _sqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("TestPassword123!")
            .WithCleanUp(true)
            .Build();

        await _sqlServerContainer.StartAsync();
        _connectionString = _sqlServerContainer.GetConnectionString();

        // Apply migrations and seed test data
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Apply migrations to create/update database schema
        await context.Database.MigrateAsync();
        await SeedTestDataAsync(context);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_sqlServerContainer != null)
        {
            await _sqlServerContainer.DisposeAsync();
        }
        
        await base.DisposeAsync();
    }

    private async Task SeedTestDataAsync(ApplicationDbContext context)
    {
        try
        {
            // Use the same data seeder as the main application to ensure consistency
            using var scope = Services.CreateScope();
            await ProjectManagementSystem.IdentityService.Data.DataSeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            // Log the exception but don't fail the test setup
            // This allows tests to run even if seeding fails
            Console.WriteLine($"Warning: Could not seed test data: {ex.Message}");
        }
    }
}