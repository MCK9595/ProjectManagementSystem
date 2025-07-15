using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ProjectManagementSystem.TaskService.Data;
using ProjectManagementSystem.TaskService.Services;
using ProjectManagementSystem.Shared.Common.Configuration;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add Azure SQL Server DbContext
builder.AddSqlServerDbContext<TaskDbContext>("taskdb");

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

// Add HttpContextAccessor for authentication forwarding
builder.Services.AddHttpContextAccessor();

// Register services
builder.Services.AddScoped<ITaskService, ProjectManagementSystem.TaskService.Services.TaskService>();
builder.Services.AddScoped<ITaskCommentService, TaskCommentService>();

// Register HttpClient for UserService to call IdentityService
builder.Services.AddHttpClient<IUserService, UserService>(client =>
{
    // The service discovery will resolve https://identity-service automatically
    client.BaseAddress = new Uri("https://identity-service/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Register HttpClient for ProjectService to call ProjectService
builder.Services.AddHttpClient<IProjectService, ProjectService>(client =>
{
    // The service discovery will resolve https://project-service automatically
    client.BaseAddress = new Uri("https://project-service/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
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
    app.UseSwaggerUI();
}

// Map service default endpoints.
app.MapDefaultEndpoints();

// Removed UseHttpsRedirection() as this is an internal service behind API Gateway
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();


app.Run();
