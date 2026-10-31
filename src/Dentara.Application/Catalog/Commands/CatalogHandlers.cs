namespace Dentara.Application.Catalog.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using MediatR;
using System.Text.Json;

// ─── Service Handlers ───

public sealed class CreateServiceCommandHandler
    : IRequestHandler<CreateServiceCommand, Result<ServiceResult>>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<Service> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public CreateServiceCommandHandler(
        IUnitOfWork uow, IRepository<Service> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result<ServiceResult>> Handle(CreateServiceCommand r, CancellationToken ct)
    {
        try
        {
            var service = Service.Create(r.Name, r.DurationMinutes, r.PriceAmount, r.Currency, _clock, r.Description);
            await _repo.AddAsync(service, ct);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("Service", service.Id, "Create",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Service '{r.Name}' created",
                _user.UserId,
                NewValueJson: JsonSerializer.Serialize(new { name = service.Name, duration = service.DurationMinutes, price = service.PriceAmount, currency = service.Currency }),
                CorrelationId: r.CorrelationId), ct);

            return Result<ServiceResult>.Success(new ServiceResult(service.Id, service.Version));
        }
        catch (Exception ex) { return Result<ServiceResult>.FromException(ex); }
    }
}

public sealed class UpdateServiceCommandHandler
    : IRequestHandler<UpdateServiceCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<Service> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public UpdateServiceCommandHandler(
        IUnitOfWork uow, IRepository<Service> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result> Handle(UpdateServiceCommand r, CancellationToken ct)
    {
        try
        {
            var service = await _repo.GetByIdAsync(r.ServiceId, ct);
            if (service is null)
                return Result.Failure("NOTFOUND_SERVICE", $"Service {r.ServiceId} not found.");

            var oldName = service.Name;
            service.UpdateMetadata(r.Name, r.Description, _clock);
            _repo.Update(service);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("Service", service.Id, "UpdateMetadata",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Service name changed from '{oldName}' to '{r.Name}'",
                _user.UserId,
                OldValueJson: JsonSerializer.Serialize(new { name = oldName }),
                NewValueJson: JsonSerializer.Serialize(new { name = service.Name, description = service.Description }),
                CorrelationId: r.CorrelationId), ct);

            return Result.Success();
        }
        catch (Exception ex) { return Result.FromException(ex); }
    }
}

public sealed class UpdateServicePricingCommandHandler
    : IRequestHandler<UpdateServicePricingCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<Service> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public UpdateServicePricingCommandHandler(
        IUnitOfWork uow, IRepository<Service> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result> Handle(UpdateServicePricingCommand r, CancellationToken ct)
    {
        try
        {
            var service = await _repo.GetByIdAsync(r.ServiceId, ct);
            if (service is null)
                return Result.Failure("NOTFOUND_SERVICE", $"Service {r.ServiceId} not found.");

            var oldDuration = service.DurationMinutes;
            var oldPrice = service.PriceAmount;
            var oldCurrency = service.Currency;
            var oldVersion = service.Version;

            service.UpdateBaseTerms(r.DurationMinutes, r.PriceAmount, r.Currency, _clock);
            _repo.Update(service);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("Service", service.Id, "PriceChange",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Pricing updated: {oldPrice} {oldCurrency} / {oldDuration}min → {r.PriceAmount} {r.Currency} / {r.DurationMinutes}min",
                _user.UserId,
                OldValueJson: JsonSerializer.Serialize(new { duration = oldDuration, price = oldPrice, currency = oldCurrency, version = oldVersion }),
                NewValueJson: JsonSerializer.Serialize(new { duration = service.DurationMinutes, price = service.PriceAmount, currency = service.Currency, version = service.Version }),
                CorrelationId: r.CorrelationId), ct);

            return Result.Success();
        }
        catch (Exception ex) { return Result.FromException(ex); }
    }
}

public sealed class DeactivateServiceCommandHandler
    : IRequestHandler<DeactivateServiceCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<Service> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public DeactivateServiceCommandHandler(
        IUnitOfWork uow, IRepository<Service> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result> Handle(DeactivateServiceCommand r, CancellationToken ct)
    {
        try
        {
            var service = await _repo.GetByIdAsync(r.ServiceId, ct);
            if (service is null)
                return Result.Failure("NOTFOUND_SERVICE", $"Service {r.ServiceId} not found.");

            service.Deactivate(_clock);
            _repo.Update(service);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("Service", service.Id, "Deactivate",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Service '{service.Name}' deactivated",
                _user.UserId, CorrelationId: r.CorrelationId), ct);

            return Result.Success();
        }
        catch (Exception ex) { return Result.FromException(ex); }
    }
}

// ─── Resource Handlers ───

public sealed class CreateResourceCommandHandler
    : IRequestHandler<CreateResourceCommand, Result<ResourceResult>>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<ClinicResource> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public CreateResourceCommandHandler(
        IUnitOfWork uow, IRepository<ClinicResource> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result<ResourceResult>> Handle(CreateResourceCommand r, CancellationToken ct)
    {
        try
        {
            var type = ResourceType.FromValue(r.ResourceTypeValue);
            var resource = ClinicResource.Create(r.Name, type, r.Capacity, _clock, r.Description, r.OwnerDentistId);
            await _repo.AddAsync(resource, ct);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("ClinicResource", resource.Id, "Create",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Resource '{r.Name}' ({type.Name}, capacity {r.Capacity}) created",
                _user.UserId,
                NewValueJson: JsonSerializer.Serialize(new { name = resource.Name, type = type.Name, capacity = resource.Capacity, ownerDentistId = resource.OwnerDentistId }),
                CorrelationId: r.CorrelationId), ct);

            return Result<ResourceResult>.Success(new ResourceResult(resource.Id));
        }
        catch (Exception ex) { return Result<ResourceResult>.FromException(ex); }
    }
}

public sealed class UpdateResourceCommandHandler
    : IRequestHandler<UpdateResourceCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<ClinicResource> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public UpdateResourceCommandHandler(
        IUnitOfWork uow, IRepository<ClinicResource> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result> Handle(UpdateResourceCommand r, CancellationToken ct)
    {
        try
        {
            var resource = await _repo.GetByIdAsync(r.ResourceId, ct);
            if (resource is null)
                return Result.Failure("NOTFOUND_RESOURCE", $"Resource {r.ResourceId} not found.");

            var oldName = resource.Name;
            resource.UpdateMetadata(r.Name, r.Description, _clock);
            _repo.Update(resource);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("ClinicResource", resource.Id, "UpdateMetadata",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Resource name changed from '{oldName}' to '{r.Name}'",
                _user.UserId, CorrelationId: r.CorrelationId), ct);

            return Result.Success();
        }
        catch (Exception ex) { return Result.FromException(ex); }
    }
}

public sealed class DeactivateResourceCommandHandler
    : IRequestHandler<DeactivateResourceCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<ClinicResource> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public DeactivateResourceCommandHandler(
        IUnitOfWork uow, IRepository<ClinicResource> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result> Handle(DeactivateResourceCommand r, CancellationToken ct)
    {
        try
        {
            var resource = await _repo.GetByIdAsync(r.ResourceId, ct);
            if (resource is null)
                return Result.Failure("NOTFOUND_RESOURCE", $"Resource {r.ResourceId} not found.");

            resource.Deactivate(_clock);
            _repo.Update(resource);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("ClinicResource", resource.Id, "Deactivate",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Resource '{resource.Name}' deactivated",
                _user.UserId, CorrelationId: r.CorrelationId), ct);

            return Result.Success();
        }
        catch (Exception ex) { return Result.FromException(ex); }
    }
}

// ─── DentistService Link Handlers ───

public sealed class LinkDentistServiceCommandHandler
    : IRequestHandler<LinkDentistServiceCommand, Result<DentistServiceResult>>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DentistService> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public LinkDentistServiceCommandHandler(
        IUnitOfWork uow, IRepository<DentistService> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result<DentistServiceResult>> Handle(LinkDentistServiceCommand r, CancellationToken ct)
    {
        try
        {
            var ds = DentistService.Create(r.DentistId, r.ServiceId, _clock,
                r.DurationMinutesOverride, r.PriceAmountOverride, r.CurrencyOverride);
            await _repo.AddAsync(ds, ct);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("DentistService", ds.Id, "Link",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Linked service {r.ServiceId} to dentist {r.DentistId}",
                _user.UserId, CorrelationId: r.CorrelationId), ct);

            return Result<DentistServiceResult>.Success(new DentistServiceResult(ds.Id));
        }
        catch (Exception ex) { return Result<DentistServiceResult>.FromException(ex); }
    }
}

public sealed class UnlinkDentistServiceCommandHandler
    : IRequestHandler<UnlinkDentistServiceCommand, Result>
{
    private readonly IUnitOfWork _uow;
    private readonly IRepository<DentistService> _repo;
    private readonly IAuditService _audit;
    private readonly ICurrentUserContext _user;
    private readonly IClock _clock;

    public UnlinkDentistServiceCommandHandler(
        IUnitOfWork uow, IRepository<DentistService> repo,
        IAuditService audit, ICurrentUserContext user, IClock clock)
    {
        _uow = uow; _repo = repo; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<Result> Handle(UnlinkDentistServiceCommand r, CancellationToken ct)
    {
        try
        {
            // Find the link by (DentistId, ServiceId) — load all and filter
            // (SQLite can't translate all LINQ expressions reliably)
            var all = await _repo.FindAsync(
                new SimpleSpecification<DentistService>(ds => ds.DentistId == r.DentistId && ds.ServiceId == r.ServiceId),
                ct);

            var ds = all.FirstOrDefault();
            if (ds is null)
                return Result.Failure("NOTFOUND_DENTIST_SERVICE", "DentistService link not found.");

            ds.Deactivate(_clock);
            _repo.Update(ds);
            await _uow.SaveChangesAsync(ct);

            await _audit.LogAsync(new AuditEntry("DentistService", ds.Id, "Unlink",
                _user.UserId.HasValue ? $"User:{_user.UserId}" : "System",
                _clock.UtcNow, $"Unlinked service {r.ServiceId} from dentist {r.DentistId}",
                _user.UserId, CorrelationId: r.CorrelationId), ct);

            return Result.Success();
        }
        catch (Exception ex) { return Result.FromException(ex); }
    }
}

/// <summary>
/// Simple ISpecification implementation for ad-hoc filtering.
/// </summary>
internal sealed class SimpleSpecification<T> : Dentara.Domain.Specifications.Specification<T>
    where T : class
{
    private readonly System.Linq.Expressions.Expression<Func<T, bool>> _expr;

    public SimpleSpecification(System.Linq.Expressions.Expression<Func<T, bool>> expr) => _expr = expr;

    public override bool IsSatisfiedBy(T candidate) => _expr.Compile()(candidate);

    public override System.Linq.Expressions.Expression<Func<T, bool>> ToExpression() => _expr;
}
