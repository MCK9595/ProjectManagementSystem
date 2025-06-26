var builder = DistributedApplication.CreateBuilder(args);

// データベースの追加 (PostgreSQL)
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var identityDb = postgres.AddDatabase("identitydb");
var organizationDb = postgres.AddDatabase("organizationdb");
var projectDb = postgres.AddDatabase("projectdb");
var taskDb = postgres.AddDatabase("taskdb");

// IdentityService
var identityService = builder.AddProject<Projects.ProjectManagementSystem_IdentityService>("identity-service")
    .WithReference(identityDb);

// OrganizationService
var organizationService = builder.AddProject<Projects.ProjectManagementSystem_OrganizationService>("organization-service")
    .WithReference(organizationDb)
    .WithReference(identityService);

// ProjectService
var projectService = builder.AddProject<Projects.ProjectManagementSystem_ProjectService>("project-service")
    .WithReference(projectDb)
    .WithReference(identityService)
    .WithReference(organizationService);

// TaskService
var taskService = builder.AddProject<Projects.ProjectManagementSystem_TaskService>("task-service")
    .WithReference(taskDb)
    .WithReference(identityService)
    .WithReference(projectService);

// ApiServiceGateway
var apiServiceGateway = builder.AddProject<Projects.ProjectManagementSystem_ApiServiceGateway>("api-gateway")
    .WithReference(identityService)
    .WithReference(organizationService)
    .WithReference(projectService)
    .WithReference(taskService);

// WebApp
builder.AddProject<Projects.ProjectManagementSystem_WebApp>("webapp")
    .WithReference(apiServiceGateway);

builder.Build().Run();
