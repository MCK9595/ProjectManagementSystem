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
    .WithReference(identityDb)
    .WithReference(organizationDb)
    .WithReference(projectDb)
    .WithReference(taskDb)
    .WaitFor(identityDb)
    .WaitFor(organizationDb)
    .WaitFor(projectDb)
    .WaitFor(taskDb);

// IdentityService - MigrationServiceの完了を待機してから起動
var identityService = builder.AddProject<Projects.ProjectManagementSystem_IdentityService>("identity-service")
    .WithReference(identityDb)
    .WaitForCompletion(migrationService);

// OrganizationService - MigrationServiceとIdentityServiceの起動を待機
var organizationService = builder.AddProject<Projects.ProjectManagementSystem_OrganizationService>("organization-service")
    .WithReference(organizationDb)
    .WithReference(identityService)
    .WaitForCompletion(migrationService)
    .WaitFor(identityService);

// ProjectService - MigrationService、IdentityService、OrganizationServiceの起動を待機
var projectService = builder.AddProject<Projects.ProjectManagementSystem_ProjectService>("project-service")
    .WithReference(projectDb)
    .WithReference(identityService)
    .WithReference(organizationService)
    .WaitForCompletion(migrationService)
    .WaitFor(identityService)
    .WaitFor(organizationService);

// TaskService - MigrationService、IdentityService、ProjectServiceの起動を待機
var taskService = builder.AddProject<Projects.ProjectManagementSystem_TaskService>("task-service")
    .WithReference(taskDb)
    .WithReference(identityService)
    .WithReference(projectService)
    .WaitForCompletion(migrationService)
    .WaitFor(identityService)
    .WaitFor(projectService);


// 外部パラメータの定義
var jwtSecretKey = builder.AddParameter("jwt-secret-key", secret: true);

// API Gateway - カスタム実装を使用
var apiGateway = builder.AddProject<Projects.ProjectManagementSystem_ApiServiceGateway>("api-gateway")
    .WithReference(identityService)
    .WithReference(organizationService)
    .WithReference(projectService)
    .WithReference(taskService)
    .WithEnvironment("JWT_SECRET_KEY", jwtSecretKey)
    .WithExternalHttpEndpoints()
    .WaitFor(identityService)
    .WaitFor(organizationService)
    .WaitFor(projectService)
    .WaitFor(taskService);

// WebApp.Wasm - API Gatewayの起動を待機
var webApp = builder.AddProject<Projects.ProjectManagementSystem_WebApp_Wasm>("webapp-wasm")
    .WithEnvironment("Services__api-gateway__http__0", apiGateway.GetEndpoint("http"))
    .WithEnvironment("Services__api-gateway__https__0", apiGateway.GetEndpoint("https"))
    .WithExternalHttpEndpoints()
    .WaitFor(apiGateway);

// サービスディスカバリーベースの統一設定（環境分岐なし）
apiGateway
    .WithEnvironment("AZURE_WEBAPP_URL", webApp.GetEndpoint("https"))
    .WithEnvironment("LOCAL_WEBAPP_URL", webApp.GetEndpoint("http"));

webApp
    .WithEnvironment("AZURE_API_GATEWAY_URL", apiGateway.GetEndpoint("https"));

// 各サービスの JWT 設定（統一）
identityService
    .WithEnvironment("JWT_SECRET_KEY", jwtSecretKey);

organizationService
    .WithEnvironment("JWT_SECRET_KEY", jwtSecretKey);

projectService
    .WithEnvironment("JWT_SECRET_KEY", jwtSecretKey);

taskService
    .WithEnvironment("JWT_SECRET_KEY", jwtSecretKey);


builder.Build().Run();
