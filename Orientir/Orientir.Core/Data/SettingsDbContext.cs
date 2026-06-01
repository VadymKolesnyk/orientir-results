using Microsoft.EntityFrameworkCore;
using Orientir.Core.Models;

namespace Orientir.Core.Data;

public class SettingsDbContext : DbContext
{
    public DbSet<AppSettings> Settings => Set<AppSettings>();
    public DbSet<EventConfig> Events => Set<EventConfig>();
    public DbSet<DayConfig> Days => Set<DayConfig>();

    private readonly string _dbPath;

    public SettingsDbContext(string dbPath)
    {
        _dbPath = dbPath;
    }

    // Для dotnet ef (design-time) — шлях підставляє фабрика.
    public SettingsDbContext(DbContextOptions<SettingsDbContext> options) : base(options)
    {
        _dbPath = "";
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseSqlite($"Data Source={_dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventConfig>()
            .HasMany(e => e.Days)
            .WithOne(d => d.Event!)
            .HasForeignKey(d => d.EventConfigId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
