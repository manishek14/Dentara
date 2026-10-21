namespace Dentara.Infrastructure.Persistence;

using Dentara.Domain.Booking;
using Dentara.Domain.Catalog;
using Dentara.Domain.Identity;
using Dentara.Domain.Notifications;
using Dentara.Domain.Payments;
using Dentara.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core DbContext for the Dentara Modular Monolith. All 5 modules live in a
/// single DbContext (per ADR-002 — Modular Monolith, single database).
///
/// CRITICAL: This DbContext is used for everything EXCEPT the booking transaction.
/// The IBookingTransactionService uses a separate path with raw SQL + UPDLOCK+
/// HOLDLOCK (per ADR-003) because EF Core does not expose lock hints directly.
/// </summary>
public class DentaraDbContext : DbContext
{
    public DentaraDbContext(DbContextOptions<DentaraDbContext> options) : base(options)
    {
    }

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<DentistProfile> DentistProfiles => Set<DentistProfile>();

    // Catalog
    public DbSet<Service> Services => Set<Service>();
    public DbSet<DentistService> DentistServices => Set<DentistService>();
    public DbSet<ClinicResource> ClinicResources => Set<ClinicResource>();
    public DbSet<ResourceServiceRequirement> ResourceServiceRequirements => Set<ResourceServiceRequirement>();

    // Scheduling
    public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
    public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();
    public DbSet<BookingWindow> BookingWindows => Set<BookingWindow>();
    public DbSet<PublishedSession> PublishedSessions => Set<PublishedSession>();

    // Booking
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ResourceAllocation> ResourceAllocations => Set<ResourceAllocation>();
    public DbSet<AppointmentEvent> AppointmentEvents => Set<AppointmentEvent>();

    // Payments
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    /// <summary>
    /// True when the provider is SQLite (test mode). T-SQL CHECK constraints
    /// (which use functions like LEN that SQLite does not support) are skipped
    /// in this mode; the invariants are still enforced in C# code.
    /// </summary>
    public bool IsSqliteProvider { get; private set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Detect provider. ProviderName IS available at model-creation time
        // because EF Core sets it from the options before calling OnModelCreating.
        var providerName = Database.ProviderName ?? "";
        IsSqliteProvider = providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase);

        // Apply all IEntityTypeConfiguration<T> from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DentaraDbContext).Assembly);

        // EF Core has a convention: any byte[] property named "RowVersion" or
        // "Timestamp" is automatically treated as a concurrency token. We must
        // explicitly disable this for SQLite (which doesn't support rowversion
        // auto-generation). For SQL Server, we upgrade to real IsRowVersion().
        if (!IsSqliteProvider)
        {
            ApplySqlServerRowVersions(modelBuilder);
        }
        else
        {
            // SQLite: disable concurrency token on ALL RowVersion properties.
            // This is critical — EF Core's naming convention auto-enables it.
            DisableSqliteRowVersionConcurrency(modelBuilder);
            StripSqliteIncompatibleConstraints(modelBuilder);
        }

        // Disable cascade delete for all FK relationships per the architecture spec:
        // "حذف آبشاری برای سوابق رزرو و مالی ممنوع یا بسیار محدود"
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var fk in entity.GetForeignKeys())
            {
                fk.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Upgrades all RowVersion properties to real SQL Server rowversion columns
    /// with optimistic concurrency support.
    /// </summary>
    private static void ApplySqlServerRowVersions(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entity.FindProperty("RowVersion");
            if (rowVersionProp != null)
            {
                rowVersionProp.IsRowVersion();
            }
        }
    }

    /// <summary>
    /// Disables the concurrency token on all RowVersion properties for SQLite.
    /// EF Core has a built-in convention that auto-treats byte[] properties
    /// named "RowVersion" as concurrency tokens. This convention runs AFTER
    /// OnModelCreating, so setting IsConcurrencyToken=false here gets
    /// overridden by the convention.
    ///
    /// Workaround: remove the RowVersion property from the model entirely
    /// and re-add it as a plain byte[] with no concurrency behavior. The
    /// property name "RowVersion" on the C# entity class still exists (it's
    /// initialized to byte[0]), but EF Core won't map it for SQLite.
    /// </summary>
    private static void DisableSqliteRowVersionConcurrency(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entity.FindProperty("RowVersion");
            if (rowVersionProp != null)
            {
                // Remove the property that was auto-configured as a concurrency token
                entity.RemoveProperty(rowVersionProp);

                // Re-add it as a plain byte[] property — no concurrency token,
                // no auto-generation, just a nullable column with a default value.
                var newProp = entity.AddProperty("RowVersion", typeof(byte[]));
                newProp.IsConcurrencyToken = false;
                newProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                newProp.IsNullable = true;
                newProp.SetDefaultValue(new byte[] { 0 });
                newProp.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
                newProp.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
            }
        }
    }

    /// <summary>
    /// Removes all CHECK constraints from the model. SQLite doesn't support
    /// T-SQL functions like LEN, so the constraints generated for SQL Server
    /// would fail. Business invariants are still enforced in C# domain code.
    /// </summary>
    private static void StripSqliteIncompatibleConstraints(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var checkConstraints = entity.GetCheckConstraints().ToList();
            foreach (var check in checkConstraints)
            {
                if (!string.IsNullOrEmpty(check.Name))
                    entity.RemoveCheckConstraint(check.Name);
            }
        }
    }
}

/// <summary>
/// Extension for marking a property as rowversion in the EF Core model.
/// </summary>
internal static class RowVersionExtensions
{
    /// <summary>
    /// Marks the property as a rowversion column for SQL Server. This is
    /// called by DentaraDbContext for SQL Server only.
    /// </summary>
    public static void IsRowVersion(this Microsoft.EntityFrameworkCore.Metadata.IMutableProperty property)
    {
        property.IsConcurrencyToken = true;
        property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
        property.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
        property.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
    }
}
