namespace Dentara.Application.Catalog.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using MediatR;

// ─── Service Commands ───

public sealed record CreateServiceCommand(
    string Name,
    int DurationMinutes,
    decimal PriceAmount,
    string Currency,
    string? Description = null,
    string? CorrelationId = null)
    : ICommand<ServiceResult>, IHasCorrelationId
{
}

public sealed record UpdateServiceCommand(
    Guid ServiceId,
    string Name,
    string? Description = null,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record UpdateServicePricingCommand(
    Guid ServiceId,
    int DurationMinutes,
    decimal PriceAmount,
    string Currency,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record DeactivateServiceCommand(
    Guid ServiceId,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record ServiceResult(Guid Id, int Version);

// ─── Resource Commands ───

public sealed record CreateResourceCommand(
    string Name,
    int ResourceTypeValue,
    int Capacity,
    string? Description = null,
    Guid? OwnerDentistId = null,
    string? CorrelationId = null)
    : ICommand<ResourceResult>, IHasCorrelationId
{
}

public sealed record UpdateResourceCommand(
    Guid ResourceId,
    string Name,
    string? Description = null,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record DeactivateResourceCommand(
    Guid ResourceId,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record ResourceResult(Guid Id);

// ─── DentistService Link Commands ───

public sealed record LinkDentistServiceCommand(
    Guid DentistId,
    Guid ServiceId,
    int? DurationMinutesOverride = null,
    decimal? PriceAmountOverride = null,
    string? CurrencyOverride = null,
    string? CorrelationId = null)
    : ICommand<DentistServiceResult>, IHasCorrelationId
{
}

public sealed record UnlinkDentistServiceCommand(
    Guid DentistId,
    Guid ServiceId,
    string? CorrelationId = null)
    : IRequest<Result>, IHasCorrelationId
{
}

public sealed record DentistServiceResult(Guid Id);
