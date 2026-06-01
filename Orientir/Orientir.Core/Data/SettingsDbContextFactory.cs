using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orientir.Core.Data;

// Використовується лише інструментом dotnet ef для генерації міграцій.
public class SettingsDbContextFactory : IDesignTimeDbContextFactory<SettingsDbContext>
{
    public SettingsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SettingsDbContext>()
            .UseSqlite("Data Source=orientir-settings.db")
            .Options;
        return new SettingsDbContext(options);
    }
}
