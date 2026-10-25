namespace Dentara.Application.Waitlist.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using MediatR;
using System.Text.Json;

public sealed class JoinWaitlistCommandHandler
    : IRequestHandler<JoinWaitlistCommand, Result<WaitlistJoinedResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Domain.Waitlist.WaitlistEntry> _repository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public JoinWaitlistCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<Domain.Waitlist.WaitlistEntry> repository,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
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

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "WaitlistEntry",
                EntityId: entry.Id,
                Action: "Join",
                PerformedBy: _currentUser.UserId.HasValue
                    ? $"User:{_currentUser.UserId}"
                    : "Anonymous",
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Patient joined waitlist for dentist {request.DentistId}, service {request.ServiceId}",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    patientId = entry.PatientId,
                    dentistId = entry.DentistId,
                    serviceId = entry.ServiceId,
                    earliestStartUtc = entry.EarliestStartUtc,
                    latestStartUtc = entry.LatestStartUtc,
                    status = entry.Status.Name
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

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
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public CancelWaitlistCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<Domain.Waitlist.WaitlistEntry> repository,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _auditService = auditService;
        _currentUser = currentUser;
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

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "WaitlistEntry",
                EntityId: entry.Id,
                Action: "Cancel",
                PerformedBy: _currentUser.UserId.HasValue
                    ? $"User:{_currentUser.UserId}"
                    : "Anonymous",
                OccurredAtUtc: _clock.UtcNow,
                Description: "Patient cancelled waitlist entry",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    status = entry.Status.Name,
                    cancelledAtUtc = entry.CancelledAtUtc
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.FromException(ex);
        }
    }
}
