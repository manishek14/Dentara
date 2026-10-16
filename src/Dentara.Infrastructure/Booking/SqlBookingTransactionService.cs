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
using System.Text.Json;

/// <summary>
/// SQL Server implementation of IBookingTransactionService. This is the
/// CRITICAL anti-overbooking implementation per ADR-003.
///
/// Flow (all inside one SQL Server transaction):
///   1. Begin transaction with SERIALIZABLE isolation (defensive; the
///      UPDLOCK+HOLDLOCK hints below are the real guard).
///   2. Lock the dentist row: SELECT … FROM DentistProfiles WITH (UPDLOCK,
///      HOLDLOCK) WHERE Id = @dentistId. This prevents two transactions
///      from simultaneously booking the same dentist.
///   3. Lock the required resource rows: SELECT … FROM ClinicResources
///      WITH (UPDLOCK, HOLDLOCK) WHERE Id IN (…) ORDER BY Id. Order by Id
///      prevents deadlocks between transactions booking overlapping resources.
///   4. Check for active appointment overlaps on the dentist:
///      SELECT COUNT(*) FROM Appointments WITH (HOLDLOCK)
///      WHERE DentistId = @dentistId AND StatusValue IN (1, 2)
///        AND StartAtUtc < @endAt AND EndAtUtc > @startAt.
///      If count > 0 → throw SlotUnavailableException.
///   5. For each required resource, check for active allocation overlaps:
///      SELECT COUNT(*) FROM ResourceAllocations WITH (HOLDLOCK)
///      WHERE ResourceId = @resourceId AND StatusValue = 1
///        AND StartAtUtc < @endAt AND EndAtUtc > @startAt.
///      If count + requestedQuantity > resource.Capacity → throw
///      ResourceUnavailableException.
///   6. INSERT the Appointment row.
///   7. INSERT all ResourceAllocation rows.
///   8. INSERT AppointmentEvent rows (Created, PaymentWindowStarted).
///   9. COMMIT the transaction.
///
/// If any step fails, the entire transaction rolls back — no partial
/// bookings, no orphan allocations.
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
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                // Step 1: Lock the dentist row (UPDLOCK + HOLDLOCK)
                await LockDentistRowAsync(request.DentistId, transaction.GetDbTransaction(), cancellationToken);

                // Step 2: Find and lock required resources (UPDLOCK + HOLDLOCK, ordered by Id)
                var resourceLocks = await FindAndLockResourcesAsync(
                    request.DentistId,
                    request.RequiredResources,
                    transaction.GetDbTransaction(),
                    cancellationToken);

                // Step 3: Check appointment overlap on the dentist
                await EnsureDentistAvailableAsync(
                    request.DentistId, request.StartAtUtc, request.EndAtUtc,
                    transaction.GetDbTransaction(), cancellationToken);

                // Step 4: Check resource allocation overlaps
                foreach (var (requirement, lockedResourceIds) in resourceLocks)
                {
                    foreach (var resourceId in lockedResourceIds)
                    {
                        await EnsureResourceAvailableAsync(
                            resourceId, request.StartAtUtc, request.EndAtUtc,
                            transaction.GetDbTransaction(), cancellationToken);
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

                // Step 7: Save changes (this persists Appointment + ResourceAllocations + AppointmentEvents)
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

    private async Task LockDentistRowAsync(Guid dentistId, System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        var sql = @"
            SELECT TOP 1 1 FROM DentistProfiles WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @dentistId AND IsActive = 1";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@dentistId", dentistId));

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result == null)
            throw new DentistNotAvailableException(dentistId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private async Task<List<(ResourceRequirementItem Requirement, List<Guid> LockedResourceIds)>> FindAndLockResourcesAsync(
        Guid dentistId,
        IReadOnlyList<ResourceRequirementItem> requirements,
        System.Data.Common.DbTransaction transaction,
        CancellationToken ct)
    {
        var result = new List<(ResourceRequirementItem, List<Guid>)>();

        foreach (var requirement in requirements.OrderBy(r => r.ResourceTypeValue))
        {
            List<Guid> lockedIds;

            if (requirement.SpecificResourceId.HasValue)
            {
                // Lock the specific resource
                lockedIds = await LockSpecificResourceAsync(
                    requirement.SpecificResourceId.Value, transaction, ct);
            }
            else if (requirement.RequireDentistOwned)
            {
                // Lock any dentist-owned resource of the required type
                lockedIds = await LockDentistOwnedResourcesAsync(
                    dentistId, requirement.ResourceTypeValue, requirement.QuantityRequired, transaction, ct);
            }
            else
            {
                // Lock any shared resource of the required type
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
        Guid resourceId, System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        var sql = @"
            SELECT Id FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @resourceId AND IsActive = 1
            ORDER BY Id";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@resourceId", resourceId));

        var ids = new List<Guid>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetGuid(0));
        return ids;
    }

    private async Task<List<Guid>> LockDentistOwnedResourcesAsync(
        Guid dentistId, int resourceTypeValue, int quantity, System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        var sql = @"
            SELECT TOP (@quantity) Id FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
            WHERE TypeValue = @typeValue AND IsActive = 1 AND OwnerDentistId = @dentistId
            ORDER BY Id";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction;
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
        int resourceTypeValue, int quantity, System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        var sql = @"
            SELECT TOP (@quantity) Id FROM ClinicResources WITH (UPDLOCK, HOLDLOCK)
            WHERE TypeValue = @typeValue AND IsActive = 1 AND OwnerDentistId IS NULL
            ORDER BY Id";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction;
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
        System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        var sql = @"
            SELECT COUNT(*) FROM Appointments WITH (HOLDLOCK)
            WHERE DentistId = @dentistId
              AND StatusValue IN (1, 2)
              AND StartAtUtc < @endAt
              AND EndAtUtc > @startAt";

        await using var cmd = _dbContext.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.Parameters.Add(new SqlParameter("@dentistId", dentistId));
        cmd.Parameters.Add(new SqlParameter("@startAt", startUtc));
        cmd.Parameters.Add(new SqlParameter("@endAt", endUtc));

        var count = (int)(await cmd.ExecuteScalarAsync(ct))!;
        if (count > 0)
            throw new SlotUnavailableException(dentistId, startUtc, endUtc);
    }

    private async Task EnsureResourceAvailableAsync(
        Guid resourceId, DateTimeOffset startUtc, DateTimeOffset endUtc,
        System.Data.Common.DbTransaction transaction, CancellationToken ct)
    {
        // Get resource capacity
        var capacitySql = "SELECT Capacity FROM ClinicResources WITH (HOLDLOCK) WHERE Id = @resourceId";
        await using var capCmd = _dbContext.Database.GetDbConnection().CreateCommand();
        capCmd.Transaction = transaction;
        capCmd.CommandText = capacitySql;
        capCmd.Parameters.Add(new SqlParameter("@resourceId", resourceId));
        var capacityObj = await capCmd.ExecuteScalarAsync(ct);
        var capacity = capacityObj == null ? 0 : Convert.ToInt32(capacityObj);

        // Count active overlapping allocations
        var overlapSql = @"
            SELECT COUNT(*) FROM ResourceAllocations WITH (HOLDLOCK)
            WHERE ResourceId = @resourceId
              AND StatusValue = 1
              AND StartAtUtc < @endAt
              AND EndAtUtc > @startAt";

        await using var overlapCmd = _dbContext.Database.GetDbConnection().CreateCommand();
        overlapCmd.Transaction = transaction;
        overlapCmd.CommandText = overlapSql;
        overlapCmd.Parameters.Add(new SqlParameter("@resourceId", resourceId));
        overlapCmd.Parameters.Add(new SqlParameter("@startAt", startUtc));
        overlapCmd.Parameters.Add(new SqlParameter("@endAt", endUtc));

        var activeCount = (int)(await overlapCmd.ExecuteScalarAsync(ct))!;
        if (activeCount >= capacity)
            throw new ResourceUnavailableException(resourceId, startUtc, endUtc);
    }
}
