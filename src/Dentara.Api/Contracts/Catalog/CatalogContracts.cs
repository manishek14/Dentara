namespace Dentara.Api.Contracts.Catalog;

// ─── Service ───

public sealed record CreateServiceRequest(
    string Name,
    int DurationMinutes,
    decimal PriceAmount,
    string Currency,
    string? Description = null);

public sealed record UpdateServiceRequest(
    string Name,
    string? Description);

public sealed record UpdateServicePricingRequest(
    int DurationMinutes,
    decimal PriceAmount,
    string Currency);

public sealed record ServiceResponse(
    Guid Id,
    string Name,
    string? Description,
    int DurationMinutes,
    decimal PriceAmount,
    string Currency,
    bool IsActive,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

// ─── ClinicResource ───

public sealed record CreateResourceRequest(
    string Name,
    int ResourceTypeValue,
    int Capacity,
    string? Description = null,
    Guid? OwnerDentistId = null);

public sealed record UpdateResourceRequest(
    string Name,
    string? Description);

public sealed record ResourceResponse(
    Guid Id,
    string Name,
    string? Description,
    int TypeValue,
    string TypeName,
    int Capacity,
    bool IsActive,
    Guid? OwnerDentistId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

// ─── DentistService (link) ───

public sealed record LinkDentistServiceRequest(
    Guid DentistId,
    Guid ServiceId,
    int? DurationMinutesOverride = null,
    decimal? PriceAmountOverride = null,
    string? CurrencyOverride = null);

public sealed record DentistServiceResponse(
    Guid Id,
    Guid DentistId,
    Guid ServiceId,
    int? DurationMinutesOverride,
    decimal? PriceAmountOverride,
    string? CurrencyOverride,
    bool IsActive);
