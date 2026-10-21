namespace Dentara.Domain.Waitlist;

using Dentara.Domain.Common;

/// <summary>
/// Status of a waitlist entry.
/// </summary>
public sealed class WaitlistStatus : Enumeration
{
    /// <summary>
    /// Patient is on the waitlist, waiting for a slot to open.
    /// </summary>
    public static readonly WaitlistStatus Waiting = new(1, nameof(Waiting));

    /// <summary>
    /// A slot has been offered to the patient. They have a limited time
    /// (default 30 min) to accept before the offer expires and the next
    /// patient is offered.
    /// </summary>
    public static readonly WaitlistStatus Offered = new(2, nameof(Offered));

    /// <summary>
    /// Patient accepted the offer and booked the appointment.
    /// </summary>
    public static readonly WaitlistStatus Fulfilled = new(3, nameof(Fulfilled));

    /// <summary>
    /// Patient declined the offer or the offer expired.
    /// </summary>
    public static readonly WaitlistStatus Declined = new(4, nameof(Declined));

    /// <summary>
    /// Patient cancelled their waitlist entry.
    /// </summary>
    public static readonly WaitlistStatus Cancelled = new(5, nameof(Cancelled));

    private WaitlistStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<WaitlistStatus> All() => GetAll<WaitlistStatus>();
    public static WaitlistStatus FromValue(int value) => FromValue<WaitlistStatus>(value);
    public static WaitlistStatus FromName(string name) => FromName<WaitlistStatus>(name);

    public bool IsTerminal => this == Fulfilled || this == Declined || this == Cancelled;
}
