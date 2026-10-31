namespace Dentara.Infrastructure.Catalog;

using Dentara.Application.Catalog.Queries;
using Dentara.Application.Common.Results;
using Dentara.Domain.Catalog;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class CatalogQueryHandlers
    : IRequestHandler<GetServicesQuery, Result<IReadOnlyList<ServiceDto>>>,
      IRequestHandler<GetServiceByIdQuery, Result<ServiceDto>>,
      IRequestHandler<GetResourcesQuery, Result<IReadOnlyList<ResourceDto>>>,
      IRequestHandler<GetResourceByIdQuery, Result<ResourceDto>>
{
    private readonly DentaraDbContext _db;

    public CatalogQueryHandlers(DentaraDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<ServiceDto>>> Handle(
        GetServicesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _db.Services.AsQueryable();
            if (request.IsActive.HasValue)
                query = query.Where(s => s.IsActive == request.IsActive.Value);

            var services = await query.ToListAsync(cancellationToken);
            var dtos = services.Select(MapService).ToList();
            return Result<IReadOnlyList<ServiceDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IReadOnlyList<ServiceDto>>.FromException(ex); }
    }

    public async Task<Result<ServiceDto>> Handle(
        GetServiceByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var service = await _db.Services.FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken);
            if (service is null)
                return Result<ServiceDto>.Failure("NOTFOUND_SERVICE", $"Service {request.ServiceId} not found.");
            return Result<ServiceDto>.Success(MapService(service));
        }
        catch (Exception ex) { return Result<ServiceDto>.FromException(ex); }
    }

    public async Task<Result<IReadOnlyList<ResourceDto>>> Handle(
        GetResourcesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var query = _db.ClinicResources.AsQueryable();
            if (request.IsActive.HasValue)
                query = query.Where(r => r.IsActive == request.IsActive.Value);
            if (request.TypeValue.HasValue)
                query = query.Where(r => r.TypeValue == request.TypeValue.Value);

            var resources = await query.ToListAsync(cancellationToken);
            var dtos = resources.Select(MapResource).ToList();
            return Result<IReadOnlyList<ResourceDto>>.Success(dtos);
        }
        catch (Exception ex) { return Result<IReadOnlyList<ResourceDto>>.FromException(ex); }
    }

    public async Task<Result<ResourceDto>> Handle(
        GetResourceByIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var resource = await _db.ClinicResources.FirstOrDefaultAsync(r => r.Id == request.ResourceId, cancellationToken);
            if (resource is null)
                return Result<ResourceDto>.Failure("NOTFOUND_RESOURCE", $"Resource {request.ResourceId} not found.");
            return Result<ResourceDto>.Success(MapResource(resource));
        }
        catch (Exception ex) { return Result<ResourceDto>.FromException(ex); }
    }

    private static ServiceDto MapService(Service s) => new(
        s.Id, s.Name, s.Description, s.DurationMinutes, s.PriceAmount, s.Currency,
        s.IsActive, s.Version, s.CreatedAtUtc, s.UpdatedAtUtc);

    private static ResourceDto MapResource(ClinicResource r) => new(
        r.Id, r.Name, r.Description, r.TypeValue, r.Type.Name, r.Capacity,
        r.IsActive, r.OwnerDentistId, r.CreatedAtUtc, r.UpdatedAtUtc);
}
