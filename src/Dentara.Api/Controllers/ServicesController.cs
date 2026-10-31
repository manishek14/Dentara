namespace Dentara.Api.Controllers;

using Dentara.Api.Authorization;
using Dentara.Api.Contracts.Catalog;
using Dentara.Api.Contracts.Common;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Catalog.Commands;
using Dentara.Application.Catalog.Queries;
using Dentara.Application.Common.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Catalog endpoints: manage services (CRUD + pricing + deactivate).
///
/// Permission mapping:
///   service.manage  → Admin (create, update, deactivate)
///   pricing.manage  → Admin (change price/duration)
/// </summary>
[ApiController]
[Route("api/services")]
[Authorize]
public sealed class ServicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(IMediator mediator, ILogger<ServicesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    // ─── Queries (read) ───

    /// <summary>
    /// Lists all services, optionally filtered by active status.
    /// Available to any authenticated user (patients need to see available services).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ServiceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var query = new GetServicesQuery(isActive, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(r => r.Select(s => new ServiceResponse(
            s.Id, s.Name, s.Description, s.DurationMinutes, s.PriceAmount, s.Currency,
            s.IsActive, s.Version, s.CreatedAtUtc, s.UpdatedAtUtc)).ToList());
    }

    /// <summary>
    /// Gets a single service by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetServiceByIdQuery(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(s => new ServiceResponse(
            s.Id, s.Name, s.Description, s.DurationMinutes, s.PriceAmount, s.Currency,
            s.IsActive, s.Version, s.CreatedAtUtc, s.UpdatedAtUtc));
    }

    // ─── Commands (write) ───

    /// <summary>
    /// Creates a new service.
    /// Permission: service.manage (Admin).
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.ServiceManage)]
    [ProducesResponseType(typeof(ServiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.DurationMinutes <= 0 || request.PriceAmount < 0)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "Name, positive DurationMinutes, and non-negative PriceAmount are required."));

        var command = new CreateServiceCommand(
            request.Name, request.DurationMinutes, request.PriceAmount,
            request.Currency, request.Description, HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new ServiceResponse(
            r.Id, request.Name, request.Description, request.DurationMinutes,
            request.PriceAmount, request.Currency, true, r.Version,
            DateTimeOffset.UtcNow, null));
    }

    /// <summary>
    /// Updates service metadata (name, description). Does NOT change price/duration.
    /// Permission: service.manage (Admin).
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ServiceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Name is required."));

        var command = new UpdateServiceCommand(id, request.Name, request.Description, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates service pricing (duration, price, currency). Bumps the Version
    /// field so existing appointments keep their snapshot (ADR-007).
    /// Permission: pricing.manage (Admin).
    /// </summary>
    [HttpPut("{id:guid}/pricing")]
    [HasPermission(Permissions.PricingManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePricing(
        [FromRoute] Guid id,
        [FromBody] UpdateServicePricingRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DurationMinutes <= 0 || request.PriceAmount < 0)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "Positive DurationMinutes and non-negative PriceAmount are required."));

        var command = new UpdateServicePricingCommand(
            id, request.DurationMinutes, request.PriceAmount, request.Currency,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deactivates a service (soft delete). Existing appointments keep their
    /// snapshot; new bookings cannot use this service.
    /// Permission: service.manage (Admin).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(Permissions.ServiceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateServiceCommand(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Links a service to a dentist (optionally with per-dentist overrides).
    /// Permission: service.manage (Admin).
    /// </summary>
    [HttpPost("link-dentist")]
    [HasPermission(Permissions.ServiceManage)]
    [ProducesResponseType(typeof(DentistServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LinkDentist(
        [FromBody] LinkDentistServiceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DentistId == Guid.Empty || request.ServiceId == Guid.Empty)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "DentistId and ServiceId are required."));

        var command = new LinkDentistServiceCommand(
            request.DentistId, request.ServiceId,
            request.DurationMinutesOverride, request.PriceAmountOverride,
            request.CurrencyOverride, HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new DentistServiceResponse(
            r.Id, request.DentistId, request.ServiceId,
            request.DurationMinutesOverride, request.PriceAmountOverride,
            request.CurrencyOverride, true));
    }

    /// <summary>
    /// Unlinks (deactivates) a service from a dentist.
    /// Permission: service.manage (Admin).
    /// </summary>
    [HttpPost("unlink-dentist")]
    [HasPermission(Permissions.ServiceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkDentist(
        [FromBody] LinkDentistServiceRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UnlinkDentistServiceCommand(
            request.DentistId, request.ServiceId, HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}

/// <summary>
/// Clinic resources endpoints: manage dental units, equipment, rooms.
///
/// Permission mapping:
///   resource.manage → Admin (create, update, deactivate)
/// </summary>
[ApiController]
[Route("api/resources")]
[Authorize]
public sealed class ResourcesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(IMediator mediator, ILogger<ResourcesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Lists all resources, optionally filtered by active status and type.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ResourceResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive,
        [FromQuery] int? typeValue,
        CancellationToken cancellationToken)
    {
        var query = new GetResourcesQuery(isActive, typeValue, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(r => r.Select(res => new ResourceResponse(
            res.Id, res.Name, res.Description, res.TypeValue, res.TypeName,
            res.Capacity, res.IsActive, res.OwnerDentistId,
            res.CreatedAtUtc, res.UpdatedAtUtc)).ToList());
    }

    /// <summary>
    /// Gets a single resource by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ResourceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetResourceByIdQuery(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);
        return result.ToActionResult(r => new ResourceResponse(
            r.Id, r.Name, r.Description, r.TypeValue, r.TypeName,
            r.Capacity, r.IsActive, r.OwnerDentistId,
            r.CreatedAtUtc, r.UpdatedAtUtc));
    }

    /// <summary>
    /// Creates a new clinic resource.
    /// Permission: resource.manage (Admin).
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.ResourceManage)]
    [ProducesResponseType(typeof(ResourceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateResourceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Capacity < 1)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "Name and positive Capacity are required."));

        var command = new CreateResourceCommand(
            request.Name, request.ResourceTypeValue, request.Capacity,
            request.Description, request.OwnerDentistId, HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        var typeName = Dentara.Domain.Catalog.ResourceType.FromValue(request.ResourceTypeValue).Name;
        return result.ToActionResult(r => new ResourceResponse(
            r.Id, request.Name, request.Description, request.ResourceTypeValue,
            typeName, request.Capacity, true, request.OwnerDentistId,
            DateTimeOffset.UtcNow, null));
    }

    /// <summary>
    /// Updates resource metadata (name, description).
    /// Permission: resource.manage (Admin).
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.ResourceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateResourceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR", "Name is required."));

        var command = new UpdateResourceCommand(id, request.Name, request.Description, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deactivates a resource (soft delete).
    /// Permission: resource.manage (Admin).
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    [HasPermission(Permissions.ResourceManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateResourceCommand(id, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }
}
