using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.ProjectService.Data;
using ProjectManagementSystem.TaskService.Data;
using ProjectManagementSystem.Shared.Common.Constants;
using System.Diagnostics;

namespace ProjectManagementSystem.MigrationService;

public class Worker : BackgroundService
{
    // Microsoft Docs recommended: OpenTelemetry Activity Source
    public static readonly ActivitySource ActivitySource = new(nameof(Worker));
    public const string ActivitySourceName = nameof(Worker);
    
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("ExecuteMigrationService");
        
        try
        {
            _logger.LogInformation("=== Starting Database Migration Service ===");
            _logger.LogInformation("Migration Service Version: {Version}", GetType().Assembly.GetName().Version);
            
            using var scope = _serviceProvider.CreateScope();
            
            // Run migrations in dependency order with timing
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            _logger.LogInformation("Phase 1: Running database migrations...");
            await MigrateIdentityDatabaseAsync(scope.ServiceProvider);
            await MigrateOrganizationDatabaseAsync(scope.ServiceProvider);
            await MigrateProjectDatabaseAsync(scope.ServiceProvider);
            await MigrateTaskDatabaseAsync(scope.ServiceProvider);
            
            _logger.LogInformation("Phase 2: Seeding initial data...");
            await SeedInitialDataAsync(scope.ServiceProvider);
            
            stopwatch.Stop();
            _logger.LogInformation("=== Database Migration Service Completed Successfully ===");
            _logger.LogInformation("Total migration time: {ElapsedMilliseconds}ms", stopwatch.ElapsedMilliseconds);
            
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "=== CRITICAL ERROR: Database Migration Service Failed ===");
            _logger.LogError("Error Type: {ErrorType}", ex.GetType().Name);
            _logger.LogError("Error Message: {ErrorMessage}", ex.Message);
            
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            
            // Re-throw to ensure the application fails fast
            throw;
        }
        finally
        {
            _logger.LogInformation("Stopping Migration Service application...");
            // Stop the application after migration is complete
            _hostApplicationLifetime.StopApplication();
        }
    }

    private async Task MigrateIdentityDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var activity = ActivitySource.StartActivity("MigrateIdentityDatabase");
        _logger.LogInformation("Migrating Identity database...");
        
        try
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Microsoft Docs recommended: Execution Strategy Pattern
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Database.MigrateAsync();
            });
            
            _logger.LogInformation("Identity database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate Identity database");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task MigrateOrganizationDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var activity = ActivitySource.StartActivity("MigrateOrganizationDatabase");
        _logger.LogInformation("Migrating Organization database...");
        
        try
        {
            var context = serviceProvider.GetRequiredService<OrganizationDbContext>();
            
            // Microsoft Docs recommended: Execution Strategy Pattern
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Database.MigrateAsync();
            });
            
            _logger.LogInformation("Organization database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate Organization database");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task MigrateProjectDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var activity = ActivitySource.StartActivity("MigrateProjectDatabase");
        _logger.LogInformation("Migrating Project database...");
        
        try
        {
            var context = serviceProvider.GetRequiredService<ProjectDbContext>();
            
            // Microsoft Docs recommended: Execution Strategy Pattern
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Database.MigrateAsync();
            });
            
            _logger.LogInformation("Project database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate Project database");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task MigrateTaskDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var activity = ActivitySource.StartActivity("MigrateTaskDatabase");
        _logger.LogInformation("Migrating Task database...");
        
        try
        {
            var context = serviceProvider.GetRequiredService<TaskDbContext>();
            
            // Microsoft Docs recommended: Execution Strategy Pattern
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await context.Database.MigrateAsync();
            });
            
            _logger.LogInformation("Task database migration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate Task database");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task SeedInitialDataAsync(IServiceProvider serviceProvider)
    {
        using var activity = ActivitySource.StartActivity("SeedInitialData");
        _logger.LogInformation("Checking if initial data seeding is required...");

        try
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            // Microsoft Docs recommended: Use Execution Strategy for data seeding
            var strategy = context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Check if any users exist
                var usersExist = await userManager.Users.AnyAsync();
                if (!usersExist)
                {
                    _logger.LogInformation("No users found. Seeding initial data...");
                    await SeedRolesAsync(roleManager);
                    await SeedAdminUserAsync(userManager);
                    _logger.LogInformation("Initial data seeding completed successfully.");
                }
                else
                {
                    _logger.LogInformation("Users already exist. Skipping initial data seeding.");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed initial data");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private async Task SeedRolesAsync(RoleManager<ApplicationRole> roleManager)
    {
        var roles = new[]
        {
            Roles.SystemAdmin,
            Roles.User
        };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogInformation("Creating role: {RoleName}", roleName);
                await roleManager.CreateAsync(new ApplicationRole(roleName));
            }
        }
    }

    private async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
    {
        const string adminEmail = "admin@projectmanagement.com";
        const string adminUsername = "admin";
        const string adminPassword = "AdminPassword123!";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            _logger.LogInformation("Creating default admin user: {AdminEmail}", adminEmail);
            
            adminUser = new ApplicationUser
            {
                UserName = adminUsername,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Administrator",
                Role = Roles.SystemAdmin,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.SystemAdmin);
                _logger.LogInformation("Default admin user created successfully.");
            }
            else
            {
                _logger.LogError("Failed to create admin user: {Errors}", 
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                throw new InvalidOperationException("Failed to create default admin user");
            }
        }
    }
}
