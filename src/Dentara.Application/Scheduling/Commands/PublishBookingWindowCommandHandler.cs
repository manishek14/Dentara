namespace Dentara.Application.Scheduling.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Services.Scheduling;
using MediatR;
using System.Text.Json;

public sealed class PublishBookingWindowCommandHandler
    : IRequestHandler<PublishBookingWindowCommand, Result<PublishBookingWindowResult>>
{
    private readonly IPublicationService _publicationService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public PublishBookingWindowCommandHandler(
        IPublicationService publicationService,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _publicationService = publicationService;
        _auditService = auditService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PublishBookingWindowResult>> Handle(PublishBookingWindowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _publicationService.PublishAsync(request.BookingWindowId, cancellationToken);

            // Audit log — only for actual publications (not idempotent re-runs)
            if (count > 0)
            {
                await _auditService.LogAsync(new AuditEntry(
                    EntityType: "BookingWindow",
                    EntityId: request.BookingWindowId,
                    Action: "Publish",
                    PerformedBy: _currentUser.UserId.HasValue
                        ? $"User:{_currentUser.UserId}"
                        : "System:PublicationWorker",
                    OccurredAtUtc: _clock.UtcNow,
                    Description: $"Published {count} sessions for booking window {request.BookingWindowId}",
                    PerformedByUserId: _currentUser.UserId,
                    NewValueJson: JsonSerializer.Serialize(new
                    {
                        bookingWindowId = request.BookingWindowId,
                        sessionsPublished = count
                    }),
                    CorrelationId: request.CorrelationId), cancellationToken);
            }

            // If count is 0, the publication was likely a no-op (idempotent re-run).
            return Result<PublishBookingWindowResult>.Success(
                new PublishBookingWindowResult(count, WasReplayed: count == 0));
        }
        catch (Exception ex)
        {
            return Result<PublishBookingWindowResult>.FromException(ex);
        }
    }
}
