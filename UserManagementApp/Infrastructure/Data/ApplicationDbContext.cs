using Microsoft.EntityFrameworkCore;

namespace UserManagementApp.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Email uniqueness is enforced by the database, not just the app.
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email_Unique");

        // Store the UserStatus enum as an integer in the database.
        modelBuilder.Entity<User>()
            .Property(u => u.Status)
            .HasConversion<int>();
    }

    // Auto-populate CreatedAt and UpdatedAt on every SaveChanges.
    public override int SaveChanges(bool acceptAllOnSuccess)
    {
        PopulateTimestamps();
        return base.SaveChanges(acceptAllOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllOnSuccess, CancellationToken cancellationToken = default)
    {
        PopulateTimestamps();
        return base.SaveChangesAsync(acceptAllOnSuccess, cancellationToken);
    }

    private void PopulateTimestamps()
    {
        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<User>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
            }
            entry.Entity.UpdatedAt = utcNow;
        }
    }
}
