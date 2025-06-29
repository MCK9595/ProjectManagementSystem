using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProjectManagementSystem.TaskService.Data;

public class TaskDbContextFactory : IDesignTimeDbContextFactory<TaskDbContext>
{
    public TaskDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TaskDbContext>();
        
        // Use a default connection string for design-time operations
        var connectionString = "Host=localhost;Database=projectmanagement_taskdb;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new TaskDbContext(optionsBuilder.Options);
    }
}