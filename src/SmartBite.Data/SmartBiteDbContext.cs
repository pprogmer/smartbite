using Microsoft.EntityFrameworkCore;
using SmartBite.Contracts;

namespace SmartBite.Data;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public class Profile
{
    public int UserId { get; set; }
    public Sex Sex { get; set; }
    public int Age { get; set; }
    public int HeightCm { get; set; }
    public decimal WeightKg { get; set; }
    public ActivityLevel ActivityLevel { get; set; }
    public Goal Goal { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public ProductUnit Unit { get; set; }
    public ProductCategory Category { get; set; }
}

/// <summary>A diary record; nutrients are for one serving.</summary>
public class DiaryEntry
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public MealType MealType { get; set; }
    public string Name { get; set; } = "";
    public EntrySource Source { get; set; }
    public decimal Servings { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Fat { get; set; }
    public decimal Carbs { get; set; }
}

public class SmartBiteDbContext(DbContextOptions<SmartBiteDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();

    // Enums are stored by name, as in docs/10-data-model.md.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(20);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // User data is deleted together with the account (cascade).
        modelBuilder.Entity<Profile>(e =>
        {
            e.HasKey(p => p.UserId);
            e.HasOne<User>().WithOne().HasForeignKey<Profile>(p => p.UserId);
        });
        modelBuilder.Entity<Product>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(p => p.UserId);
            e.Property(p => p.Name).HasMaxLength(100);
        });
        modelBuilder.Entity<DiaryEntry>(e =>
        {
            e.HasOne<User>().WithMany().HasForeignKey(d => d.UserId);
            e.HasIndex(d => new { d.UserId, d.Date });
            e.Property(d => d.Name).HasMaxLength(200);
        });
    }
}
