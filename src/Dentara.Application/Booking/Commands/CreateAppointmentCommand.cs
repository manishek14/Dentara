namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Services.Booking;
using MediatR;

/// <summary>
/// Reserves a new appointment atomically. The IBookingTransactionService
/// implementation (in Infrastructure) wraps the entire operation in a SQL
/// Server transaction with UPDLOCK+HOLDLOCK per ADR-003.
/// </summary>
public sealed record CreateAppointmentCommand(
    Guid PatientId,
    Guid DentistId,
    Guid ServiceId,
    Guid? PublishedSessionId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset EndAtUtc,
    // Snapshot fields (caller computes these via IServicePricingService)
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
    IReadOnlyList<ResourceRequirementItem> RequiredResources,
    string? CorrelationId = null,
    TimeSpan? PaymentWindowOverride = null)
    : ICommand<AppointmentCreatedResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "appointment_create";
    public string Key => $"{PatientId}|{DentistId}|{ServiceId}|{StartAtUtc:O}";
    public TimeSpan? Ttl => TimeSpan.FromHours(1); // allow retry within 1h
}

/// <summary>
/// Carries the requirement for a single resource type.
/// </summary>
public sealed record ResourceRequirementItem(
    int ResourceTypeValue,
    int QuantityRequired,
    bool RequireDentistOwned,
    Guid? SpecificResourceId);

public sealed record AppointmentCreatedResult(
    Guid AppointmentId,
    DateTimeOffset ExpiresAtUtc,
    decimal PriceAmount,
    string Currency);
