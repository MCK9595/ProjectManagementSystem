using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjectManagementSystem.Shared.Client.Services;
using ProjectManagementSystem.Shared.Client.Handlers;
using ProjectManagementSystem.Shared.Client.Authentication;
using System.Net.Http.Json;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// First, get configuration from the server
using var configHttpClient = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
try
{
    var configResponse = await configHttpClient.GetFromJsonAsync<ProjectManagementSystem.WebApp.Wasm.Client.ConfigResponse>("/api/config");
    var apiGatewayBaseUrl = configResponse?.ApiGatewayBaseUrl;
    
    // Validate the API Gateway URL
    if (string.IsNullOrEmpty(apiGatewayBaseUrl))
    {
        throw new InvalidOperationException("API Gateway URL is not configured");
    }
    
    // Store in configuration for later use
    builder.Configuration["ApiGateway:BaseUrl"] = apiGatewayBaseUrl;
    
    // Register named HttpClient for API Gateway with TokenHandler
    builder.Services.AddHttpClient("ApiGateway", client =>
    {
        client.BaseAddress = new Uri(apiGatewayBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<TokenHandler>();
    
    Console.WriteLine($"API Gateway configured: {apiGatewayBaseUrl}");
}
catch (Exception ex)
{
    // Fallback to configuration-based setup
    var fallbackUrl = 
        // Azure Container Apps 環境変数
        builder.Configuration["AZURE_API_GATEWAY_URL"] ??
        // 標準設定
        builder.Configuration["ApiGateway:BaseUrl"] ??
        // 開発環境用フォールバック
        "https://localhost:5001";
    
    builder.Services.AddHttpClient("ApiGateway", client =>
    {
        client.BaseAddress = new Uri(fallbackUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddHttpMessageHandler<TokenHandler>();
    
    Console.WriteLine($"Failed to get config from server, using fallback: {fallbackUrl}. Error: {ex.Message}");
}

// Register default HttpClient (for backward compatibility)
builder.Services.AddScoped(sp => 
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    return httpClientFactory.CreateClient("ApiGateway");
});

// Register WebAssembly-specific services
builder.Services.AddScoped<ISessionTokenService, LocalStorageTokenService>();
builder.Services.AddScoped<TokenHandler>();

// Register authentication services
builder.Services.AddScoped<AuthenticationStateProvider, WebAssemblyAuthenticationStateProvider>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Register business services
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IUserService, UserService>();

await builder.Build().RunAsync();

namespace ProjectManagementSystem.WebApp.Wasm.Client
{
    public class ConfigResponse
    {
        public string ApiGatewayBaseUrl { get; set; } = string.Empty;
    }
}
