namespace Dentara.Domain.Common;

/// <summary>
/// Abstract clock to make time injection testable.
/// The production implementation returns DateTimeOffset.UtcNow.
/// Tests can use FakeClock for deterministic behavior.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
