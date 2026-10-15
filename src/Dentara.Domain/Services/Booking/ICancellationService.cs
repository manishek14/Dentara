namespace Dentara.Domain.Services.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;

/// <summary>
/// Domain service for cancellation: atomically transitions the appointment to
/// Cancelled, releases all ResourceAllocations, and creates a Refund record
/// (if the appointment was Confirmed). All in one DB transaction per ADR-003.
///
/// The refund is created in Pending state. A separate worker (or the payment
/// module) processes the actual gateway refund call.
/// </summary>
public interface ICancellationService
{
    /// <summary>
    /// Cancels an appointment and releases capacity atomically.
    /// </summary>
    /// <param name="appointmentId">The appointment to cancel.</param>
    /// <param name="cancelledBy">Who cancelled: "Patient:GUID", "Staff:GUID", "System:Expiry".</param>
    /// <param name="reason">Optional reason text.</param>
    /// <param name="correlationId">For end-to-end audit.</param>
    /// <returns>The cancelled appointment with updated state and events.</returns>
    Task<Appointment> CancelAsync(
        Guid appointmentId,
        string cancelledBy,
        string? reason = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
