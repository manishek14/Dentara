namespace Dentara.Api.Contracts.Waitlist;

/// <summary>
/// Request body for POST /api/waitlist — join the waitlist.
/// </summary>
public sealed record JoinWaitlistRequest(
    Guid DentistId,
    Guid ServiceId,
    DateTimeOffset EarliestStartUtc,
    DateTimeOffset LatestStartUtc,
    string? Notes = null);

/// <summary>
/// Response body for POST /api/waitlist.
/// </summary>
public sealed record JoinWaitlistResponse(
    Guid WaitlistEntryId,
    string Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Request body for POST /api/waitlist/{id}/cancel.
/// </summary>
public sealed record CancelWaitlistRequest(
    Guid? PatientId = null); // optional: staff can cancel on behalf

/// <summary>
/// Response for GET /api/waitlist/{id} and GET /api/waitlist (list items).
/// </summary>
public sealed record WaitlistEntryResponse(
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
/// Request body for POST /api/waitlist/{id}/accept — patient accepts an offer.
/// This triggers the booking flow for the offered slot.
/// </summary>
public sealed record AcceptWaitlistOfferRequest(
    Guid PatientId);
