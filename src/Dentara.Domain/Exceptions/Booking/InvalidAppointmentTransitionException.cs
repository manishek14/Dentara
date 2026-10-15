namespace Dentara.Domain.Exceptions.Booking;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when an illegal state transition is attempted on an Appointment.
/// Example: trying to Confirm an appointment that is already Expired.
/// </summary>
public sealed class InvalidAppointmentTransitionException : DomainException
{
    public string FromStatus { get; }
    public string ToStatus { get; }

    public InvalidAppointmentTransitionException(Guid appointmentId, string fromStatus, string toStatus)
        : base(
            "BOOKING_INVALID_TRANSITION",
            $"Cannot transition appointment {appointmentId} from {fromStatus} to {toStatus}.",
            appointmentId)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
