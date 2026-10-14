namespace Dentara.UnitTests.Payments;

using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using FluentAssertions;
using Xunit;

public class IdempotencyRecordTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var rec = IdempotencyRecord.Create(
            "payment_callback", "Zarinpal|EVT-001", _clock,
            userId: Guid.NewGuid(),
            requestHash: "abc123");

        rec.Scope.Should().Be("payment_callback");
        rec.Key.Should().Be("Zarinpal|EVT-001");
        rec.Status.Should().Be(IdempotencyStatus.InProgress);
        rec.RequestHash.Should().Be("abc123");
    }

    [Fact]
    public void Create_WithTtl_SetsExpiresAt()
    {
        var rec = IdempotencyRecord.Create(
            "sms_otp", "+98912...|login", _clock,
            ttl: TimeSpan.FromMinutes(5));

        rec.ExpiresAtUtc.Should().Be(_clock.UtcNow.AddMinutes(5));
    }

    [Fact]
    public void Create_WithoutTtl_DoesNotSetExpiry()
    {
        var rec = IdempotencyRecord.Create("X", "Y", _clock);

        rec.ExpiresAtUtc.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyScope_Throws(string scope)
    {
        var act = () => IdempotencyRecord.Create(scope, "key", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyKey_Throws(string key)
    {
        var act = () => IdempotencyRecord.Create("scope", key, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTooLongScope_Throws()
    {
        var scope = new string('x', 65);

        var act = () => IdempotencyRecord.Create(scope, "key", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkCompleted_FromInProgress_TransitionsToCompleted()
    {
        var rec = IdempotencyRecord.Create("X", "Y", _clock);

        rec.MarkCompleted("{\"ok\":true}", 200, _clock);

        rec.Status.Should().Be(IdempotencyStatus.Completed);
        rec.ResponsePayload.Should().Be("{\"ok\":true}");
        rec.ResponseStatusCode.Should().Be(200);
        rec.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkCompleted_IsIdempotent()
    {
        var rec = IdempotencyRecord.Create("X", "Y", _clock);
        rec.MarkCompleted("payload1", 200, _clock);
        var firstCompletedAt = rec.CompletedAtUtc;

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        rec.MarkCompleted("payload2", 200, _clock);

        rec.CompletedAtUtc.Should().Be(firstCompletedAt); // not overwritten
    }

    [Fact]
    public void MarkFailed_FromInProgress_TransitionsToFailed()
    {
        var rec = IdempotencyRecord.Create("X", "Y", _clock);

        rec.MarkFailed("Bad request", _clock);

        rec.Status.Should().Be(IdempotencyStatus.Failed);
        rec.ResponsePayload.Should().Be("Bad request");
    }

    [Fact]
    public void NaturalKey_CombinesScopeAndKey()
    {
        var rec = IdempotencyRecord.Create("payment_callback", "EVT-001", _clock);

        rec.NaturalKey.Should().Be("payment_callback|EVT-001");
    }

    [Fact]
    public void IsExpired_WithPastExpiry_ReturnsTrue()
    {
        _clock.UtcNow = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
        var rec = IdempotencyRecord.Create("X", "Y", _clock, ttl: TimeSpan.FromMinutes(5));

        _clock.UtcNow = _clock.UtcNow.AddMinutes(10);
        rec.IsExpired(_clock).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WithFutureExpiry_ReturnsFalse()
    {
        var rec = IdempotencyRecord.Create("X", "Y", _clock, ttl: TimeSpan.FromMinutes(5));

        rec.IsExpired(_clock).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithoutExpiry_ReturnsFalse()
    {
        var rec = IdempotencyRecord.Create("X", "Y", _clock);

        rec.IsExpired(_clock).Should().BeFalse();
    }
}
