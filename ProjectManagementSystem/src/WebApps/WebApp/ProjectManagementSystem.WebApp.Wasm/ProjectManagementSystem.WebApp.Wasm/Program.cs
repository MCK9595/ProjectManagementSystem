using ProjectManagementSystem.WebApp.Wasm.Client.Pages;
using ProjectManagementSystem.WebApp.Wasm.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Since pre-rendering is disabled, we don't need server-side authentication services

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// API endpoint to provide configuration to WebAssembly client
app.MapGet("/api/config", (IConfiguration configuration) =>
{
    var apiGatewayUrl = 
        // Azure Container Apps 環境での設定を優先
        configuration["AZURE_API_GATEWAY_URL"] ??
        // Aspire サービスディスカバリーの設定
        configuration["Services:api-gateway:https:0"] ??
        configuration["Services:api-gateway:http:0"] ?? 
        configuration.GetConnectionString("api-gateway") ?? 
        configuration["ApiGateway:BaseUrl"] ?? 
        // 開発環境用フォールバック
        (app.Environment.IsDevelopment() ? "https://localhost:5001" : null);
    
    // 本番環境では明示的な設定が必要
    if (string.IsNullOrEmpty(apiGatewayUrl) && !app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("API Gateway URL is not configured for production environment");
    }
                       
    return new { ApiGatewayBaseUrl = apiGatewayUrl };
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ProjectManagementSystem.WebApp.Wasm.Client._Imports).Assembly);

// Map service default endpoints.
app.MapDefaultEndpoints();

app.Run();
