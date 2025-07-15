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
    var apiGatewayUrl = configuration["Services:api-gateway:http:0"] ?? 
                       configuration.GetConnectionString("api-gateway") ?? 
                       configuration["ApiGateway:BaseUrl"] ?? 
                       "https://localhost:5001";
                       
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
