namespace Dentara.Infrastructure.Gateways.Sms;

using Dentara.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sandbox SMS gateway — logs messages to the console instead of sending them.
/// Used for development and testing.
///
/// For production, replace with SmsIrGateway (per user choice: sms.ir).
/// The production adapter must:
///   - Call the sms.ir REST API with the configured API key.
///   - Use OTP templates for OTP messages (SendOtpAsync).
///   - Implement retry with exponential backoff.
///   - Log the message ID returned by sms.ir for audit.
/// </summary>
public sealed class SandboxSmsGateway : ISmsGateway
{
    private readonly ILogger<SandboxSmsGateway> _logger;

    public SandboxSmsGateway(ILogger<SandboxSmsGateway> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "SMS → {PhoneNumber}: {Message}",
            phoneNumber,
            message);
        return Task.FromResult(true);
    }

    public Task<bool> SendOtpAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "OTP → {PhoneNumber}: code={Code}",
            phoneNumber,
            otpCode);
        return Task.FromResult(true);
    }
}
