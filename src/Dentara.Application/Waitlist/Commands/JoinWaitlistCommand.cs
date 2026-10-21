namespace Dentara.Application.Waitlist.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Waitlist;
using MediatR;

/// <summary>
/// Adds a patient to the waitlist for a specific dentist + service + date range.
/// Idempotent per ADR-006.
/// </summary>
public sealed record JoinWaitlistCommand(
    Guid PatientId,
    Guid DentistId,
    Guid ServiceId,
    DateTimeOffset EarliestStartUtc,
    DateTimeOffset LatestStartUtc,
    string? Notes = null,
    string? CorrelationId = null)
    : ICommand<WaitlistJoinedResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "waitlist_join";
    public string Key => $"{PatientId}|{DentistId}|{ServiceId}|{EarliestStartUtc:O}";
    public TimeSpan? Ttl => TimeSpan.FromHours(1);
}

public sealed record WaitlistJoinedResult(Guid WaitlistEntryId);
