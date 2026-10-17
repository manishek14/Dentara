namespace Dentara.Api.Contracts.Booking;

/// <summary>
/// Request body for POST /api/appointments.
/// </summary>
public sealed record CreateAppointmentRequest(
    Guid DentistId,
    Guid ServiceId,
    Guid? PublishedSessionId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    // Snapshot fields (caller computes these via /api/services/{id}/pricing first)
    string ServiceNameSnapshot,
    int ServiceDurationMinutesSnapshot,
    decimal PriceAmountSnapshot,
    string CurrencySnapshot,
    string DentistDisplayNameSnapshot,
    int CancellationPolicyVersion,
    decimal CancellationFeePercent,
    decimal RefundablePercent,
    string? TermsSnapshotHash,
    // Resource requirements to allocate (caller resolves from Service)
    IReadOnlyList<CreateAppointmentRequest.ResourceRequirement> RequiredResources,
    TimeSpan? PaymentWindowOverride = null)
{
    public sealed record ResourceRequirement(
        int ResourceTypeValue,
        int QuantityRequired,
        bool RequireDentistOwned,
        Guid? SpecificResourceId);
}

/// <summary>
/// Response body for POST /api/appointments.
/// </summary>
public sealed record CreateAppointmentResponse(
    Guid AppointmentId,
    DateTimeOffset ExpiresAtUtc,
    decimal PriceAmount,
    string Currency);

/// <summary>
/// Request body for POST /api/appointments/{id}/cancel.
/// </summary>
public sealed record CancelAppointmentRequest(
    string? Reason);
