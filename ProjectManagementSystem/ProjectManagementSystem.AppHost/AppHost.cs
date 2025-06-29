var builder = DistributedApplication.CreateBuilder(args);

// データベースの追加 (PostgreSQL)
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var identityDb = postgres.AddDatabase("identitydb");
var organizationDb = postgres.AddDatabase("organizationdb");
var projectDb = postgres.AddDatabase("projectdb");
var taskDb = postgres.AddDatabase("taskdb");

// IdentityService - 基盤サービスとして最初に起動
var identityService = builder.AddProject<Projects.ProjectManagementSystem_IdentityService>("identity-service")
    .WithReference(identityDb)
    .WaitFor(identityDb);

// OrganizationService - IdentityServiceの起動を待機
var organizationService = builder.AddProject<Projects.ProjectManagementSystem_OrganizationService>("organization-service")
    .WithReference(organizationDb)
    .WithReference(identityService)
    .WaitFor(organizationDb)
    .WaitFor(identityService);

// ProjectService - IdentityServiceとOrganizationServiceの起動を待機
var projectService = builder.AddProject<Projects.ProjectManagementSystem_ProjectService>("project-service")
    .WithReference(projectDb)
    .WithReference(identityService)
    .WithReference(organizationService)
    .WaitFor(projectDb)
    .WaitFor(identityService)
    .WaitFor(organizationService);

// TaskService - IdentityServiceとProjectServiceの起動を待機
var taskService = builder.AddProject<Projects.ProjectManagementSystem_TaskService>("task-service")
    .WithReference(taskDb)
    .WithReference(identityService)
    .WithReference(projectService)
    .WaitFor(taskDb)
    .WaitFor(identityService)
    .WaitFor(projectService);


// API Gateway - .NET Aspire 9.x YARP統合を使用
var apiGateway = builder.AddYarp("api-gateway")
    .WithConfigFile("yarp.json")
    .WithReference(identityService)
    .WithReference(organizationService)
    .WithReference(projectService)
    .WithReference(taskService)
    .WaitFor(identityService)
    .WaitFor(organizationService)
    .WaitFor(projectService)
    .WaitFor(taskService);

// WebApp - API Gatewayの起動を待機
builder.AddProject<Projects.ProjectManagementSystem_WebApp>("webapp")
    .WithEnvironment("Services__api-gateway__http__0", apiGateway.GetEndpoint("http"))
    .WithExternalHttpEndpoints()
    .WaitFor(apiGateway);


builder.Build().Run();
