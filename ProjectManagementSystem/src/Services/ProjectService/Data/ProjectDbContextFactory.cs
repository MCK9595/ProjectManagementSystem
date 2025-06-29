using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectManagementSystem.ProjectService.Data;

public class ProjectDbContextFactory : IDesignTimeDbContextFactory<ProjectDbContext>
{
    public ProjectDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProjectDbContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Host=localhost;Database=projectmanagement_projectdb;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new ProjectDbContext(optionsBuilder.Options);
    }
}