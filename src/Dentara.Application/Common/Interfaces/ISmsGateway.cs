namespace Dentara.Application.Common.Interfaces;

/// <summary>
/// SMS gateway abstraction. The Application layer uses this to dispatch OTP
/// codes and notifications. The Infrastructure layer provides concrete adapters:
/// SandboxSmsGateway (always succeeds, logs to console) and SmsIrGateway
/// (per user choice: sms.ir).
/// </summary>
public interface ISmsGateway
{
    /// <summary>
    /// Sends an SMS. Returns true if the gateway accepted the message.
    /// Does NOT throw on gateway failures — returns false so the caller can
    /// decide whether to retry (e.g. via outbox).
    /// </summary>
    Task<bool> SendAsync(
        string phoneNumber,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an OTP via the gateway's OTP template (if supported). Falls back
    /// to plain SendAsync if no template is configured.
    /// </summary>
    Task<bool> SendOtpAsync(
        string phoneNumber,
        string otpCode,
        CancellationToken cancellationToken = default);
}
