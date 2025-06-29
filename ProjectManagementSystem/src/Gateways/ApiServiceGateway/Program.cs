using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add JWT authentication
var jwtKey = builder.Configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

// Add authorization services
builder.Services.AddAuthorization();

// Add request timeouts services (required for UseRequestTimeouts middleware)
builder.Services.AddRequestTimeouts();

// Add YARP with enhanced logging
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add detailed logging for YARP and HTTP requests
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Add CORS - fix.mdの提案に従い動的設定を適用
var allowedOrigin = builder.Configuration["AllowedOrigins"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (!string.IsNullOrEmpty(allowedOrigin))
        {
            policy.WithOrigins(allowedOrigin)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // JWT認証のためにCredentialsを許可
        }
        else
        {
            // フォールバック: 開発環境用
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Add Swagger for API Gateway documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Project Management API Gateway", Version = "v1" });
    
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Please enter JWT token",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
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
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Project Management API Gateway v1");
        c.RoutePrefix = "swagger";
    });
}

// Map service default endpoints.
app.MapDefaultEndpoints();

// Add request logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var requestId = Guid.NewGuid().ToString("N")[..8];
    
    logger.LogInformation("=== INCOMING REQUEST {RequestId} ===", requestId);
    logger.LogInformation("Method: {Method}, Path: {Path}, QueryString: {QueryString}", 
        context.Request.Method, context.Request.Path, context.Request.QueryString);
    logger.LogInformation("Headers: {Headers}", 
        string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}: {string.Join(", ", h.Value)}")));
    logger.LogInformation("RemoteIpAddress: {RemoteIp}", context.Connection.RemoteIpAddress);
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await next();
    stopwatch.Stop();
    
    logger.LogInformation("=== RESPONSE {RequestId} === Status: {StatusCode}, Duration: {Duration}ms", 
        requestId, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
});

// Enable CORS - デフォルトポリシーを使用
app.UseCors();

// Enable endpoint routing, required for YARP
app.UseRouting();

// Use authentication and authorization (required for JWT) - must be after UseRouting
app.UseAuthentication();
app.UseAuthorization();

// Add request timeouts middleware (required by YARP)
app.UseRequestTimeouts();

// Configure endpoints
app.UseEndpoints(endpoints =>
{
    // Health check endpoint for the gateway itself
    endpoints.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
       .WithName("HealthCheck")
       .WithTags("Health");

    // API Gateway info endpoint
    endpoints.MapGet("/gateway/info", () => Results.Ok(new 
    { 
        service = "API Gateway", 
        version = "1.0.0",
        environment = app.Environment.EnvironmentName,
        timestamp = DateTime.UtcNow 
    }))
    .WithName("GatewayInfo")
    .WithTags("Gateway");

    // Debug endpoint to check service URLs
    endpoints.MapGet("/gateway/services", (IConfiguration config, ILogger<Program> logger) => 
    {
        logger.LogInformation("=== SERVICE DISCOVERY DEBUG ===");
        var services = new Dictionary<string, string>();
        
        // Try different configuration patterns
        foreach (var service in new[] { "identity-service", "organization-service", "project-service", "task-service" })
        {
            var url = config.GetConnectionString(service) ?? 
                      config[$"services:{service}:http:0"] ?? 
                      config[$"services:{service}:https:0"] ??
                      config[$"services__{service}__http__0"] ??
                      config[$"services__{service}__https__0"] ??
                      "not found";
            services[service] = url;
            logger.LogInformation("Service {ServiceName} resolved to: {ServiceUrl}", service, url);
        }
        
        // Also log all configuration keys for debugging
        logger.LogInformation("All Configuration Keys:");
        foreach (var kvp in config.AsEnumerable().OrderBy(x => x.Key))
        {
            logger.LogInformation("Config: {Key} = {Value}", kvp.Key, kvp.Value);
        }
        
        return Results.Ok(services);
    })
    .WithName("ServiceUrls")
    .WithTags("Gateway");

    // Use YARP reverse proxy
    endpoints.MapReverseProxy();
});

app.Run();