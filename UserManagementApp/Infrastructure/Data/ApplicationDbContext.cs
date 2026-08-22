using Microsoft.EntityFrameworkCore;

namespace UserManagementApp.Infrastructure.Data;

/// <summary>
/// Application database context for the user management system.
/// The UNIQUE INDEX on Email is created here — not in application code —
/// so the database engine itself enforces email uniqueness.
/// </summary>
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

        // UNIQUE INDEX on Email — the ultimate guarantee against duplicate emails.
        // Distinct from the PK on Id (Guid).
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("IX_Users_Email_Unique");

        // Store the UserStatus enum as an integer in the database
        modelBuilder.Entity<User>()
            .Property(u => u.Status)
            .HasConversion<int>();
    }

    // Auto-populate CreatedAt and UpdatedAt timestamps on every SaveChanges
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
