namespace Dentara.Application.Waitlist.Queries;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// Returns waitlist entries, optionally filtered by patient, dentist, or status.
/// Patients can only see their own entries; staff can see all.
/// </summary>
public sealed record GetWaitlistEntriesQuery(
    Guid? PatientId = null,
    Guid? DentistId = null,
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 50,
    string? CorrelationIdHeader = null)
    : IQuery<PagedResult<WaitlistEntryDto>>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

/// <summary>
/// A single waitlist entry in the response.
/// </summary>
public sealed record WaitlistEntryDto(
    Guid Id,
    Guid PatientId,
    Guid DentistId,
    Guid ServiceId,
    DateTimeOffset EarliestStartUtc,
    DateTimeOffset LatestStartUtc,
    string Status,
    long Priority,
    DateTimeOffset? OfferedAtUtc,
    DateTimeOffset? OfferExpiresAtUtc,
    DateTimeOffset? OfferedStartUtc,
    DateTimeOffset? FulfilledAtUtc,
    DateTimeOffset? DeclinedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    string? DeclineReason,
    string? Notes,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Returns a single waitlist entry by ID.
/// </summary>
public sealed record GetWaitlistEntryByIdQuery(
    Guid EntryId,
    string? CorrelationIdHeader = null)
    : IQuery<WaitlistEntryDto>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}
