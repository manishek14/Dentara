namespace Dentara.UnitTests.Application;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Application.Booking.Commands;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions.Booking;
using Dentara.Domain.Services.Booking;
using FluentAssertions;
using Moq;
using Xunit;

public class ConfirmAppointmentCommandHandlerTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int hour) =>
        new DateTimeOffset(2026, 11, 1, hour, 0, 0, TimeSpan.Zero);

    private Appointment CreatePendingAppointment()
    {
        return Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(10), Utc(11), _clock,
            "X", 60, 500_000m, "IRR", "Dr", 1, 15m, 85m);
    }

    [Fact]
    public async Task Handle_WithPendingPayment_TransitionsToConfirmed()
    {
        // Arrange
        var appt = CreatePendingAppointment();
        var repoMock = new Mock<IRepository<Appointment>>();
        repoMock.Setup(r => r.GetByIdAsync(appt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appt);
        var uowMock = new Mock<IUnitOfWork>();
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var handler = new ConfirmAppointmentCommandHandler(
            uowMock.Object, repoMock.Object, dispatcherMock.Object, _clock);

        // Act
        var result = await handler.Handle(
            new ConfirmAppointmentCommand(appt.Id, Guid.NewGuid(), 500_000m),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        appt.Status.Should().Be(AppointmentStatus.Confirmed);
        repoMock.Verify(r => r.Update(appt), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        dispatcherMock.Verify(d => d.DispatchAsync(appt, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithAlreadyConfirmed_ReturnsSuccessWithoutMutation()
    {
        // Arrange
        var appt = CreatePendingAppointment();
        appt.Confirm(_clock, Guid.NewGuid(), 500_000m);
        var originalConfirmedAt = appt.ConfirmedAtUtc;

        var repoMock = new Mock<IRepository<Appointment>>();
        repoMock.Setup(r => r.GetByIdAsync(appt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appt);
        var uowMock = new Mock<IUnitOfWork>();
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var handler = new ConfirmAppointmentCommandHandler(
            uowMock.Object, repoMock.Object, dispatcherMock.Object, _clock);

        // Act
        var result = await handler.Handle(
            new ConfirmAppointmentCommand(appt.Id, Guid.NewGuid(), 500_000m),
            CancellationToken.None);

        // Assert — idempotent: returns success without re-saving or re-dispatching
        result.IsSuccess.Should().BeTrue();
        repoMock.Verify(r => r.Update(It.IsAny<Appointment>()), Times.Never);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<AggregateRoot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithNonExistentAppointment_ReturnsNotFoundFailure()
    {
        // Arrange
        var repoMock = new Mock<IRepository<Appointment>>();
        repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Appointment?)null);
        var uowMock = new Mock<IUnitOfWork>();
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var handler = new ConfirmAppointmentCommandHandler(
            uowMock.Object, repoMock.Object, dispatcherMock.Object, _clock);

        // Act
        var result = await handler.Handle(
            new ConfirmAppointmentCommand(Guid.NewGuid(), Guid.NewGuid(), 100m),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NOTFOUND_APPOINTMENT");
    }

    [Fact]
    public async Task Handle_WithExpiredAppointment_ReturnsFailure()
    {
        // Arrange — appointment in Expired state, Confirm should throw
        var appt = CreatePendingAppointment();
        appt.Expire(_clock, paymentWasVerifiedAbsent: true);

        var repoMock = new Mock<IRepository<Appointment>>();
        repoMock.Setup(r => r.GetByIdAsync(appt.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appt);
        var uowMock = new Mock<IUnitOfWork>();
        var dispatcherMock = new Mock<IDomainEventDispatcher>();
        var handler = new ConfirmAppointmentCommandHandler(
            uowMock.Object, repoMock.Object, dispatcherMock.Object, _clock);

        // Act
        var result = await handler.Handle(
            new ConfirmAppointmentCommand(appt.Id, Guid.NewGuid(), 500_000m),
            CancellationToken.None);

        // Assert — the InvalidAppointmentTransitionException is mapped to its ErrorCode
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("BOOKING_INVALID_TRANSITION");
    }
}
