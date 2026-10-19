namespace Dentara.Infrastructure.Persistence;

using Dentara.Domain.Booking;
using Dentara.Domain.Catalog;
using Dentara.Domain.Identity;
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

    /// <summary>
    /// True when the provider is SQLite (test mode). T-SQL CHECK constraints
    /// (which use functions like LEN that SQLite does not support) are skipped
    /// in this mode; the invariants are still enforced in C# code.
    /// </summary>
    public bool IsSqliteProvider { get; private set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Detect SQLite provider — used to skip T-SQL-specific CHECK constraints.
        IsSqliteProvider = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        // Apply all IEntityTypeConfiguration<T> from this assembly.
        // For SQLite, we apply a subset that avoids T-SQL-specific functions.
        if (IsSqliteProvider)
        {
            ApplySqliteCompatibleModel(modelBuilder);
        }
        else
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DentaraDbContext).Assembly);
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
    /// SQLite-compatible model configuration. Skips T-SQL-specific CHECK
    /// constraints (LEN, etc.). Business invariants are still enforced in
    /// C# domain code, so this is acceptable for testing.
    /// </summary>
    private void ApplySqliteCompatibleModel(ModelBuilder modelBuilder)
    {
        // Apply all configurations from the assembly.
        var assembly = typeof(DentaraDbContext).Assembly;
        modelBuilder.ApplyConfigurationsFromAssembly(assembly);

        // Remove all CHECK constraints (SQLite doesn't support T-SQL functions).
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var checkConstraints = entity.GetCheckConstraints().ToList();
            foreach (var check in checkConstraints)
            {
                if (!string.IsNullOrEmpty(check.Name))
                    entity.RemoveCheckConstraint(check.Name);
            }

            // SQLite doesn't support SQL Server's rowversion. Make the column
            // nullable with a default value, and disable concurrency token.
            // Production SQL Server uses real rowversion columns with
            // IsRowVersion() — which we leave in place from the configurations.
            var rowVersionProp = entity.FindProperty("RowVersion");
            if (rowVersionProp != null)
            {
                rowVersionProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                rowVersionProp.IsNullable = true;
                rowVersionProp.SetDefaultValue(new byte[] { 0 });
                rowVersionProp.IsConcurrencyToken = false;
                rowVersionProp.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Save);
                rowVersionProp.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Save);
            }
        }
    }
}

