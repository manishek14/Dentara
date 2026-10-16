namespace Dentara.Application.Identity.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Services.Identity;
using MediatR;

public sealed class RequestOtpCommandHandler
    : IRequestHandler<RequestOtpCommand, Result<OtpRequestResult>>
{
    private readonly IOtpService _otpService;

    public RequestOtpCommandHandler(IOtpService otpService)
    {
        _otpService = otpService;
    }

    public async Task<Result<OtpRequestResult>> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _otpService.GenerateAsync(request.PhoneNumber, request.Purpose, cancellationToken);
            return Result<OtpRequestResult>.Success(new OtpRequestResult(
                result.WasNewlyGenerated, result.ExpiresAtUtc));
        }
        catch (Exception ex)
        {
            return Result<OtpRequestResult>.FromException(ex);
        }
    }
}

public sealed class VerifyOtpCommandHandler
    : IRequestHandler<VerifyOtpCommand, Result<VerifyOtpResult>>
{
    private readonly IOtpService _otpService;
    private readonly IJwtTokenService _jwtTokenService;

    public VerifyOtpCommandHandler(IOtpService otpService, IJwtTokenService jwtTokenService)
    {
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<Result<VerifyOtpResult>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _otpService.VerifyAsync(
                request.PhoneNumber, request.Purpose, request.OtpCode, cancellationToken);

            var token = await _jwtTokenService.IssueAsync(user, cancellationToken);

            return Result<VerifyOtpResult>.Success(new VerifyOtpResult(
                user.Id,
                token.AccessToken,
                token.RefreshToken,
                token.ExpiresAtUtc,
                user.Roles.Select(r => Domain.Identity.Role.FromValue(r.RoleValue).Name).ToList()));
        }
        catch (Exception ex)
        {
            return Result<VerifyOtpResult>.FromException(ex);
        }
    }
}
