namespace Dentara.Infrastructure.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions.Booking;
using Dentara.Domain.Services.Booking;
using Dentara.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using System.Data;

/// <summary>
/// SQL Server implementation of IBookingTransactionService. This is the
/// CRITICAL anti-overbooking implementation per ADR-003.
///
/// Two execution paths:
///   - SQL Server (production): uses raw SQL with UPDLOCK+HOLDLOCK hints.
///   - SQLite (testing): uses EF Core LINQ queries with SERIALIZABLE isolation
///     via BEGIN IMMEDIATE TRANSACTION. SQLite doesn't support lock hints,
///     but BEGIN IMMEDIATE acquires a write lock that achieves equivalent
///     serialization for our purposes.
///
/// Flow (all inside one transaction):
///   1. Begin transaction with SERIALIZABLE isolation.
///   2. Lock the dentist row (UPDLOCK+HOLDLOCK on SQL Server, or implicit
///      write lock via BEGIN IMMEDIATE on SQLite).
///   3. Lock the required resource rows (ordered by Id to prevent deadlocks).
///   4. Check for active appointment overlaps on the dentist.
///   5. Check for active allocation overlaps on each resource.
///   6. INSERT the Appointment + all ResourceAllocations + AppointmentEvents.
///   7. Commit (or rollback on any failure).
///
/// CRITICAL: NO external calls (payment gateway, SMS) happen inside this
/// transaction. The transaction is short and only touches the database.
/// </summary>
public sealed class SqlBookingTransactionService : IBookingTransactionService
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<SqlBookingTransactionService> _logger;

    public SqlBookingTransactionService(
        DentaraDbContext dbContext,
        IClock clock,
        ILogger<SqlBookingTransactionService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    private bool IsSqlite =>
        _dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    public async Task<Appointment> ReserveAppointmentAsync(
        BookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartAtUtc.Offset != TimeSpan.Zero || request.EndAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("StartAtUtc and EndAtUtc must be UTC.");

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            try
            {
                // Step 1: Lock the dentist row (or verify it exists with write-lock intent)
                await LockDentistRowAsync(request.DentistId, transaction, cancellationToken);

                // Step 2: Find and lock required resources (ordered by Id to prevent deadlocks)
                var resourceLocks = await FindAndLockResourcesAsync(
                    request.DentistId,
                    request.RequiredResources,
                    transaction,
                    cancellationToken);

                // Step 3: Check appointment overlap on the dentist
                await EnsureDentistAvailableAsync(
                    request.DentistId, request.StartAtUtc, request.EndAtUtc,
                    transaction, cancellationToken);

                // Step 4: Check resource allocation overlaps
                foreach (var (requirement, lockedResourceIds) in resourceLocks)
                {
                    foreach (var resourceId in lockedResourceIds)
                    {
                        await EnsureResourceAvailableAsync(
                            resourceId, request.StartAtUtc, request.EndAtUtc,
                            transaction, cancellationToken);
                    }
                }

                // Step 5: Create the Appointment aggregate (with snapshot fields)
                var appointment = Appointment.Create(
                    request.PatientId,
                    request.DentistId,
                    request.ServiceId,
                    request.StartAtUtc,
                    request.EndAtUtc,
                    _clock,
                    request.ServiceNameSnapshot,
                    request.ServiceDurationMinutesSnapshot,
                    request.PriceAmountSnapshot,
                    request.CurrencySnapshot,
                    request.DentistDisplayNameSnapshot,
                    request.CancellationPolicyVersion,
                    request.CancellationFeePercent,
                    request.RefundablePercent,
                    request.TermsSnapshotHash,
                    request.PublishedSessionId,
                    request.PaymentWindow);

                _dbContext.Appointments.Add(appointment);

                // Step 6: Create ResourceAllocations for each locked resource
                foreach (var (requirement, lockedResourceIds) in resourceLocks)
                {
                    foreach (var resourceId in lockedResourceIds)
                    {
                        var allocation = ResourceAllocation.Create(
                            appointment.Id,
                            resourceId,
                            requirement.ResourceTypeValue,
                            request.StartAtUtc,
                            request.EndAtUtc,
                            _clock);
                        appointment.AddResourceAllocation(allocation);
                        _dbContext.ResourceAllocations.Add(allocation);
                    }
                }

                // Step 7: Save changes
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Step 8: Commit
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Reserved appointment {AppointmentId} for dentist {DentistId} at {StartAtUtc}",
                    appointment.Id, appointment.DentistId, appointment.StartAtUtc);

                return appointment;
            }
            catch (Exception ex) when (ex is not SlotUnavailableException
                                        && ex is not ResourceUnavailableException
                                        && ex is not DentistNotAvailableException)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Booking transaction failed; rolling back");
                throw;
            }
        });
    }

    private async Task LockDentistRowAsync(
        Guid dentistId, IDbContextTransaction transaction, CancellationToken ct)
    {
        if (IsSqlite)
        {
            // SQLite: BEGIN IMMEDIATE already holds a write lock. Just verify the dentist exists.
            var dentist = await _dbContext.DentistProfiles
                .FirstOrDefaultAsync(d => d.Id == dentistId && d.IsActive, ct);
            if (dentist == null)
                throw new DentistNotAvailableException(dentistId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return;
        }

        // SQL Server: use UPDLOCK+HOLDLOCK hints
        var sql = @"
            SELECT TOP 1 1 FROM DentistProfiles WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @dentistId AND IsActive = 1";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction.GetDbTransaction();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@dentistId", dentistId));

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null)
            throw new DentistNotAvailableException(dentistId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private async Task<List<(ResourceRequirementItem Requirement, List<Guid> LockedResourceIds)>> FindAndLockResourcesAsync(
        Guid dentistId,
        IReadOnlyList<ResourceRequirementItem> requirements,
        IDbContextTransaction transaction,
        CancellationToken ct)
    {
        var result = new List<(ResourceRequirementItem, List<Guid>)>();

        foreach (var requirement in requirements.OrderBy(r => r.ResourceTypeValue))
        {
            List<Guid> lockedIds;

            if (requirement.SpecificResourceId.HasValue)
            {
                lockedIds = await LockSpecificResourceAsync(
                    requirement.SpecificResourceId.Value, transaction, ct);
            }
            else if (requirement.RequireDentistOwned)
            {
                lockedIds = await LockDentistOwnedResourcesAsync(
                    dentistId, requirement.ResourceTypeValue, requirement.QuantityRequired, transaction, ct);
            }
            else
            {
                lockedIds = await LockSharedResourcesAsync(
                    requirement.ResourceTypeValue, requirement.QuantityRequired, transaction, ct);
            }

            if (lockedIds.Count < requirement.QuantityRequired)
                throw new ResourceUnavailableException(
                    lockedIds.FirstOrDefault(),
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddHours(1));

            result.Add((requirement, lockedIds));
        }

        return result;
    }

    private async Task<List<Guid>> LockSpecificResourceAsync(
        Guid resourceId, IDbContextTransaction transaction, CancellationToken ct)
    {
        if (IsSqlite)
        {
            var r = await _dbContext.ClinicResources
                .Where(x => x.Id == resourceId && x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(ct);
            return r;
        }

        var sql = @"
            SELECT Id FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @resourceId AND IsActive = 1
            ORDER BY Id";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction.GetDbTransaction();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@resourceId", resourceId));

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    private async Task<List<Guid>> LockDentistOwnedResourcesAsync(
        Guid dentistId, int resourceTypeValue, int quantity, IDbContextTransaction transaction, CancellationToken ct)
    {
        if (IsSqlite)
        {
            return await _dbContext.ClinicResources
                .Where(x => x.TypeValue == resourceTypeValue && x.IsActive && x.OwnerDentistId == dentistId)
                .OrderBy(x => x.Id)
                .Take(quantity)
                .Select(x => x.Id)
                .ToListAsync(ct);
        }

        var sql = @"
            SELECT TOP (@quantity) Id FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
            WHERE TypeValue = @typeValue AND IsActive = 1 AND OwnerDentistId = @dentistId
            ORDER BY Id";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction.GetDbTransaction();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@quantity", quantity));
        cmd.Parameters.Add(new SqlParameter("@typeValue", resourceTypeValue));
        cmd.Parameters.Add(new SqlParameter("@dentistId", dentistId));

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    private async Task<List<Guid>> LockSharedResourcesAsync(
        int resourceTypeValue, int quantity, IDbContextTransaction transaction, CancellationToken ct)
    {
        if (IsSqlite)
        {
            return await _dbContext.ClinicResources
                .Where(x => x.TypeValue == resourceTypeValue && x.IsActive && x.OwnerDentistId == null)
                .OrderBy(x => x.Id)
                .Take(quantity)
                .Select(x => x.Id)
                .ToListAsync(ct);
        }

        var sql = @"
            SELECT TOP (@quantity) Id FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
            WHERE TypeValue = @typeValue AND IsActive = 1 AND OwnerDentistId IS NULL
            ORDER BY Id";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction.GetDbTransaction();
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@quantity", quantity));
        cmd.Parameters.Add(new SqlParameter("@typeValue", resourceTypeValue));

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    private async Task EnsureDentistAvailableAsync(
        Guid dentistId, DateTimeOffset startUtc, DateTimeOffset endUtc,
        IDbContextTransaction transaction, CancellationToken ct)
    {
        if (IsSqlite)
        {
            // Use ADO.NET directly for SQLite — avoids SqlQueryRaw entity-mapping issues.
            var sql = @"
                SELECT COUNT(*) FROM Appointments
                WHERE DentistId = $dentistId
                  AND StatusValue IN (1, 2)
                  AND StartAtUtc < $endAt
                  AND EndAtUtc > $startAt";

            await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
            cmd.Transaction = transaction.GetDbTransaction();
            cmd.CommandText = sql;
            var p1 = cmd.CreateParameter();
            p1.ParameterName = "$dentistId";
            p1.Value = dentistId;
            var p2 = cmd.CreateParameter();
            p2.ParameterName = "$endAt";
            p2.Value = endUtc;
            var p3 = cmd.CreateParameter();
            p3.ParameterName = "$startAt";
            p3.Value = startUtc;
            cmd.Parameters.Add(p1);
            cmd.Parameters.Add(p2);
            cmd.Parameters.Add(p3);

            var countObj = await cmd.ExecuteScalarAsync(ct);
            var count = countObj == null ? 0 : Convert.ToInt32(countObj);
            if (count > 0)
                throw new SlotUnavailableException(dentistId, startUtc, endUtc);
            return;
        }

        var sqlServerSql = @"
            SELECT COUNT(*) FROM Appointments WITH (HOLDLOCK)
            WHERE DentistId = @dentistId
              AND StatusValue IN (1, 2)
              AND StartAtUtc < @endAt
              AND EndAtUtc > @startAt";

        await using var sqlServerCmd = _dbContext.Database.GetDbConnection().CreateCommand();
        sqlServerCmd.Transaction = transaction.GetDbTransaction();
        sqlServerCmd.CommandText = sqlServerSql;
        sqlServerCmd.Parameters.Add(new SqlParameter("@dentistId", dentistId));
        sqlServerCmd.Parameters.Add(new SqlParameter("@startAt", startUtc));
        sqlServerCmd.Parameters.Add(new SqlParameter("@endAt", endUtc));

        var result = (int)(await sqlServerCmd.ExecuteScalarAsync(ct))!;
        if (result > 0)
            throw new SlotUnavailableException(dentistId, startUtc, endUtc);
    }

    private async Task EnsureResourceAvailableAsync(
        Guid resourceId, DateTimeOffset startUtc, DateTimeOffset endUtc,
        IDbContextTransaction transaction, CancellationToken ct)
    {
        // Get resource capacity
        int capacity;
        if (IsSqlite)
        {
            var capSql = "SELECT Capacity FROM ClinicResources WHERE Id = $resourceId";
            await using var capCmd = _dbContext.Database.GetDbConnection().CreateCommand();
            capCmd.Transaction = transaction.GetDbTransaction();
            capCmd.CommandText = capSql;
            var capP = capCmd.CreateParameter();
            capP.ParameterName = "$resourceId";
            capP.Value = resourceId;
            capCmd.Parameters.Add(capP);
            var capacityObj = await capCmd.ExecuteScalarAsync(ct);
            capacity = capacityObj == null ? 0 : Convert.ToInt32(capacityObj);
        }
        else
        {
            var capSql = "SELECT Capacity FROM ClinicResources WITH (HOLDLOCK) WHERE Id = @resourceId";
            await using var capCmd = _dbContext.Database.GetDbConnection().CreateCommand();
            capCmd.Transaction = transaction.GetDbTransaction();
            capCmd.CommandText = capSql;
            capCmd.Parameters.Add(new SqlParameter("@resourceId", resourceId));
            var capacityObj = await capCmd.ExecuteScalarAsync(ct);
            capacity = capacityObj == null ? 0 : Convert.ToInt32(capacityObj);
        }

        // Count active overlapping allocations
        int activeCount;
        if (IsSqlite)
        {
            var overlapSql = @"
                SELECT COUNT(*) FROM ResourceAllocations
                WHERE ResourceId = $resourceId
                  AND StatusValue = 1
                  AND StartAtUtc < $endAt
                  AND EndAtUtc > $startAt";

            await using var overlapCmd = _dbContext.Database.GetDbConnection().CreateCommand();
            overlapCmd.Transaction = transaction.GetDbTransaction();
            overlapCmd.CommandText = overlapSql;
            var p1 = overlapCmd.CreateParameter();
            p1.ParameterName = "$resourceId";
            p1.Value = resourceId;
            var p2 = overlapCmd.CreateParameter();
            p2.ParameterName = "$endAt";
            p2.Value = endUtc;
            var p3 = overlapCmd.CreateParameter();
            p3.ParameterName = "$startAt";
            p3.Value = startUtc;
            overlapCmd.Parameters.Add(p1);
            overlapCmd.Parameters.Add(p2);
            overlapCmd.Parameters.Add(p3);

            var overlapObj = await overlapCmd.ExecuteScalarAsync(ct);
            activeCount = overlapObj == null ? 0 : Convert.ToInt32(overlapObj);
        }
        else
        {
            var overlapSql = @"
                SELECT COUNT(*) FROM ResourceAllocations WITH (HOLDLOCK)
                WHERE ResourceId = @resourceId
                  AND StatusValue = 1
                  AND StartAtUtc < @endAt
                  AND EndAtUtc > @startAt";

            await using var overlapCmd = _dbContext.Database.GetDbConnection().CreateCommand();
            overlapCmd.Transaction = transaction.GetDbTransaction();
            overlapCmd.CommandText = overlapSql;
            overlapCmd.Parameters.Add(new SqlParameter("@resourceId", resourceId));
            overlapCmd.Parameters.Add(new SqlParameter("@startAt", startUtc));
            overlapCmd.Parameters.Add(new SqlParameter("@endAt", endUtc));

            activeCount = (int)(await overlapCmd.ExecuteScalarAsync(ct))!;
        }

        if (activeCount >= capacity)
            throw new ResourceUnavailableException(resourceId, startUtc, endUtc);
    }
}
