namespace Dentara.Infrastructure.Clock;

using Dentara.Domain.Common;

/// <summary>
/// Default production implementation of IClock. Returns DateTimeOffset.UtcNow.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
