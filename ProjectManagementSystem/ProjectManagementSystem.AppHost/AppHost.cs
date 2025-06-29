var builder = DistributedApplication.CreateBuilder(args);

// データベースの追加 (Azure SQL Server - ローカルではコンテナ、本番ではAzure SQL Database)
var azureSql = builder.AddAzureSqlServer("azuresql")
    .RunAsContainer(container => container.WithDataVolume());

var identityDb = azureSql.AddDatabase("identitydb");
var organizationDb = azureSql.AddDatabase("organizationdb");
var projectDb = azureSql.AddDatabase("projectdb");
var taskDb = azureSql.AddDatabase("taskdb");

// MigrationService - データベースマイグレーションを最初に実行（Microsoft Docs準拠）
var migrationService = builder.AddProject<Projects.ProjectManagementSystem_MigrationService>("migration-service")
    .WithReference(azureSql)
    .WithReference(identityDb)
    .WithReference(organizationDb)
    .WithReference(projectDb)
    .WithReference(taskDb)
    .WaitFor(azureSql);

// IdentityService - MigrationServiceの完了を待機してから起動
var identityService = builder.AddProject<Projects.ProjectManagementSystem_IdentityService>("identity-service")
    .WithReference(identityDb)
    .WaitFor(migrationService);

// OrganizationService - MigrationServiceとIdentityServiceの起動を待機
var organizationService = builder.AddProject<Projects.ProjectManagementSystem_OrganizationService>("organization-service")
    .WithReference(organizationDb)
    .WithReference(identityService)
    .WaitFor(migrationService)
    .WaitFor(identityService);

// ProjectService - MigrationService、IdentityService、OrganizationServiceの起動を待機
var projectService = builder.AddProject<Projects.ProjectManagementSystem_ProjectService>("project-service")
    .WithReference(projectDb)
    .WithReference(identityService)
    .WithReference(organizationService)
    .WaitFor(migrationService)
    .WaitFor(identityService)
    .WaitFor(organizationService);

// TaskService - MigrationService、IdentityService、ProjectServiceの起動を待機
var taskService = builder.AddProject<Projects.ProjectManagementSystem_TaskService>("task-service")
    .WithReference(taskDb)
    .WithReference(identityService)
    .WithReference(projectService)
    .WaitFor(migrationService)
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
