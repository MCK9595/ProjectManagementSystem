# Security Configuration Setup

## JWT Secret Key Configuration

For security reasons, the JWT secret key is not stored in the source code. This system uses .NET Aspire's external parameters feature to manage JWT secrets consistently across all microservices.

### Option 1: Using AppHost Parameters (Recommended)

The system now uses .NET Aspire's external parameters for centralized JWT secret management. The JWT secret key is automatically distributed to all microservices through the AppHost configuration.

1. **For Development (AppHost configuration):**
   The JWT secret key is set in the AppHost's `appsettings.json`:
   ```json
   {
     "Parameters": {
       "jwt-secret-key": "DefaultSecretKeyForDevelopment-ChangeInProduction-32CharactersLong!"
     }
   }
   ```

2. **For Production (Environment Variables):**
   Set the following environment variable:
   ```bash
   export JWT_SECRET_KEY="your-super-secret-key-at-least-32-characters-long"
   ```

   The AppHost will automatically distribute this secret to all microservices.

### Option 2: Using Environment Variables (Alternative)

Set the following environment variable:
```bash
export JWT_SECRET_KEY="your-super-secret-key-at-least-32-characters-long"
```

**Note:** The system now uses `JWT_SECRET_KEY` as the standard environment variable name, which is automatically injected into all microservices by the AppHost.

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
5. **AUTOMATIC SECRET DISTRIBUTION**: The AppHost automatically ensures all microservices use the same JWT secret key through .NET Aspire's external parameters feature. This eliminates the risk of secret key mismatches between services.

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
JWT secret key configuration issue or AppHost parameter not properly set.

**Solution:**
1. **Check AppHost Configuration:**
   Verify the JWT secret key is properly configured in the AppHost's `appsettings.json`:
   ```bash
   cd ProjectManagementSystem/ProjectManagementSystem/ProjectManagementSystem.AppHost
   cat appsettings.json
   ```

2. **Check Environment Variable (Production):**
   If running in production, ensure the environment variable is set:
   ```bash
   echo $JWT_SECRET_KEY
   ```

3. **Verify Parameter Distribution:**
   The AppHost should automatically distribute the JWT secret to all microservices. Check the AppHost logs for any parameter-related errors.

4. **Restart the Application:**
   ```bash
   dotnet build
   dotnet run --project ProjectManagementSystem.AppHost
   ```

**Verification:**
- Organization page should load data instead of showing errors
- Project creation page should work without logout
- All protected API endpoints should accept JWT tokens properly

### Azure Container Apps Deployment

For Azure Container Apps deployment, set the JWT secret as an environment variable:

1. **Using Azure CLI:**
   ```bash
   az containerapp update \
     --name your-app-name \
     --resource-group your-resource-group \
     --set-env-vars JWT_SECRET_KEY=your-secret-key
   ```

2. **Using Azure Portal:**
   - Navigate to your Container App
   - Go to Settings → Environment variables
   - Add `JWT_SECRET_KEY` with your secret value

The AppHost will automatically distribute this secret to all microservices through the .NET Aspire external parameters system.