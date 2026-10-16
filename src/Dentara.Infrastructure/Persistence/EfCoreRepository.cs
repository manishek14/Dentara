namespace Dentara.Infrastructure.Persistence;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Specifications;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Generic repository implementation backed by EF Core. Used for most
/// aggregate persistence. NOT used for the booking transaction — that
/// requires raw SQL with lock hints (see IBookingTransactionService).
/// </summary>
public sealed class EfCoreRepository<T> : IRepository<T> where T : class
{
    private readonly DentaraDbContext _dbContext;
    private readonly DbSet<T> _dbSet;

    public EfCoreRepository(DentaraDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object?[] { id }, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        var query = ApplySpecification(specification);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> specification)
    {
        var query = _dbSet.AsQueryable();
        var expression = specification.ToExpression();
        return query.Where(expression);
    }
}
