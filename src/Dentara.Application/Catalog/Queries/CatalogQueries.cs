namespace Dentara.Application.Catalog.Queries;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

public sealed record GetServicesQuery(
    bool? IsActive = null,
    string? CorrelationIdHeader = null)
    : IQuery<IReadOnlyList<ServiceDto>>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

public sealed record GetServiceByIdQuery(
    Guid ServiceId,
    string? CorrelationIdHeader = null)
    : IQuery<ServiceDto>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

public sealed record ServiceDto(
    Guid Id, string Name, string? Description,
    int DurationMinutes, decimal PriceAmount, string Currency,
    bool IsActive, int Version,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public sealed record GetResourcesQuery(
    bool? IsActive = null,
    int? TypeValue = null,
    string? CorrelationIdHeader = null)
    : IQuery<IReadOnlyList<ResourceDto>>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

public sealed record GetResourceByIdQuery(
    Guid ResourceId,
    string? CorrelationIdHeader = null)
    : IQuery<ResourceDto>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

public sealed record ResourceDto(
    Guid Id, string Name, string? Description,
    int TypeValue, string TypeName, int Capacity,
    bool IsActive, Guid? OwnerDentistId,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);
