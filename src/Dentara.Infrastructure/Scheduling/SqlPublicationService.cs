namespace Dentara.Infrastructure.Scheduling;

using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using Dentara.Domain.Scheduling;
using Dentara.Domain.Services.Scheduling;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQL implementation of IPublicationService. Generates PublishedSessions for
/// a BookingWindow based on each dentist's WorkSchedules (minus ScheduleExceptions).
///
/// Idempotent per ADR-006: re-running for the same window produces 0 new
/// sessions because PublishedSessions.IdempotencyKey is UNIQUE. The first
/// call inserts; subsequent calls hit the UNIQUE constraint and the existing
/// sessions are kept.
/// </summary>
public sealed class SqlPublicationService : IPublicationService
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<SqlPublicationService> _logger;

    public SqlPublicationService(
        DentaraDbContext dbContext,
        IClock clock,
        ILogger<SqlPublicationService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> PublishAsync(Guid bookingWindowId, CancellationToken cancellationToken = default)
    {
        var window = await _dbContext.BookingWindows
            .FirstOrDefaultAsync(w => w.Id == bookingWindowId, cancellationToken);

        if (window == null)
            throw new InvalidOperationException($"BookingWindow {bookingWindowId} not found.");

        if (window.Status == BookingWindowStatus.Cancelled)
        {
            _logger.LogInformation("PublicationService: window {WindowId} is Cancelled; skipping", bookingWindowId);
            return 0;
        }

        // Transition to Open (idempotent — MarkOpened is a no-op if already Open)
        window.MarkOpened(_clock);

        // Find all active dentists (filter by DentistIdFilter if set)
        var dentistQuery = _dbContext.DentistProfiles.Where(d => d.IsActive);
        if (window.DentistIdFilter.HasValue)
        {
            dentistQuery = dentistQuery.Where(d => d.Id == window.DentistIdFilter.Value);
        }
        var dentists = await dentistQuery.ToListAsync(cancellationToken);

        var newSessionsCount = 0;

        foreach (var dentist in dentists)
        {
            // Load the dentist's work schedules (active, in the applicable range)
            var schedules = await _dbContext.WorkSchedules
                .Where(ws => ws.DentistId == dentist.Id && ws.IsActive)
                .ToListAsync(cancellationToken);

            // Load the dentist's schedule exceptions in the applicable range
            var exceptions = await _dbContext.ScheduleExceptions
                .Where(se => (se.DentistId == dentist.Id || se.DentistId == Guid.Empty)
                    && se.EffectiveFromUtc < window.ApplicableDateUntilUtc
                    && se.EffectiveUntilUtc > window.ApplicableDateFromUtc)
                .ToListAsync(cancellationToken);

            // Generate sessions: for each day in the applicable range, for each
            // work schedule matching that day of week, generate a session.
            // Skip sessions that fall within a Unavailability or Holiday exception.
            for (var day = window.ApplicableDateFromUtc; day < window.ApplicableDateUntilUtc; day = day.AddDays(1))
            {
                var dayOfWeek = day.DayOfWeek;
                var daySchedules = schedules
                    .Where(ws => ws.DayOfWeek == dayOfWeek && ws.IsEffectiveAt(day))
                    .ToList();

                foreach (var schedule in daySchedules)
                {
                    // Compute the session start/end in UTC for this day.
                    // The clinic's local timezone offset is applied in reverse:
                    // local 09:00 → UTC 09:00 - offset. For Iran (UTC+3:30),
                    // local 09:00 → UTC 05:30.
                    // For simplicity in this implementation, we assume the
                    // clinic timezone is UTC+3:30 (Asia/Tehran). The full
                    // implementation would read this from ClinicSettings.
                    var clinicOffset = TimeSpan.FromHours(3.5);
                    var localStart = day.Date + schedule.StartTime;
                    var localEnd = day.Date + schedule.EndTime;
                    var startUtc = localStart - clinicOffset;
                    var endUtc = localEnd - clinicOffset;

                    // Skip if outside the applicable range
                    if (startUtc < window.ApplicableDateFromUtc
                        || endUtc > window.ApplicableDateUntilUtc)
                        continue;

                    // Skip if covered by an Unavailability or Holiday exception
                    var isUnavailable = exceptions.Any(e =>
                        (e.Type == ScheduleExceptionType.Unavailability || e.Type == ScheduleExceptionType.Holiday)
                        && e.IsActiveAt(startUtc)
                        && e.OverlapsRange(startUtc, endUtc));
                    if (isUnavailable)
                        continue;

                    // Check for idempotency: compute the deterministic key
                    var idempotencyKey = PublishedSession.BuildIdempotencyKey(window.Id, dentist.Id, startUtc);

                    var existing = await _dbContext.PublishedSessions
                        .AnyAsync(ps => ps.IdempotencyKey == idempotencyKey, cancellationToken);
                    if (existing)
                        continue; // already published — idempotent skip

                    var session = PublishedSession.Create(
                        window.Id, dentist.Id, startUtc, endUtc, _clock);

                    await _dbContext.PublishedSessions.AddAsync(session, cancellationToken);
                    newSessionsCount++;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "PublicationService: published {Count} new sessions for window {WindowId}",
            newSessionsCount, bookingWindowId);

        return newSessionsCount;
    }
}
