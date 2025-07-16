using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.TaskService.Data.Entities;

namespace ProjectManagementSystem.TaskService.Data;

public class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options)
    {
    }

    public DbSet<Entities.Task> Tasks { get; set; }
    public DbSet<Comment> Comments { get; set; }
    public DbSet<TaskDependency> TaskDependencies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Task entity
        modelBuilder.Entity<Entities.Task>(entity =>
        {
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            
            // Configure decimal properties with explicit precision to resolve EF Core 9.0 pending changes
            entity.Property(e => e.EstimatedHours)
                .HasPrecision(18, 2);
                
            entity.Property(e => e.ActualHours)
                .HasPrecision(18, 2);
            
            // プロジェクト内でのタスク番号ユニーク制約
            entity.HasIndex(e => new { e.ProjectId, e.TaskNumber })
                .IsUnique()
                .HasDatabaseName("IX_Tasks_ProjectId_TaskNumber");

            entity.HasIndex(e => e.ProjectId)
                .HasDatabaseName("IX_Tasks_ProjectId");

            entity.HasIndex(e => e.AssignedToUserId)
                .HasDatabaseName("IX_Tasks_AssignedToUserId");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_Tasks_Status");

            entity.HasIndex(e => e.Priority)
                .HasDatabaseName("IX_Tasks_Priority");
            
            entity.HasIndex(e => e.ParentTaskId)
                .HasDatabaseName("IX_Tasks_ParentTaskId");
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
                
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Configure relationship with Comments
            entity.HasMany(t => t.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure task hierarchy relationship
            entity.HasOne(t => t.ParentTask)
                .WithMany(t => t.SubTasks)
                .HasForeignKey(t => t.ParentTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure task dependency relationships
            entity.HasMany(t => t.Dependencies)
                .WithOne(d => d.Task)
                .HasForeignKey(d => d.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.DependentTasks)
                .WithOne(d => d.DependentOnTask)
                .HasForeignKey(d => d.DependentOnTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Comment entity
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasIndex(e => e.TaskId)
                .HasDatabaseName("IX_Comments_TaskId");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Comments_UserId");
                
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
                
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // Configure TaskDependency entity
        modelBuilder.Entity<TaskDependency>(entity =>
        {
            entity.HasIndex(e => e.TaskId)
                .HasDatabaseName("IX_TaskDependencies_TaskId");

            entity.HasIndex(e => e.DependentOnTaskId)
                .HasDatabaseName("IX_TaskDependencies_DependentOnTaskId");

            // Ensure a task cannot depend on itself and no duplicate dependencies
            entity.HasIndex(e => new { e.TaskId, e.DependentOnTaskId })
                .IsUnique()
                .HasDatabaseName("IX_TaskDependencies_TaskId_DependentOnTaskId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var taskEntries = ChangeTracker.Entries<Entities.Task>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in taskEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        var commentEntries = ChangeTracker.Entries<Comment>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in commentEntries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}