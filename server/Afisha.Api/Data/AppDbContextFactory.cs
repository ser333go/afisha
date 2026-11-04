using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Afisha.Api.Data;

/// <summary>Нужен только для команд `dotnet ef migrations ...`, чтобы не поднимать всё приложение.</summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=afisha.db")
            .Options;

        return new AppDbContext(options);
    }
}
