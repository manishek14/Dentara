namespace Dentara.Domain.Specifications.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Specifications;

/// <summary>
/// Matches appointments in PendingPayment state whose ExpiresAtUtc has passed.
/// Used by the Background Worker to find expired-but-not-yet-processed appointments.
///
/// IMPORTANT (ADR-004): the worker must NOT auto-expire these. For each match,
/// it must first verify that no PaymentAttempt is in Unknown/Succeeded state.
/// Only then can it call Appointment.Expire(paymentWasVerifiedAbsent: true).
/// </summary>
public sealed class ExpiredPendingPaymentSpecification : Specification<Appointment>
{
    private readonly DateTimeOffset _nowUtc;

    public ExpiredPendingPaymentSpecification(DateTimeOffset nowUtc)
    {
        if (nowUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("nowUtc must be UTC.", nameof(nowUtc));
        _nowUtc = nowUtc;
    }

    public override bool IsSatisfiedBy(Appointment candidate)
    {
        if (candidate == null) return false;
        return candidate.Status == AppointmentStatus.PendingPayment
            && candidate.ExpiresAtUtc <= _nowUtc;
    }

    public override System.Linq.Expressions.Expression<Func<Appointment, bool>> ToExpression()
    {
        return a =>
            a.StatusValue == AppointmentStatus.PendingPayment.Value &&
            a.ExpiresAtUtc <= _nowUtc;
    }
}
