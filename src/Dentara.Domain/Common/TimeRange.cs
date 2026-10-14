namespace Dentara.Domain.Common;

/// <summary>
/// Represents a half-open time range [Start, End).
/// All timestamps are UTC. Stored as two columns via owned entity configuration.
/// </summary>
public readonly record struct TimeRange
{
    public DateTimeOffset StartAtUtc { get; init; }

    public DateTimeOffset EndAtUtc { get; init; }

    public TimeRange(DateTimeOffset startAtUtc, DateTimeOffset endAtUtc)
    {
        if (startAtUtc >= endAtUtc)
            throw new ArgumentException("Start must be strictly before end.", nameof(startAtUtc));
        if (startAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Start must be UTC.", nameof(startAtUtc));
        if (endAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("End must be UTC.", nameof(endAtUtc));

        StartAtUtc = startAtUtc;
        EndAtUtc = endAtUtc;
    }

    public TimeSpan Duration => EndAtUtc - StartAtUtc;

    /// <summary>
    /// Returns true if this range overlaps with <paramref name="other"/>.
    /// Half-open: [a, b) overlaps [c, d) iff a &lt; d AND c &lt; b.
    /// Touching ranges (a == d) do NOT overlap.
    /// </summary>
    public bool Overlaps(TimeRange other) =>
        StartAtUtc < other.EndAtUtc && other.StartAtUtc < EndAtUtc;

    /// <summary>
    /// Returns true if <paramref name="other"/> is entirely contained within this range.
    /// </summary>
    public bool Contains(TimeRange other) =>
        StartAtUtc <= other.StartAtUtc && other.EndAtUtc <= EndAtUtc;

    public override string ToString() => $"[{StartAtUtc:O}, {EndAtUtc:O})";
}
