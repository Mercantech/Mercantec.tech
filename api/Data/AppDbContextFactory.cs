using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mercantec.Tech.Api.Data;

/// <summary>Design-time factory til `dotnet ef migrations` uden kørende Postgres.</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("CONNECTION_STRING")
                 ?? "Host=127.0.0.1;Port=5432;Database=mercantec;Username=mercantec;Password=mercantec";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs)
            .Options;
        return new AppDbContext(options);
    }
}
