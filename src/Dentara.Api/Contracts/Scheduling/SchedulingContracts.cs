namespace Dentara.Api.Contracts.Scheduling;

public sealed record PublishBookingWindowResponse(
    int SessionsPublished,
    bool WasReplayed);
