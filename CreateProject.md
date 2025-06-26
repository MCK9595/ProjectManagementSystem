# プロジェクト作成戦略

本プロジェクトは.NET Aspireをベースとしたマイクロサービスアーキテクチャで構築されます。プロジェクトの作成は、以下の手順と戦略に従って行います。

## 1. .NET Aspireプロジェクトの初期化

まず、ソリューションのルートとなる.NET Aspireプロジェクトを作成します。これにより、`AppHost`プロジェクトと`ServiceDefaults`プロジェクトが自動的に生成されます。

```bash
dotnet new aspire -n ProjectManagementSystem
```

- `ProjectManagementSystem.AppHost`: 各マイクロサービスをオーケストレーションし、実行環境を定義するプロジェクトです。
- `ProjectManagementSystem.ServiceDefaults`: 各マイクロサービス間で共有される設定（ロギング、トレース、ヘルスチェックなど）を定義するプロジェクトです。

## 2. 各マイクロサービスのプロジェクト作成

`README.md` の「システムアーキテクチャ」セクションで定義された各マイクロサービス（`ApiServiceGateway`, `IdentityService`, `OrganizationService`, `ProjectService`, `TaskService`, `WebApp`）を、それぞれ独立したプロジェクトとして作成します。

各サービスは、その役割に応じて適切なプロジェクトテンプレートを選択します。

- **APIサービス (IdentityService, OrganizationService, ProjectService, TaskService):**
  ASP.NET Core Web APIプロジェクトとして作成します。
  ```bash
  dotnet new webapi -n ProjectManagementSystem.IdentityService -o src/Services/IdentityService
  dotnet new webapi -n ProjectManagementSystem.OrganizationService -o src/Services/OrganizationService
  dotnet new webapi -n ProjectManagementSystem.ProjectService -o src/Services/ProjectService
  dotnet new webapi -n ProjectManagementSystem.TaskService -o src/Services/TaskService
  ```

- **APIゲートウェイ (ApiServiceGateway):**
  ASP.NET Core Web APIプロジェクトとして作成し、OcelotなどのAPIゲートウェイライブラリを導入します。
  ```bash
  dotnet new webapi -n ProjectManagementSystem.ApiServiceGateway -o src/Gateways/ApiServiceGateway
  ```

- **フロントエンド (WebApp):**
  Blazor Web Appプロジェクトとして作成します。クライアント側のレンダリングモードを中心に実装するため、適切なテンプレートを選択します。
  ```bash
  dotnet new blazor -n ProjectManagementSystem.WebApp -o src/WebApps/WebApp --interactivity Client --no-host-authentication
  ```

## 3. テストプロジェクトの作成

単体テストおよび結合テスト用のプロジェクトを作成します。各マイクロサービスに対して個別の単体テストプロジェクトを作成し、分散アプリケーション全体の結合テストを行うための専用プロジェクトを作成します。

- **単体テストプロジェクト (xUnit):**
  各サービスに対応するテストプロジェクトを作成します。例: `IdentityService` の単体テストプロジェクト
  ```bash
  dotnet new xunit -n ProjectManagementSystem.IdentityService.Tests -o tests/UnitTests/IdentityService.Tests
  dotnet new xunit -n ProjectManagementSystem.OrganizationService.Tests -o tests/UnitTests/OrganizationService.Tests
  dotnet new xunit -n ProjectManagementSystem.ProjectService.Tests -o tests/UnitTests/ProjectService.Tests
  dotnet new xunit -n ProjectManagementSystem.TaskService.Tests -o tests/UnitTests/TaskService.Tests
  dotnet new xunit -n ProjectManagementSystem.ApiServiceGateway.Tests -o tests/UnitTests/ApiServiceGateway.Tests
  dotnet new xunit -n ProjectManagementSystem.WebApp.Tests -o tests/UnitTests/WebApp.Tests
  ```

- **結合テストプロジェクト (.NET Aspire対応 xUnit):**
  分散アプリケーション全体の結合テストを行うためのプロジェクトを作成します。Aspireのテストテンプレートを使用します。
  ```bash
  dotnet new aspire-xunit -n ProjectManagementSystem.IntegrationTests -o tests/IntegrationTests
  ```

## 4. ソリューションへのプロジェクト追加

作成した各プロジェクトを、`ProjectManagementSystem.sln` ソリューションファイルに追加します。

```bash
dotnet sln add src/Services/IdentityService/ProjectManagementSystem.IdentityService.csproj
dotnet sln add src/Services/OrganizationService/ProjectManagementSystem.OrganizationService.csproj
dotnet sln add src/Services/ProjectService/ProjectManagementSystem.ProjectService.csproj
dotnet sln add src/Services/TaskService/ProjectManagementSystem.TaskService.csproj
dotnet sln add src/Gateways/ApiServiceGateway/ProjectManagementSystem.ApiServiceGateway.csproj
dotnet sln add src/WebApps/WebApp/ProjectManagementSystem.WebApp.csproj

dotnet sln add tests/UnitTests/IdentityService.Tests/ProjectManagementSystem.IdentityService.Tests.csproj
dotnet sln add tests/UnitTests/OrganizationService.Tests/ProjectManagementSystem.OrganizationService.Tests.csproj
dotnet sln add tests/UnitTests/ProjectService.Tests/ProjectManagementSystem.ProjectService.Tests.csproj
dotnet sln add tests/UnitTests/TaskService.Tests/ProjectManagementSystem.TaskService.Tests.csproj
dotnet sln add tests/UnitTests/ApiServiceGateway.Tests/ProjectManagementSystem.ApiServiceGateway.Tests.csproj
dotnet sln add tests/UnitTests/WebApp.Tests/ProjectManagementSystem.WebApp.Tests.csproj

dotnet sln add tests/IntegrationTests/ProjectManagementSystem.IntegrationTests.csproj
```

## 5. AppHostプロジェクトからの参照追加とサービス登録

`ProjectManagementSystem.AppHost`プロジェクトから、各マイクロサービスプロジェクトへの参照を追加し、`Program.cs`でサービスとして登録します。これにより、.NET Aspireが各サービスをオーケストレーションできるようになります。

例: `ProjectManagementSystem.AppHost/Program.cs`

```csharp
// AppHostプロジェクトのルートディレクトリで実行
dotnet add ProjectManagementSystem.AppHost/ProjectManagementSystem.AppHost.csproj reference src/Services/IdentityService/ProjectManagementSystem.IdentityService.csproj
// ... 他のサービスも同様に追加
```

`ProjectManagementSystem.AppHost/Program.cs` の内容を以下のように更新します。

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// データベースの追加 (PostgreSQLを想定)
var postgres = builder.AddPostgres("postgres");
var identityDb = postgres.AddDatabase("identitydb");
var organizationDb = postgres.AddDatabase("organizationdb");
var projectDb = postgres.AddDatabase("projectdb");
var taskDb = postgres.AddDatabase("taskdb");

// IdentityService
var identityService = builder.AddProject<Projects.ProjectManagementSystem_IdentityService>("identity-service")
    .WithReference(identityDb);

// OrganizationService
var organizationService = builder.AddProject<Projects.ProjectManagementSystem_OrganizationService>("organization-service")
    .WithReference(organizationDb);

// ProjectService
var projectService = builder.AddProject<Projects.ProjectManagementSystem_ProjectService>("project-service")
    .WithReference(projectDb);

// TaskService
var taskService = builder.AddProject<Projects.ProjectManagementSystem_TaskService>("task-service")
    .WithReference(taskDb);

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
```

## 6. ServiceDefaultsプロジェクトの参照追加

各マイクロサービスプロジェクトから `ProjectManagementSystem.ServiceDefaults` プロジェクトへの参照を追加し、`Program.cs`で`AddServiceDefaults()`と`MapDefaultEndpoints()`を呼び出します。これにより、Aspireの提供する共通機能（サービスディスカバリ、ヘルスチェック、テレメトリなど）を各サービスで利用できるようになります。

例: `src/Services/IdentityService/ProjectManagementSystem.IdentityService.csproj` の `Program.cs`

```csharp
// 各サービスプロジェクトのルートディレクトリで実行
dotnet add src/Services/IdentityService/ProjectManagementSystem.IdentityService.csproj reference ../../ServiceDefaults/ProjectManagementSystem.ServiceDefaults.csproj
// ... 他のサービスも同様に追加
```

`Program.cs` の内容を以下のように更新します。

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults(); // これを追加

// ... その他の設定

var app = builder.Build();

app.MapDefaultEndpoints(); // これを追加

// ... その他のミドルウェア

app.Run();
```

## 7. データベースのセットアップ

各サービスが必要とするデータベース（IdentityService用、OrganizationService用など）をPostgreSQLコンテナ内に作成し、接続文字列をAspireのAppHostで管理します。

## 8. 開発環境での実行

`ProjectManagementSystem.AppHost`プロジェクトをスタートアッププロジェクトに設定し、実行することで、全てのマイクロサービスがDockerコンテナとして起動し、Aspire Dashboardで監視できるようになります。

```bash
dotnet run --project ProjectManagementSystem.AppHost
```

## 9. 今後の作業

- 各サービスにおけるAPIエンドポイントの実装
- Entity Framework Coreを使用したデータモデルの実装とマイグレーション
- 認証・認可ロジックの実装
- Blazor Web AppでのUI実装とAPI連携
- テストコードの作成