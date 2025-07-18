using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ProjectManagementSystem.IdentityService.Data;
using ProjectManagementSystem.IdentityService.Data.Entities;
using ProjectManagementSystem.IdentityService.Services;
using ProjectManagementSystem.Shared.Common.Configuration;
using ProjectManagementSystem.Shared.Common.Extensions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Azure SQL Server database context using .NET Aspire
builder.AddSqlServerDbContext<ApplicationDbContext>("identitydb");

// Configure ASP.NET Core Identity with custom authentication setup
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Remove default Cookie authentication schemes to avoid conflict with JWT
builder.Services.ConfigureApplicationCookie(options =>
{
    options.SlidingExpiration = false;
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
});

// Add services to the container
builder.Services.AddControllers();

// Configure JWT Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Validate JWT settings at startup
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
if (jwtSettings == null)
{
    throw new InvalidOperationException($"Missing {JwtSettings.SectionName} configuration section");
}
jwtSettings.Validate();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdminOnly", policy =>
        policy.RequireRole(ProjectManagementSystem.Shared.Common.Constants.Roles.SystemAdmin));
});

// Register application services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IUserDeletionService, UserDeletionService>();

// Add HttpClient for service-to-service communication
builder.Services.AddHttpClient("OrganizationService", client =>
{
    client.BaseAddress = new Uri("https://organization-service/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient("ProjectService", client =>
{
    client.BaseAddress = new Uri("https://project-service/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient("TaskService", client =>
{
    client.BaseAddress = new Uri("https://task-service/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register abstraction implementations for testability
builder.Services.AddSingleton<ProjectManagementSystem.IdentityService.Abstractions.IDateTimeProvider, ProjectManagementSystem.IdentityService.Implementations.SystemDateTimeProvider>();
builder.Services.AddSingleton<ProjectManagementSystem.IdentityService.Abstractions.IRandomGenerator, ProjectManagementSystem.IdentityService.Implementations.SystemRandomGenerator>();
builder.Services.AddSingleton<ProjectManagementSystem.IdentityService.Abstractions.IGuidGenerator, ProjectManagementSystem.IdentityService.Implementations.SystemGuidGenerator>();

// Add OpenAPI with JWT authentication
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Identity Service API", Version = "v1" });
    
    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map service default endpoints.
app.MapDefaultEndpoints();

// Add global exception handling
app.UseGlobalExceptionHandler();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Add diagnostic endpoints for connectivity testing
app.MapGet("/health-check", () => Results.Ok(new { 
    status = "healthy", 
    service = "identity-service",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}))
.WithName("HealthCheck")
.WithTags("Diagnostics");

app.MapGet("/debug/ping", () => Results.Ok(new { 
    message = "Identity Service is reachable",
    timestamp = DateTime.UtcNow
}))
.WithName("DebugPing")
.WithTags("Diagnostics");


app.Run();

// Make Program class accessible for integration tests
public partial class Program { }