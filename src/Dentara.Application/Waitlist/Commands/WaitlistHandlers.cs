namespace Dentara.Application.Waitlist.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using MediatR;

public sealed class JoinWaitlistCommandHandler
    : IRequestHandler<JoinWaitlistCommand, Result<WaitlistJoinedResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Domain.Waitlist.WaitlistEntry> _repository;
    private readonly IClock _clock;

    public JoinWaitlistCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<Domain.Waitlist.WaitlistEntry> repository,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result<WaitlistJoinedResult>> Handle(JoinWaitlistCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entry = Domain.Waitlist.WaitlistEntry.Create(
                request.PatientId,
                request.DentistId,
                request.ServiceId,
                request.EarliestStartUtc,
                request.LatestStartUtc,
                _clock,
                request.Notes);

            await _repository.AddAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<WaitlistJoinedResult>.Success(new WaitlistJoinedResult(entry.Id));
        }
        catch (Exception ex)
        {
            return Result<WaitlistJoinedResult>.FromException(ex);
        }
    }
}

/// <summary>
/// Cancels a waitlist entry. Only legal from Waiting status.
/// </summary>
public sealed record CancelWaitlistCommand(
    Guid WaitlistEntryId,
    Guid PatientId,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed class CancelWaitlistCommandHandler
    : IRequestHandler<CancelWaitlistCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Domain.Waitlist.WaitlistEntry> _repository;
    private readonly IClock _clock;

    public CancelWaitlistCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<Domain.Waitlist.WaitlistEntry> repository,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _clock = clock;
    }

    public async Task<Result> Handle(CancelWaitlistCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _repository.GetByIdAsync(request.WaitlistEntryId, cancellationToken);
            if (entry == null)
                return Result.Failure("NOTFOUND_WAITLIST", $"Waitlist entry {request.WaitlistEntryId} not found.");

            if (entry.PatientId != request.PatientId)
                return Result.Failure("FORBIDDEN", "Only the patient who created the entry can cancel it.");

            entry.Cancel(_clock);
            _repository.Update(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.FromException(ex);
        }
    }
}
