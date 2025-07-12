# Security Configuration Setup

## JWT Secret Key Configuration

For security reasons, the JWT secret key is not stored in the source code. You must configure it using one of the following methods:

### Option 1: Using User Secrets (Development Only)

1. Navigate to the IdentityService project directory:
   ```bash
   cd ProjectManagementSystem/src/Services/IdentityService
   ```

2. Initialize user secrets:
   ```bash
   dotnet user-secrets init
   ```

3. Set the JWT secret key:
   ```bash
   dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-key-at-least-32-characters-long"
   ```

4. **IMPORTANT**: Set the same JWT secret key for ALL microservices to ensure proper token validation:

   **OrganizationService:**
   ```bash
   cd ../../Services/OrganizationService
   dotnet user-secrets init
   dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-key-at-least-32-characters-long"
   ```

   **ProjectService:**
   ```bash
   cd ../ProjectService
   dotnet user-secrets init
   dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-key-at-least-32-characters-long"
   ```

   **TaskService:**
   ```bash
   cd ../TaskService
   dotnet user-secrets init
   dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-key-at-least-32-characters-long"
   ```

   **API Gateway (if needed):**
   ```bash
   cd ../../Gateways/ApiServiceGateway
   dotnet user-secrets init
   dotnet user-secrets set "JwtSettings:SecretKey" "your-super-secret-key-at-least-32-characters-long"
   ```

### Option 2: Using Environment Variables

Set the following environment variable:
```bash
export JwtSettings__SecretKey="your-super-secret-key-at-least-32-characters-long"
```

Note: Use double underscores (`__`) as the delimiter for nested configuration keys.

### Option 3: Using Azure Key Vault or AWS Secrets Manager (Production)

For production environments, use a secure secret management service:

1. Store the JWT secret in your chosen secret management service
2. Configure your application to retrieve the secret at startup
3. See the respective documentation for Azure Key Vault or AWS Secrets Manager integration

## Important Security Notes

1. **Never commit secrets to source control**
2. **Use different keys for different environments** (development, staging, production)
3. **Rotate keys regularly** (recommended: every 90 days)
4. **Use strong, randomly generated keys** (minimum 32 characters)
5. **CRITICAL**: All microservices must use the SAME JWT secret key for proper token validation. A mismatch will cause 401 Unauthorized errors when accessing protected endpoints.

## Generating a Secure Key

You can generate a secure random key using:

### PowerShell:
```powershell
[System.Convert]::ToBase64String((1..32 | ForEach {Get-Random -Maximum 256}))
```

### Bash:
```bash
openssl rand -base64 32
```

### C# Console:
```csharp
var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
Console.WriteLine(key);
```

## Troubleshooting JWT Authentication Issues

### Common Problem: Organization/Project/Task Pages Return 401 Errors

**Symptoms:**
- Login succeeds but organization page shows "Error Loading Organizations"
- Project creation page causes logout and redirect to login
- API Gateway logs show successful authentication (200) but microservice calls return 401

**Root Cause:**
JWT secret key mismatch between IdentityService and other microservices.

**Solution:**
1. Check what JWT secret key is currently set for IdentityService:
   ```bash
   cd ProjectManagementSystem/src/Services/IdentityService
   dotnet user-secrets list
   ```

2. Use the SAME secret key for all other services:
   ```bash
   # Replace "your-actual-secret-key" with the key from step 1
   cd ../OrganizationService
   dotnet user-secrets set "JwtSettings:SecretKey" "your-actual-secret-key"
   
   cd ../ProjectService  
   dotnet user-secrets set "JwtSettings:SecretKey" "your-actual-secret-key"
   
   cd ../TaskService
   dotnet user-secrets set "JwtSettings:SecretKey" "your-actual-secret-key"
   ```

3. Rebuild and restart the application:
   ```bash
   dotnet build
   dotnet run --project ProjectManagementSystem.AppHost
   ```

**Verification:**
- Organization page should load data instead of showing errors
- Project creation page should work without logout
- All protected API endpoints should accept JWT tokens properly