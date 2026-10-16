namespace Dentara.Infrastructure.Gateways.Sms;

using Dentara.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

/// <summary>
/// sms.ir gateway adapter (stub — production implementation TBD).
///
/// To use: register with services.AddSingleton<ISmsGateway, SmsIrGateway>().
/// Configure "SmsIr:ApiKey" and "SmsIr:LineNumber" in appsettings.json.
///
/// NOTE: This is a minimal stub. Production implementation requires:
///   - Calling https://api.sms.ir/v1/send/verify or /v1/send/bulk.
///   - Using template IDs for OTP messages (SendOtpAsync).
///   - Proper error handling and retry logic.
/// </summary>
public sealed class SmsIrGateway : ISmsGateway
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _lineNumber;
    private readonly ILogger<SmsIrGateway> _logger;

    public SmsIrGateway(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SmsIrGateway> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["SmsIr:ApiKey"]
            ?? throw new InvalidOperationException("SmsIr:ApiKey not configured.");
        _lineNumber = configuration["SmsIr:LineNumber"]
            ?? throw new InvalidOperationException("SmsIr:LineNumber not configured.");
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    public async Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // Minimal stub — production would call the real API.
        _logger.LogWarning("SmsIrGateway.SendAsync is a stub. Phone={Phone}, Message={Message}",
            phoneNumber, message);

        var payload = new
        {
            lineNumber = _lineNumber,
            messageText = message,
            mobiles = new[] { phoneNumber }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "https://api.sms.ir/v1/send/bulk", payload, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SmsIrGateway.SendAsync failed for {Phone}", phoneNumber);
            return false;
        }
    }

    public async Task<bool> SendOtpAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("SmsIrGateway.SendOtpAsync is a stub. Phone={Phone}, Code={Code}",
            phoneNumber, otpCode);

        // Minimal stub — production would use a template ID.
        var message = $"کد تأیید شما: {otpCode}";
        return await SendAsync(phoneNumber, message, cancellationToken);
    }
}
