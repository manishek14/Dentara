namespace Dentara.Domain.Specifications.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Specifications;

/// <summary>
/// Matches appointments that overlap a given UTC time range AND are in a state
/// that consumes capacity (PendingPayment or Confirmed).
///
/// This is the SPECIFICATION form of the overlap check that ADR-003 enforces
/// via SQL Server locks. The Application layer uses this specification to:
///   1. Pre-check (informational) before opening a transaction.
///   2. Build the actual SQL WHERE clause inside the transaction.
///
/// Note: this specification ALONE does NOT prevent overbooking. The DB
/// transaction with UPDLOCK+HOLDLOCK is the authoritative guard. This
/// specification is for the in-transaction overlap query.
/// </summary>
public sealed class ActiveAppointmentOverlapSpecification : Specification<Appointment>
{
    private readonly TimeRange _range;
    private readonly Guid? _dentistId;
    private readonly Guid? _patientId;
    private readonly Guid? _excludeAppointmentId;

    public ActiveAppointmentOverlapSpecification(
        TimeRange range,
        Guid? dentistId = null,
        Guid? patientId = null,
        Guid? excludeAppointmentId = null)
    {
        _range = range;
        _dentistId = dentistId;
        _patientId = patientId;
        _excludeAppointmentId = excludeAppointmentId;
    }

    public override bool IsSatisfiedBy(Appointment candidate)
    {
        if (candidate == null) return false;
        if (!candidate.Status.ConsumesCapacity) return false;
        if (_excludeAppointmentId.HasValue && candidate.Id == _excludeAppointmentId.Value) return false;
        if (_dentistId.HasValue && candidate.DentistId != _dentistId.Value) return false;
        if (_patientId.HasValue && candidate.PatientId != _patientId.Value) return false;
        // Half-open overlap: [a, b) overlaps [c, d) iff a < d && c < b
        return candidate.StartAtUtc < _range.EndAtUtc && _range.StartAtUtc < candidate.EndAtUtc;
    }

    public override System.Linq.Expressions.Expression<Func<Appointment, bool>> ToExpression()
    {
        // Active capacity-consuming statuses
        var activeStatuses = new[] { AppointmentStatus.PendingPayment.Value, AppointmentStatus.Confirmed.Value };

        return a =>
            activeStatuses.Contains(a.StatusValue) &&
            (_excludeAppointmentId == null || a.Id != _excludeAppointmentId.Value) &&
            (_dentistId == null || a.DentistId == _dentistId.Value) &&
            (_patientId == null || a.PatientId == _patientId.Value) &&
            a.StartAtUtc < _range.EndAtUtc && _range.StartAtUtc < a.EndAtUtc;
    }
}
