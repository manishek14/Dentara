namespace Dentara.Domain.Specifications.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Specifications;

/// <summary>
/// Matches resource allocations that are Active (not Released) AND overlap a
/// given UTC time range. Used for the ADR-003 overlap check on each resource.
/// </summary>
public sealed class ActiveResourceAllocationOverlapSpecification : Specification<ResourceAllocation>
{
    private readonly TimeRange _range;
    private readonly Guid? _resourceId;
    private readonly Guid? _excludeAppointmentId;

    public ActiveResourceAllocationOverlapSpecification(
        TimeRange range,
        Guid? resourceId = null,
        Guid? excludeAppointmentId = null)
    {
        _range = range;
        _resourceId = resourceId;
        _excludeAppointmentId = excludeAppointmentId;
    }

    public override bool IsSatisfiedBy(ResourceAllocation candidate)
    {
        if (candidate == null) return false;
        if (candidate.Status != AllocationStatus.Active) return false;
        if (_excludeAppointmentId.HasValue && candidate.AppointmentId == _excludeAppointmentId.Value) return false;
        if (_resourceId.HasValue && candidate.ResourceId != _resourceId.Value) return false;
        return candidate.StartAtUtc < _range.EndAtUtc && _range.StartAtUtc < candidate.EndAtUtc;
    }

    public override System.Linq.Expressions.Expression<Func<ResourceAllocation, bool>> ToExpression()
    {
        return a =>
            a.StatusValue == AllocationStatus.Active.Value &&
            (_excludeAppointmentId == null || a.AppointmentId != _excludeAppointmentId.Value) &&
            (_resourceId == null || a.ResourceId == _resourceId.Value) &&
            a.StartAtUtc < _range.EndAtUtc && _range.StartAtUtc < a.EndAtUtc;
    }
}
