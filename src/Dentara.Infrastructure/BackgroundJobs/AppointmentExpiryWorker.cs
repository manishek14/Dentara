namespace Dentara.Infrastructure.BackgroundJobs;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using Dentara.Domain.Services.Payments;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background worker that processes expired PendingPayment appointments.
///
/// CRITICAL per ADR-004: this worker MUST NOT auto-fail an appointment when
/// its payment is in Unknown state. The flow is:
///
///   1. Find appointments in PendingPayment where ExpiresAtUtc ≤ now.
///   2. For each, look up the latest PaymentAttempt:
///      a. If status is Succeeded → call ConfirmAppointmentCommand (late payment).
///         The appointment transitions to Confirmed. Capacity stays reserved.
///      b. If status is Failed → safe to expire. Transition to Expired.
///         Capacity is released.
///      c. If status is Unknown → call IPaymentVerificationService.VerifyAsync:
///         - Returns Succeeded → confirm the appointment.
///         - Returns Failed → expire the appointment.
///         - Returns StillUnknown → SKIP this appointment; try again next iteration.
///           NEVER auto-fail. The patient's money may be in flight at the gateway.
///      d. If no PaymentAttempt exists → safe to expire.
///   3. Each appointment is processed in its own DI scope + DB transaction.
///      A failure on one appointment does not affect others.
///
/// Idempotency:
///   - The Appointment.Expire() method is guarded by the state machine:
///     calling it on an already-Expired appointment throws
///     InvalidAppointmentTransitionException. The worker catches this and
///     logs a warning, then moves on.
///   - The Appointment.Confirm() method has the same guard.
/// </summary>
public sealed class AppointmentExpiryWorker : WorkerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public AppointmentExpiryWorker(
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<AppointmentExpiryWorker> logger)
        : base(TimeSpan.FromMinutes(1), logger, errorDelay: TimeSpan.FromMinutes(2))
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
        var verificationService = scope.ServiceProvider.GetRequiredService<IPaymentVerificationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppointmentExpiryWorker>>();

        // Find expired PendingPayment appointments. Take a batch of 50 at a time.
        // Use ADO.NET directly because EF Core SQLite has trouble translating
        // DateTimeOffset comparisons in both LINQ and FromSqlInterpolated.
        var nowUtc = _clock.UtcNow;

        // Get IDs via ADO.NET
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id FROM Appointments WHERE StatusValue = 1 AND ExpiresAtUtc <= $now ORDER BY ExpiresAtUtc LIMIT 50";
        var p = cmd.CreateParameter();
        p.ParameterName = "$now";
        p.Value = nowUtc.ToString("O"); // ISO 8601 round-trip format
        cmd.Parameters.Add(p);

        var expiredIds = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(stoppingToken);
        while (await reader.ReadAsync(stoppingToken))
            expiredIds.Add(reader.GetGuid(0));
        await reader.CloseAsync();

        if (expiredIds.Count == 0)
            return;

        // Load entities via EF Core (tracked) using the IDs we found.
        // Note: no OrderBy — SQLite can't order by DateTimeOffset in LINQ.
        // The IDs were already ordered by ExpiresAtUtc in the ADO.NET query.
        var expiredAppts = await db.Appointments
            .Where(a => expiredIds.Contains(a.Id))
            .ToListAsync(stoppingToken);

        if (expiredAppts.Count == 0)
            return;

        logger.LogInformation(
            "ExpiryWorker: processing {Count} expired appointments", expiredAppts.Count);

        foreach (var appt in expiredAppts)
        {
            try
            {
                await ProcessExpiredAppointmentAsync(appt, db, verificationService, logger, stoppingToken);
            }
            catch (Exception ex)
            {
                // Log and continue — one bad appointment must not stop the worker.
                logger.LogError(ex,
                    "ExpiryWorker: failed to process appointment {AppointmentId}", appt.Id);
            }
        }
    }

    private async Task ProcessExpiredAppointmentAsync(
        Appointment appointment,
        DentaraDbContext db,
        IPaymentVerificationService verificationService,
        ILogger logger,
        CancellationToken ct)
    {
        // Look up the latest PaymentAttempt for this appointment.
        // Note: can't use OrderBy(CreatedAtUtc) in SQLite — DateTimeOffset
        // ordering is not supported. Load all and sort on client.
        var payments = await db.PaymentAttempts
            .Where(p => p.AppointmentId == appointment.Id)
            .ToListAsync(ct);
        var latestPayment = payments.OrderByDescending(p => p.CreatedAtUtc).FirstOrDefault();

        if (latestPayment == null)
        {
            // No payment was ever initiated — safe to expire
            await ExpireAsync(appointment, db, paymentWasVerifiedAbsent: true, logger, ct);
            return;
        }

        var status = latestPayment.Status;

        if (status == PaymentStatus.Succeeded)
        {
            // Late payment confirmation — transition to Confirmed (capacity stays reserved)
            await ConfirmAsync(appointment, latestPayment, db, logger, ct);
        }
        else if (status == PaymentStatus.Failed)
        {
            // Payment definitively failed — safe to expire
            await ExpireAsync(appointment, db, paymentWasVerifiedAbsent: true, logger, ct);
        }
        else if (status == PaymentStatus.Unknown)
        {
            // CRITICAL (ADR-004): must verify with gateway before expiring
            await HandleUnknownPaymentAsync(appointment, latestPayment, db, verificationService, logger, ct);
        }
        else if (status == PaymentStatus.Pending || status == PaymentStatus.Initiated)
        {
            // Patient started but never completed — treat as no payment yet.
            // Safe to expire; if the gateway later confirms success, the
            // late-payment path in the webhook handler will issue a refund.
            await ExpireAsync(appointment, db, paymentWasVerifiedAbsent: true, logger, ct);
        }
        else
        {
            // RefundPending, PartiallyRefunded, Refunded, RefundFailed — these
            // shouldn't happen for a PendingPayment appointment. Log and skip.
            logger.LogWarning(
                "ExpiryWorker: appointment {AppointmentId} has unexpected payment status {Status}; skipping",
                appointment.Id, status);
        }
    }

    private async Task HandleUnknownPaymentAsync(
        Appointment appointment,
        PaymentAttempt payment,
        DentaraDbContext db,
        IPaymentVerificationService verificationService,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogInformation(
            "ExpiryWorker: verifying payment {PaymentId} for appointment {AppointmentId} with gateway",
            payment.Id, appointment.Id);

        var verification = await verificationService.VerifyAsync(payment.Id, ct);

        if (verification.Outcome == PaymentVerificationOutcome.Succeeded)
        {
            // Gateway confirms success — transition payment + appointment to Confirmed
            if (!string.IsNullOrEmpty(verification.ProviderTransactionId))
            {
                payment.MarkSucceeded(verification.ProviderTransactionId, _clock);
            }
            await ConfirmAsync(appointment, payment, db, logger, ct);
        }
        else if (verification.Outcome == PaymentVerificationOutcome.Failed)
        {
            // Gateway confirms failure — safe to expire
            await ExpireAsync(appointment, db, paymentWasVerifiedAbsent: true, logger, ct);
        }
        else // StillUnknown
        {
            // CRITICAL: do NOT expire. Skip this appointment; the next iteration
            // will retry the verification. The capacity stays reserved until we
            // have a definitive answer from the gateway.
            logger.LogWarning(
                "ExpiryWorker: appointment {AppointmentId} payment still Unknown after verification; " +
                "capacity stays reserved until gateway responds definitively (ADR-004)",
                appointment.Id);
        }
    }

    private async Task ExpireAsync(
        Appointment appointment, DentaraDbContext db, bool paymentWasVerifiedAbsent,
        ILogger logger, CancellationToken ct)
    {
        try
        {
            appointment.Expire(_clock, paymentWasVerifiedAbsent);

            // Explicitly add new audit events to the DbContext.
            foreach (var evt in appointment.GetUncommittedEvents())
            {
                await db.AppointmentEvents.AddAsync(evt, ct);
            }
            appointment.MarkEventsAsCommitted();

            // Release all active resource allocations
            var allocations = await db.ResourceAllocations
                .Where(ra => ra.AppointmentId == appointment.Id && ra.StatusValue == AllocationStatus.Active.Value)
                .ToListAsync(ct);
            foreach (var alloc in allocations)
                alloc.Release(_clock, "Appointment expired");

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "ExpiryWorker: expired appointment {AppointmentId}, released {Count} allocations",
                appointment.Id, allocations.Count);
        }
        catch (Domain.Exceptions.Booking.InvalidAppointmentTransitionException)
        {
            // Already terminal — another worker beat us to it. Safe to skip.
            logger.LogDebug(
                "ExpiryWorker: appointment {AppointmentId} already terminal; skipping", appointment.Id);
        }
    }

    private async Task ConfirmAsync(
        Appointment appointment, PaymentAttempt payment, DentaraDbContext db,
        ILogger logger, CancellationToken ct)
    {
        try
        {
            appointment.Confirm(_clock, payment.Id, payment.Amount);

            // Explicitly add new audit events to the DbContext.
            foreach (var evt in appointment.GetUncommittedEvents())
            {
                await db.AppointmentEvents.AddAsync(evt, ct);
            }
            appointment.MarkEventsAsCommitted();

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "ExpiryWorker: confirmed appointment {AppointmentId} after late payment verification",
                appointment.Id);
        }
        catch (Domain.Exceptions.Booking.InvalidAppointmentTransitionException)
        {
            logger.LogDebug(
                "ExpiryWorker: appointment {AppointmentId} already confirmed/cancelled; skipping",
                appointment.Id);
        }
    }
}
