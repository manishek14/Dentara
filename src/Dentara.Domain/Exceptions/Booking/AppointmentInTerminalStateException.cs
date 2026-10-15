namespace Dentara.Domain.Exceptions.Booking;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when an attempt is made to modify or cancel an appointment that is
/// in a terminal state (Expired, Cancelled, Completed, NoShow).
/// </summary>
public sealed class AppointmentInTerminalStateException : DomainException
{
    public string CurrentStatus { get; }

    public AppointmentInTerminalStateException(Guid appointmentId, string currentStatus)
        : base(
            "BOOKING_APPOINTMENT_TERMINAL",
            $"Appointment {appointmentId} is in terminal state '{currentStatus}' and cannot be modified.")
    {
        CurrentStatus = currentStatus;
    }
}
