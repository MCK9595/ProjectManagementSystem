using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectManagementSystem.ProjectService.Data;

public class ProjectDbContextFactory : IDesignTimeDbContextFactory<ProjectDbContext>
{
    public ProjectDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProjectDbContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Server=localhost,1433;Database=projectmanagement_projectdb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;";
        optionsBuilder.UseSqlServer(connectionString);

        return new ProjectDbContext(optionsBuilder.Options);
    }
}