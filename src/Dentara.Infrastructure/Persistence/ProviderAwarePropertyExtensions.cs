namespace Dentara.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

/// <summary>
/// Extension methods for provider-aware entity configuration.
///
/// SQLite does not support SQL Server's rowversion auto-generation. Calling
/// IsRowVersion() on SQLite causes DbUpdateConcurrencyException on UPDATE
/// because the column value never changes and EF Core's optimistic concurrency
/// check fails.
///
/// Usage in entity configurations:
///   builder.Property(x => x.RowVersion).ConfigureRowVersion();
///
/// This configures a plain byte[] with default [0] that works on ALL providers.
/// DentaraDbContext.OnModelCreating then detects the SQL Server provider at
/// runtime and applies IsRowVersion() to all RowVersion properties — but only
/// for SQL Server. SQLite keeps the plain byte[] without concurrency token.
/// </summary>
public static class ProviderAwarePropertyExtensions
{
    /// <summary>
    /// Configures the RowVersion property as a plain nullable byte[] with
    /// default value [0]. Works on all providers. DentaraDbContext adds
    /// IsRowVersion() for SQL Server at runtime.
    /// </summary>
    public static PropertyBuilder<byte[]> ConfigureRowVersion(
        this PropertyBuilder<byte[]> builder)
    {
        builder.IsRequired(false)
               .HasDefaultValue(new byte[] { 0 });
        return builder;
    }
}
