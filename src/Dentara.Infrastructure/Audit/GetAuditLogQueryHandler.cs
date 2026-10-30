namespace Dentara.Infrastructure.Audit;

using Dentara.Application.Audit.Queries;
using Dentara.Application.Common.Results;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class GetAuditLogQueryHandler
    : IRequestHandler<GetAuditLogQuery, Result<PagedResult<AuditLogEntry>>>
{
    private readonly DentaraDbContext _dbContext;

    public GetAuditLogQueryHandler(DentaraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<AuditLogEntry>>> Handle(
        GetAuditLogQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Build the query with filters. DateTimeOffset comparisons can't
            // be translated by SQLite LINQ, so we load the filtered set and
            // paginate on the client when the date filters are set.
            var query = _dbContext.AuditLogs.AsQueryable();

            if (!string.IsNullOrEmpty(request.EntityType))
                query = query.Where(a => a.EntityType == request.EntityType);

            if (request.EntityId.HasValue)
                query = query.Where(a => a.EntityId == request.EntityId.Value);

            if (!string.IsNullOrEmpty(request.Action))
                query = query.Where(a => a.Action == request.Action);

            if (request.PerformedByUserId.HasValue)
                query = query.Where(a => a.PerformedByUserId == request.PerformedByUserId.Value);

            if (!string.IsNullOrEmpty(request.CorrelationId))
                query = query.Where(a => a.CorrelationId == request.CorrelationId);

            // Load all matching entries (SQLite can't translate DateTimeOffset
            // comparisons in Where). For production SQL Server, the date filters
            // would be pushed to the database.
            var allMatches = await query.ToListAsync(cancellationToken);

            // Apply date filters on the client
            var filtered = allMatches.AsEnumerable();
            if (request.FromUtc.HasValue)
                filtered = filtered.Where(a => a.OccurredAtUtc >= request.FromUtc.Value);
            if (request.ToUtc.HasValue)
                filtered = filtered.Where(a => a.OccurredAtUtc <= request.ToUtc.Value);

            // Order by most recent first
            var ordered = filtered.OrderByDescending(a => a.OccurredAtUtc).ToList();

            // Paginate
            var totalCount = ordered.Count;
            var pageSize = Math.Max(1, Math.Min(request.PageSize, 200)); // cap at 200
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageItems = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AuditLogEntry(
                    a.Id,
                    a.EntityType,
                    a.EntityId,
                    a.Action,
                    a.PerformedBy,
                    a.PerformedByUserId,
                    a.OldValueJson,
                    a.NewValueJson,
                    a.Description,
                    a.IpAddress,
                    a.CorrelationId,
                    a.OccurredAtUtc))
                .ToList();

            return Result<PagedResult<AuditLogEntry>>.Success(
                new PagedResult<AuditLogEntry>(pageItems, totalCount, pageNumber, pageSize));
        }
        catch (Exception ex)
        {
            return Result<PagedResult<AuditLogEntry>>.FromException(ex);
        }
    }
}
