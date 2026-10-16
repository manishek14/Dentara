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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all IEntityTypeConfiguration<T> from this assembly.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DentaraDbContext).Assembly);

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
}
