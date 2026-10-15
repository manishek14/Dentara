namespace Dentara.UnitTests.Exceptions;

using Dentara.Domain.Exceptions.Booking;
using Dentara.Domain.Exceptions.Payments;
using FluentAssertions;
using Xunit;

public class DomainExceptionTests
{
    [Fact]
    public void InvalidAppointmentTransitionException_PreservesData()
    {
        var apptId = Guid.NewGuid();
        var ex = new InvalidAppointmentTransitionException(apptId, "Expired", "Confirmed");

        ex.ErrorCode.Should().Be("BOOKING_INVALID_TRANSITION");
        ex.EntityId.Should().Be(apptId);
        ex.FromStatus.Should().Be("Expired");
        ex.ToStatus.Should().Be("Confirmed");
        ex.Message.Should().Contain(apptId.ToString());
        ex.Message.Should().Contain("Expired");
        ex.Message.Should().Contain("Confirmed");
    }

    [Fact]
    public void SlotUnavailableException_PreservesDentistAndTimeRange()
    {
        var dentistId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero);

        var ex = new SlotUnavailableException(dentistId, start, end);

        ex.ErrorCode.Should().Be("BOOKING_SLOT_UNAVAILABLE");
        ex.DentistId.Should().Be(dentistId);
        ex.RequestedStartUtc.Should().Be(start);
        ex.RequestedEndUtc.Should().Be(end);
    }

    [Fact]
    public void ResourceUnavailableException_PreservesResourceId()
    {
        var resId = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 11, 1, 11, 0, 0, TimeSpan.Zero);

        var ex = new ResourceUnavailableException(resId, start, end);

        ex.ErrorCode.Should().Be("BOOKING_RESOURCE_UNAVAILABLE");
        ex.ResourceId.Should().Be(resId);
    }

    [Fact]
    public void AppointmentInTerminalStateException_PreservesStatus()
    {
        var apptId = Guid.NewGuid();
        var ex = new AppointmentInTerminalStateException(apptId, "Completed");

        ex.ErrorCode.Should().Be("BOOKING_APPOINTMENT_TERMINAL");
        ex.CurrentStatus.Should().Be("Completed");
    }

    [Fact]
    public void RefundExceedsPaymentException_PreservesAmounts()
    {
        var paId = Guid.NewGuid();
        var ex = new RefundExceedsPaymentException(paId, 500_000m, 300_000m, 250_000m);

        ex.ErrorCode.Should().Be("PAYMENT_REFUND_EXCEEDS_TOTAL");
        ex.PaymentAmount.Should().Be(500_000m);
        ex.AlreadyRefunded.Should().Be(300_000m);
        ex.AttemptedRefund.Should().Be(250_000m);
    }

    [Fact]
    public void InvalidWebhookSignatureException_PreservesProviderAndEventId()
    {
        var ex = new InvalidWebhookSignatureException("Zarinpal", "EVT-001");

        ex.ErrorCode.Should().Be("PAYMENT_INVALID_WEBHOOK_SIGNATURE");
        ex.Provider.Should().Be("Zarinpal");
        ex.ProviderEventId.Should().Be("EVT-001");
    }

    [Fact]
    public void WebhookAmountMismatchException_PreservesAmounts()
    {
        var paId = Guid.NewGuid();
        var ex = new WebhookAmountMismatchException(paId, 500_000m, 100_000m, "IRR", "IRR");

        ex.ErrorCode.Should().Be("PAYMENT_WEBHOOK_AMOUNT_MISMATCH");
        ex.ExpectedAmount.Should().Be(500_000m);
        ex.ReceivedAmount.Should().Be(100_000m);
    }

    [Fact]
    public void IdempotencyConflictException_PreservesScopeAndKey()
    {
        var ex = new IdempotencyConflictException("payment_callback", "Zarinpal|EVT-001", "Completed");

        ex.ErrorCode.Should().Be("IDEMPOTENCY_CONFLICT");
        ex.Scope.Should().Be("payment_callback");
        ex.Key.Should().Be("Zarinpal|EVT-001");
        ex.ExistingStatus.Should().Be("Completed");
    }

    [Fact]
    public void AllDomainExceptions_HaveNonEmptyErrorCode()
    {
        // Verify that all domain exceptions have a meaningful error code that the
        // API layer can map to HTTP status + user message.
        var exceptions = new Dentara.Domain.Exceptions.DomainException[]
        {
            new InvalidAppointmentTransitionException(Guid.NewGuid(), "X", "Y"),
            new SlotUnavailableException(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            new ResourceUnavailableException(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            new DentistNotAvailableException(Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            new AppointmentInTerminalStateException(Guid.NewGuid(), "Completed"),
            new InvalidPaymentTransitionException(Guid.NewGuid(), "X", "Y"),
            new RefundExceedsPaymentException(Guid.NewGuid(), 100m, 50m, 75m),
            new InvalidWebhookSignatureException("Zarinpal", "EVT"),
            new WebhookAmountMismatchException(Guid.NewGuid(), 100m, 50m),
            new IdempotencyConflictException("scope", "key", "Completed"),
        };

        foreach (var ex in exceptions)
        {
            ex.ErrorCode.Should().NotBeNullOrEmpty($"exception type {ex.GetType().Name} must have an ErrorCode");
            ex.Message.Should().NotBeNullOrEmpty($"exception type {ex.GetType().Name} must have a Message");
        }
    }
}
