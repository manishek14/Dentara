namespace Dentara.Application.Scheduling.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Results;
using Dentara.Domain.Services.Scheduling;
using MediatR;

public sealed class PublishBookingWindowCommandHandler
    : IRequestHandler<PublishBookingWindowCommand, Result<PublishBookingWindowResult>>
{
    private readonly IPublicationService _publicationService;

    public PublishBookingWindowCommandHandler(IPublicationService publicationService)
    {
        _publicationService = publicationService;
    }

    public async Task<Result<PublishBookingWindowResult>> Handle(PublishBookingWindowCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _publicationService.PublishAsync(request.BookingWindowId, cancellationToken);

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
