namespace Dentara.Infrastructure.Waitlist;

using Dentara.Application.Common.Results;
using Dentara.Application.Waitlist.Queries;
using Dentara.Domain.Waitlist;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class GetWaitlistQueriesHandler
    : IRequestHandler<GetWaitlistEntriesQuery, Result<PagedResult<WaitlistEntryDto>>>,
      IRequestHandler<GetWaitlistEntryByIdQuery, Result<WaitlistEntryDto>>
{
    private readonly DentaraDbContext _dbContext;

    public GetWaitlistQueriesHandler(DentaraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<WaitlistEntryDto>>> Handle(
        GetWaitlistEntriesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _dbContext.WaitlistEntries.AsQueryable();

            if (request.PatientId.HasValue)
                query = query.Where(w => w.PatientId == request.PatientId.Value);

            if (request.DentistId.HasValue)
                query = query.Where(w => w.DentistId == request.DentistId.Value);

            // Status filter — convert string to int value
            if (!string.IsNullOrEmpty(request.Status))
            {
                try
                {
                    var status = WaitlistStatus.FromName(request.Status);
                    query = query.Where(w => w.StatusValue == status.Value);
                }
                catch (ArgumentException)
                {
                    // Invalid status name — return empty
                    return Result<PagedResult<WaitlistEntryDto>>.Success(
                        PagedResult<WaitlistEntryDto>.Empty(request.PageNumber, request.PageSize));
                }
            }

            // Load all matches (SQLite can't order by DateTimeOffset in LINQ)
            var allMatches = await query.ToListAsync(cancellationToken);

            // Order by priority (ascending = FIFO) on client
            var ordered = allMatches.OrderBy(w => w.Priority).ToList();

            var totalCount = ordered.Count;
            var pageSize = Math.Max(1, Math.Min(request.PageSize, 200));
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageItems = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapToDto)
                .ToList();

            return Result<PagedResult<WaitlistEntryDto>>.Success(
                new PagedResult<WaitlistEntryDto>(pageItems, totalCount, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            return Result<PagedResult<WaitlistEntryDto>>.FromException(ex);
        }
    }

    public async Task<Result<WaitlistEntryDto>> Handle(
        GetWaitlistEntryByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _dbContext.WaitlistEntries
                .FirstOrDefaultAsync(w => w.Id == request.EntryId, cancellationToken);

            if (entry == null)
                return Result<WaitlistEntryDto>.Failure(
                    "NOTFOUND_WAITLIST",
                    $"Waitlist entry {request.EntryId} not found.");

            return Result<WaitlistEntryDto>.Success(MapToDto(entry));
        }
        catch (Exception ex)
        {
            return Result<WaitlistEntryDto>.FromException(ex);
        }
    }

    private static WaitlistEntryDto MapToDto(WaitlistEntry w) => new(
        w.Id,
        w.PatientId,
        w.DentistId,
        w.ServiceId,
        w.EarliestStartUtc,
        w.LatestStartUtc,
        w.Status.Name,
        w.Priority,
        w.OfferedAtUtc,
        w.OfferExpiresAtUtc,
        w.OfferedStartUtc,
        w.FulfilledAtUtc,
        w.DeclinedAtUtc,
        w.CancelledAtUtc,
        w.DeclineReason,
        w.Notes,
        w.CreatedAtUtc);
}
