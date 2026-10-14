namespace Dentara.UnitTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class AppointmentStatusTests
{
    [Fact]
    public void All_ReturnsSixStatuses()
    {
        var all = AppointmentStatus.All().ToList();

        all.Should().HaveCount(6);
        all.Should().Contain(new[]
        {
            AppointmentStatus.PendingPayment,
            AppointmentStatus.Confirmed,
            AppointmentStatus.Expired,
            AppointmentStatus.Cancelled,
            AppointmentStatus.Completed,
            AppointmentStatus.NoShow
        });
    }

    [Theory]
    [InlineData(1, "PendingPayment")]
    [InlineData(2, "Confirmed")]
    [InlineData(3, "Expired")]
    [InlineData(4, "Cancelled")]
    [InlineData(5, "Completed")]
    [InlineData(6, "NoShow")]
    public void FromValue_ReturnsExpectedStatus(int value, string expectedName)
    {
        var status = AppointmentStatus.FromValue(value);
        status.Name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(1, true)]   // PendingPayment
    [InlineData(2, true)]   // Confirmed
    [InlineData(3, false)]  // Expired
    [InlineData(4, false)]  // Cancelled
    [InlineData(5, false)]  // Completed
    [InlineData(6, false)]  // NoShow
    public void ConsumesCapacity_IsCorrectForEachStatus(int statusValue, bool expected)
    {
        var status = AppointmentStatus.FromValue(statusValue);
        status.ConsumesCapacity.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, false)]  // PendingPayment
    [InlineData(2, false)]  // Confirmed
    [InlineData(3, true)]   // Expired
    [InlineData(4, true)]   // Cancelled
    [InlineData(5, true)]   // Completed
    [InlineData(6, true)]   // NoShow
    public void IsTerminal_IsCorrectForEachStatus(int statusValue, bool expected)
    {
        var status = AppointmentStatus.FromValue(statusValue);
        status.IsTerminal.Should().Be(expected);
    }

    [Theory]
    [InlineData(5, true)]   // Completed
    [InlineData(6, true)]   // NoShow
    [InlineData(1, false)]  // PendingPayment
    [InlineData(2, false)]  // Confirmed
    [InlineData(3, false)]  // Expired
    [InlineData(4, false)]  // Cancelled
    public void IsHistorical_IsCorrectForEachStatus(int statusValue, bool expected)
    {
        var status = AppointmentStatus.FromValue(statusValue);
        status.IsHistorical.Should().Be(expected);
    }
}
