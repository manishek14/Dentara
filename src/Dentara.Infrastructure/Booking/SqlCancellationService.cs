namespace Dentara.Infrastructure.Booking;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions.Booking;
using Dentara.Domain.Payments;
using Dentara.Domain.Services.Booking;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQL Server implementation of ICancellationService. Atomically cancels an
/// appointment, releases all ResourceAllocations, and creates a Refund row
/// if the appointment was Confirmed. All in one DB transaction per ADR-003.
/// </summary>
public sealed class SqlCancellationService : ICancellationService
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<SqlCancellationService> _logger;

    public SqlCancellationService(
        DentaraDbContext dbContext,
        IClock clock,
        ILogger<SqlCancellationService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Appointment> CancelAsync(
        Guid appointmentId,
        string cancelledBy,
        string? reason = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, cancellationToken);

            try
            {
                var appointment = await _dbContext.Appointments
                    .Include(a => a.ResourceAllocations)
                    .Include(a => a.Events)
                    .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

                if (appointment == null)
                    throw new InvalidAppointmentTransitionException(
                        appointmentId, "NotFound", nameof(AppointmentStatus.Cancelled));

                var wasConfirmed = appointment.Status == AppointmentStatus.Confirmed;
                var refundAmount = wasConfirmed ? appointment.CalculateRefundAmount() : 0m;

                // Transition the appointment to Cancelled (raises domain events)
                appointment.Cancel(_clock, cancelledBy, reason, correlationId);

                // Release all active ResourceAllocations
                foreach (var allocation in appointment.ResourceAllocations)
                {
                    if (allocation.Status == AllocationStatus.Active)
                        allocation.Release(_clock, "Appointment cancelled");
                }

                // If was Confirmed, create a Refund row (in Pending state)
                if (wasConfirmed && refundAmount > 0)
                {
                    // Find the succeeded payment attempt for this appointment
                    var paymentAttempt = await _dbContext.PaymentAttempts
                        .FirstOrDefaultAsync(p =>
                            p.AppointmentId == appointmentId &&
                            p.Status == PaymentStatus.Succeeded,
                            cancellationToken);

                    if (paymentAttempt != null)
                    {
                        var refund = Refund.Create(
                            paymentAttempt.Id,
                            appointmentId,
                            refundAmount,
                            appointment.CurrencySnapshot,
                            reason ?? "Patient cancelled",
                            _clock,
                            correlationId);

                        paymentAttempt.AddRefund(refund);
                        _dbContext.Refunds.Add(refund);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Confirmed appointment {AppointmentId} has no succeeded payment; " +
                            "refund row not created. Manual reconciliation required.",
                            appointmentId);
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Cancelled appointment {AppointmentId} by {CancelledBy}; refundAmount={RefundAmount}",
                    appointmentId, cancelledBy, refundAmount);

                return appointment;
            }
            catch (Exception ex) when (ex is not InvalidAppointmentTransitionException)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Cancellation transaction failed for {AppointmentId}", appointmentId);
                throw;
            }
        });
    }
}
