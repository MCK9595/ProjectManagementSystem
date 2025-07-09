using ProjectManagementSystem.WebApp.Components;
using ProjectManagementSystem.WebApp.Services;
using Microsoft.AspNetCore.Components.Authorization;
using ProjectManagementSystem.Shared.Common.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure logging explicitly for Blazor Server
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Add test logging to verify configuration
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
logger.LogInformation("=== BLAZOR SERVER STARTING ===");
logger.LogDebug("Blazor Server Program.cs - Debug logging enabled");

// NavigationExceptionを無効化（.NET 10の動作を先取り）
// これによりBlazor Serverでのナビゲーション時の例外を防ぐ
AppContext.SetSwitch(
    "Microsoft.AspNetCore.Components.Endpoints.NavigationManager.EnableThrowNavigationException", 
    isEnabled: false);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add authorization services (required for AuthorizeRouteView)
builder.Services.AddAuthorization();

// Add session services for server-side token storage
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // Extended session timeout
    options.Cookie.Name = ".ProjectManagement.Session"; // Custom cookie name
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax; // Changed from Strict to allow navigation
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use HTTPS in production
    options.IOTimeout = TimeSpan.FromMinutes(1); // Allow more time for session operations
});

// Add HttpContextAccessor for session access
builder.Services.AddHttpContextAccessor();

// Add memory cache service for token storage
builder.Services.AddMemoryCache();

// Add server-side token service
// Use MemoryTokenService with IMemoryCache for better Blazor Server compatibility
// Changed to Scoped to work properly with Blazor Server circuits
builder.Services.AddScoped<ProjectManagementSystem.Shared.Common.Services.ISessionTokenService, ProjectManagementSystem.WebApp.Services.MemoryTokenService>();

// Add TokenHandler for automatic JWT token attachment
builder.Services.AddScoped<ProjectManagementSystem.WebApp.Services.TokenHandler>();

// Add authentication services
builder.Services.AddAuthentication("Custom")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ProjectManagementSystem.WebApp.Services.CustomAuthenticationHandler>("Custom", options => { });

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

// Add business services
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IUserService, UserService>();

// Configure HttpClient with TokenHandler for server-side HttpClient
// Use Aspire service discovery for API Gateway
builder.Services.AddHttpClient("ApiGateway", (serviceProvider, client) =>
{
    // Aspire service discovery will automatically resolve the api-gateway URL
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var apiGatewayUrl = configuration.GetConnectionString("api-gateway") ?? "http://api-gateway";
    
    client.BaseAddress = new Uri(apiGatewayUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("=== HttpClient configured with BaseAddress: {BaseAddress} ===", client.BaseAddress);
})
.AddHttpMessageHandler<ProjectManagementSystem.WebApp.Services.TokenHandler>();

var app = builder.Build();

// Test logging after app is built
var appLogger = app.Services.GetRequiredService<ILogger<Program>>();
appLogger.LogInformation("=== BLAZOR SERVER APP BUILT SUCCESSFULLY ===");
appLogger.LogWarning("=== WARNING TEST: Server-side logging is working ===");
appLogger.LogError("=== ERROR TEST: This should appear in server console ===");
appLogger.LogDebug("=== DEBUG TEST: Debug level logging enabled for server ===");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Use session middleware (must be before UseRouting)
app.UseSession();

// Add custom middleware to ensure session is initialized and handle token flushing
app.Use(async (context, next) =>
{
    // Ensure session is loaded early in the request pipeline
    await context.Session.LoadAsync();
    
    // Call next middleware
    await next();
    
    // Try to flush any pending tokens after the request is processed
    var sessionTokenService = context.RequestServices.GetService<ProjectManagementSystem.Shared.Common.Services.ISessionTokenService>();
    if (sessionTokenService != null)
    {
        try
        {
            await sessionTokenService.FlushPendingTokenAsync();
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "Failed to flush pending tokens after request completion");
        }
    }
});

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map service default endpoints.
app.MapDefaultEndpoints();

app.Run();
