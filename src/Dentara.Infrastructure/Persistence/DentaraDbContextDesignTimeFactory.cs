namespace Dentara.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

/// <summary>
/// Design-time factory for DentaraDbContext. Used by EF Core tooling (dotnet ef)
/// to create a DbContext instance without going through the full DI container.
/// This avoids dependency resolution errors during migrations when some
/// Application-layer services (gateway adapters, etc.) are not yet registered.
/// </summary>
public sealed class DentaraDbContextDesignTimeFactory : IDesignTimeDbContextFactory<DentaraDbContext>
{
    public DentaraDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DentaraDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=Dentara;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true")
            .Options;

        return new DentaraDbContext(options);
    }
}
