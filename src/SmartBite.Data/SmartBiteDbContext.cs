using Microsoft.EntityFrameworkCore;

namespace SmartBite.Data;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public class SmartBiteDbContext(DbContextOptions<SmartBiteDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
}
