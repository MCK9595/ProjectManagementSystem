using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ProjectManagementSystem.OrganizationService.Data;
using ProjectManagementSystem.OrganizationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add SQL Server database context using .NET Aspire
builder.AddSqlServerDbContext<OrganizationDbContext>(connectionName: "organizationdb");

// Add services to the container
builder.Services.AddControllers();

// Add JWT Authentication (should match IdentityService configuration)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "ProjectManagementSystem-Super-Secret-Key-For-JWT-Token-Generation-2025";
var issuer = jwtSettings["Issuer"] ?? "ProjectManagementSystem.IdentityService";
var audience = jwtSettings["Audience"] ?? "ProjectManagementSystem.Users";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Register application services
builder.Services.AddScoped<IOrganizationService, ProjectManagementSystem.OrganizationService.Services.OrganizationService>();
builder.Services.AddScoped<IOrganizationMemberService, OrganizationMemberService>();

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