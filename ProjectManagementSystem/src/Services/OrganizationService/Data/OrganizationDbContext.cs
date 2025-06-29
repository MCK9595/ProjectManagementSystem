using Microsoft.EntityFrameworkCore;
using ProjectManagementSystem.OrganizationService.Data.Entities;

namespace ProjectManagementSystem.OrganizationService.Data;

public class OrganizationDbContext : DbContext
{
    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options)
    {
    }

    public DbSet<Organization> Organizations { get; set; }
    public DbSet<OrganizationUser> OrganizationUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Organization entity
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("NOW()");
                
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("NOW()");

            // Configure relationship with OrganizationUsers
            entity.HasMany(o => o.Members)
                .WithOne(ou => ou.Organization)
                .HasForeignKey(ou => ou.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure OrganizationUser entity
        modelBuilder.Entity<OrganizationUser>(entity =>
        {
            // Composite unique index to prevent duplicate user-organization pairs
            entity.HasIndex(e => new { e.OrganizationId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_OrganizationUsers_OrganizationId_UserId");
                
            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("NOW()");
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
        var entries = ChangeTracker.Entries<Organization>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}