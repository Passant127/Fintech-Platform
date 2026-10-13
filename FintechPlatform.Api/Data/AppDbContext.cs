using FintechPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FintechPlatform.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(t => t.Amount).HasPrecision(18, 2);
            entity.Property(t => t.Currency).HasMaxLength(3).IsRequired();
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<IdempotencyKey>(entity =>
        {
            entity.Property(k => k.Key).HasMaxLength(100).IsRequired();
            entity.HasIndex(k => k.Key).IsUnique();
            entity.Property(k => k.ResponseBody).IsRequired();
        });
    }
}