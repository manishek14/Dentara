namespace Dentara.Application.Booking.DTOs;

using Dentara.Application.Common.DTOs;

/// <summary>
/// Response DTO for a created/reserved appointment. Returned to the client
/// after the booking transaction commits. Contains everything the client needs
/// to redirect to payment.
/// </summary>
public sealed record AppointmentResponse(
    Guid Id,
    Guid PatientId,
    Guid DentistId,
    string DentistDisplayName,
    Guid ServiceId,
    string ServiceName,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    decimal PriceAmount,
    string Currency,
    CommercialTermsSnapshot Terms,
    DisplayDate? StartDisplay);
