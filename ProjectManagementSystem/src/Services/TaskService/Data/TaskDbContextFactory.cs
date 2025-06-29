using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectManagementSystem.TaskService.Data;

public class TaskDbContextFactory : IDesignTimeDbContextFactory<TaskDbContext>
{
    public TaskDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TaskDbContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Server=localhost,1433;Database=projectmanagement_taskdb;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;";
        optionsBuilder.UseSqlServer(connectionString);

        return new TaskDbContext(optionsBuilder.Options);
    }
}