namespace Dentara.Application.Scheduling.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Services.Scheduling;
using MediatR;

/// <summary>
/// Triggers publication of sessions for a BookingWindow. Normally called by
/// the BackgroundWorker, but exposed as a command so admins can manually
/// re-trigger if needed. Idempotent per ADR-006.
/// </summary>
public sealed record PublishBookingWindowCommand(
    Guid BookingWindowId,
    string? CorrelationId = null)
    : ICommand<PublishBookingWindowResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "booking_window_publish";
    public string Key => BookingWindowId.ToString();
    public TimeSpan? Ttl => TimeSpan.FromMinutes(5); // re-publication safety window
}

public sealed record PublishBookingWindowResult(
    int SessionsPublished,
    bool WasReplayed);
