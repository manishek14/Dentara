namespace Dentara.Api.Controllers;

using Dentara.Api.Contracts.Common;
using Dentara.Api.Contracts.Identity;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Identity.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Authentication endpoints: OTP request + verify, JWT issuance.
/// </summary>
[ApiController]
[Route("api/identity")]
public sealed class IdentityController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<IdentityController> _logger;

    public IdentityController(IMediator mediator, ILogger<IdentityController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Requests an OTP for the given phone number. If the user doesn't exist,
    /// they are auto-registered with the Patient role. Rate-limited to one OTP
    /// per minute per (phone, purpose) per ADR-006.
    /// </summary>
    [HttpPost("otp/request")]
    [ProducesResponseType(typeof(RequestOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestOtp(
        [FromBody] RequestOtpRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Purpose))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "PhoneNumber and Purpose are required."));

        var command = new RequestOtpCommand(
            request.PhoneNumber,
            request.Purpose,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => Ok(new RequestOtpResponse(
            r.WasNewlyGenerated, r.ExpiresAtUtc)));
    }

    /// <summary>
    /// Verifies an OTP and returns a JWT + refresh token. The OTP is single-use:
    /// successful verification removes it from the store.
    /// </summary>
    [HttpPost("otp/verify")]
    [ProducesResponseType(typeof(VerifyOtpResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber) ||
            string.IsNullOrWhiteSpace(request.Purpose) ||
            string.IsNullOrWhiteSpace(request.OtpCode))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "PhoneNumber, Purpose, and OtpCode are required."));

        var command = new VerifyOtpCommand(
            request.PhoneNumber,
            request.Purpose,
            request.OtpCode,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => Ok(new VerifyOtpResponse(
            r.UserId, r.AccessToken, r.RefreshToken, r.ExpiresAtUtc, r.Roles)));
    }
}
