# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Project Management System built with .NET Aspire microservices architecture. The system manages organizations, projects, and tasks in a hierarchical structure with Role-Based Access Control (RBAC).

## Key Commands

### Build and Run
```bash
# Build entire solution
dotnet build

# Run the entire application (starts all microservices)
dotnet run --project ProjectManagementSystem.AppHost

# Run tests
dotnet test

# Run specific service tests
dotnet test tests/UnitTests/IdentityService.Tests/ProjectManagementSystem.IdentityService.Tests.csproj

# Run integration tests
dotnet test tests/IntegrationTests/ProjectManagementSystem.IntegrationTests.csproj
```

### Development Commands
```bash
# Add EF Core migrations (run from service directory)
dotnet ef migrations add InitialCreate -o Data/Migrations

# Update database
dotnet ef database update

# Add package to a specific project
dotnet add [project-path] package [package-name]

# Create new migration for a service (example for IdentityService)
cd src/Services/IdentityService
dotnet ef migrations add [MigrationName] -o Data/Migrations
```

### Database Migration Requirements
**IMPORTANT**: When modifying any Entity Framework Core entity classes or DbContext configurations, you MUST create a new migration:

1. **Navigate to the service directory** containing the modified entity:
   ```bash
   cd src/Services/[ServiceName]
   ```

2. **Create a new migration** with a descriptive name:
   ```bash
   dotnet ef migrations add [DescriptiveMigrationName] -o Data/Migrations
   ```

3. **Examples for each service**:
   ```bash
   # IdentityService
   cd src/Services/IdentityService
   dotnet ef migrations add AddUserProfileFields -o Data/Migrations

   # OrganizationService
   cd src/Services/OrganizationService
   dotnet ef migrations add AddOrganizationSettings -o Data/Migrations

   # ProjectService
   cd src/Services/ProjectService
   dotnet ef migrations add AddProjectBudgetField -o Data/Migrations

   # TaskService
   cd src/Services/TaskService
   dotnet ef migrations add AddTaskPriorityIndex -o Data/Migrations
   ```

4. **Migration will be automatically applied** by the MigrationService when running `aspire run`

**Note**: Never modify or delete existing migration files. Always create new migrations for schema changes.

## Architecture Overview

### Microservices Structure
The system consists of the following services orchestrated by .NET Aspire:

1. **AppHost** (`ProjectManagementSystem.AppHost/`)
   - Central orchestration point
   - Configures all services and databases
   - Manages service discovery and dependencies

2. **ServiceDefaults** (`ProjectManagementSystem.ServiceDefaults/`)
   - Shared configuration for all services
   - Implements telemetry, health checks, and service discovery

3. **API Gateway** (`src/Gateways/ApiServiceGateway/`)
   - Single entry point for all API calls
   - Routes requests to appropriate microservices
   - Implements cross-cutting concerns (auth, rate limiting)

4. **Microservices** (`src/Services/`)
   - **IdentityService**: User authentication and global user management
   - **OrganizationService**: Organization and membership management
   - **ProjectService**: Project and project member management
   - **TaskService**: Task, assignee, and comment management

5. **Frontend** (`src/WebApps/WebApp/`)
   - Blazor WebAssembly application
   - Communicates through API Gateway

6. **Shared Libraries** (`src/Shared/`)
   - **Shared.Models**: DTOs for all services
   - **Shared.Common**: Common utilities, exceptions, constants

### Database Strategy
- Each microservice has its own PostgreSQL database
- Databases are managed through AppHost configuration
- Connection strings are injected via .NET Aspire

### Service Communication
- Services communicate via HTTP/gRPC through service discovery
- API Gateway uses Ocelot for routing
- All services integrate with Aspire telemetry

## Important Design Decisions

### Authentication & Authorization
- JWT-based authentication implemented in IdentityService
- RBAC with hierarchical permissions:
  - SystemAdmin: Full system control
  - Organization roles: OrgAdmin, OrgMember
  - Project roles: ProjectAdmin, ProjectEditor, ProjectViewer

### Logging Strategy
- All services use ILogger<T> for structured logging
- Logs are collected via OpenTelemetry
- Aspire Dashboard provides real-time monitoring

### API Design
- RESTful APIs with standardized response format
- Common error handling through shared exceptions
- Pagination support via PagedResult<T>

## Development Workflow

### Adding New Features
1. Update shared models if needed (`src/Shared/ProjectManagementSystem.Shared.Models/`)
2. Implement service logic in appropriate microservice
3. Add/update API endpoints
4. Update API Gateway routing if new endpoints added
5. Implement frontend components
6. Write unit tests for new functionality
7. Test with integration tests

### Running Locally
1. Ensure Docker Desktop is running
2. Run `dotnet run --project ProjectManagementSystem.AppHost`
3. Access Aspire Dashboard at the URL shown in console
4. Frontend typically runs on https://localhost:7XXX
5. API Gateway typically runs on https://localhost:5XXX

### Testing Strategy
- Unit tests for each service in `tests/UnitTests/`
- Integration tests using Aspire.Hosting.Testing
- Use xUnit for all tests
- Mock external dependencies with Moq

## Critical Development Rules

### Test Code Requirements
**IMPORTANT**: When adding any new functionality or modifying existing code, you MUST write corresponding test code. This includes:
- Unit tests for all new methods and classes
- Integration tests for new API endpoints
- Update existing tests when modifying functionality
- Ensure test coverage for both success and failure scenarios

### Build and Test Verification
**MANDATORY**: At the completion of EVERY task, you MUST:
1. Run `dotnet build` to ensure the entire solution builds successfully
2. Run `dotnet test` to verify all tests pass
3. Only consider a task complete when both build and tests succeed
4. If any build errors or test failures occur, fix them before proceeding

### ToDo.md Task Management
When working with tasks from `ToDo.md`:
- Mark completed tasks by changing `[ ]` to `[x]`
- Update the task status immediately upon completion
- Example: Change `- [ ] Create User entity` to `- [x] Create User entity`
- Keep the ToDo.md file updated as the single source of truth for project progress

## Current Development Status

The project structure is complete with:
- All microservices created and configured
- Shared libraries with DTOs and common utilities
- AppHost configuration with PostgreSQL databases
- ServiceDefaults integration in all services
- Test project structure ready
- Authentication system implemented and working

### Default Administrator Account
For testing and initial setup, a default administrator account is automatically created:
- **Email**: admin@projectmanagement.com
- **Username**: admin
- **Password**: AdminPassword123!
- **Role**: SystemAdmin

### Authentication Features
- JWT-based authentication with refresh tokens
- Role-based access control (RBAC)
- Account lockout protection after failed login attempts
- Secure password hashing with ASP.NET Core Identity

Next steps typically involve:
- Implementing Entity Framework Core models for other services
- Creating API endpoints in each service
- Setting up Ocelot in API Gateway
- Building additional Blazor UI components