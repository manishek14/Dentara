namespace Dentara.UnitTests.Application;

using Dentara.Application.Common.Results;
using Dentara.Domain.Exceptions.Booking;
using FluentAssertions;
using Xunit;

public class ResultTests
{
    [Fact]
    public void Success_WithValue_ReturnsSuccessWithPayload()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public void Failure_WithCodeAndMessage_ReturnsFailure()
    {
        var result = Result<string>.Failure("BOOKING_SLOT_UNAVAILABLE", "Slot taken");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("BOOKING_SLOT_UNAVAILABLE");
        result.ErrorMessage.Should().Be("Slot taken");
        result.Value.Should().BeNull();
    }

    [Fact]
    public void GetValueOrThrow_OnSuccess_ReturnsValue()
    {
        var result = Result<int>.Success(42);

        result.GetValueOrThrow().Should().Be(42);
    }

    [Fact]
    public void GetValueOrThrow_OnFailure_Throws()
    {
        var result = Result<int>.Failure("X", "Y");

        var act = () => result.GetValueOrThrow();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FromException_WithDomainException_PreservesErrorCode()
    {
        var ex = new SlotUnavailableException(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

        var result = Result<string>.FromException(ex);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("BOOKING_SLOT_UNAVAILABLE");
    }

    [Fact]
    public void FromException_WithArgumentException_MapsToValidationError()
    {
        var ex = new ArgumentException("Bad input");

        var result = Result<string>.FromException(ex);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().Be("Bad input");
    }

    [Fact]
    public void FromException_WithUnexpectedException_MapsToUnexpectedError()
    {
        var ex = new InvalidOperationException("Something broke");

        var result = Result<string>.FromException(ex);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_OPERATION");
    }

    [Fact]
    public void UnitResult_Success_Works()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void UnitResult_Failure_Works()
    {
        var result = Result.Failure("X", "Y");

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("X");
    }
}
