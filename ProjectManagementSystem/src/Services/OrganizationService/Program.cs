using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.OrganizationService.Services;
using ProjectManagementSystem.Shared.Common.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Azure SQL Server database context using .NET Aspire
builder.AddSqlServerDbContext<OrganizationDbContext>("organizationdb");

// Add services to the container
builder.Services.AddControllers();

// Configure JWT Settings (consistent with IdentityService)
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Validate JWT settings at startup
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
if (jwtSettings == null)
{
    throw new InvalidOperationException($"Missing {JwtSettings.SectionName} configuration section");
}
jwtSettings.Validate();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

builder.Services.AddAuthorization();

// Register application services
builder.Services.AddScoped<IOrganizationService, ProjectManagementSystem.OrganizationService.Services.OrganizationService>();
builder.Services.AddScoped<IOrganizationMemberService, OrganizationMemberService>();

// Register authentication delegating handler
builder.Services.AddScoped<AuthenticationDelegatingHandler>();
builder.Services.AddHttpContextAccessor();

// Register HttpClient for UserService to call IdentityService
builder.Services.AddHttpClient<IUserService, UserService>(client =>
{
    // The service discovery will resolve https://identity-service automatically
    client.BaseAddress = new Uri("https://identity-service/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Add OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Organization Service API", Version = "v1" });
    
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

// Removed UseHttpsRedirection() as this is an internal service behind API Gateway
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


app.Run();