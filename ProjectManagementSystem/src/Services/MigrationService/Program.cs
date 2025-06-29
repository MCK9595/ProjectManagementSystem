using Microsoft.AspNetCore.Identity;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.ProjectService.Data;
using ProjectManagementSystem.TaskService.Data;
using ProjectManagementSystem.MigrationService;

var builder = Host.CreateApplicationBuilder(args);

// Add service defaults & Aspire client integrations
builder.AddServiceDefaults();

// Microsoft Docs recommended: Add OpenTelemetry tracing for migration service
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

// Add SQL Server database contexts using .NET Aspire
builder.AddSqlServerDbContext<ApplicationDbContext>(connectionName: "identitydb");
builder.AddSqlServerDbContext<OrganizationDbContext>(connectionName: "organizationdb");
builder.AddSqlServerDbContext<ProjectDbContext>(connectionName: "projectdb");
builder.AddSqlServerDbContext<TaskDbContext>(connectionName: "taskdb");

// Configure ASP.NET Core Identity for seeding
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings (same as IdentityService)
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Add the migration worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
await host.RunAsync();
