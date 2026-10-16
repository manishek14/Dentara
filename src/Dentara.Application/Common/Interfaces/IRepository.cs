namespace Dentara.Application.Common.Interfaces;

using Dentara.Domain.Specifications;

/// <summary>
/// Generic repository abstraction for aggregate roots. The Application layer
/// uses this to load and persist aggregates without depending on EF Core.
///
/// Note: For the booking transaction (ADR-003), the Application layer uses
/// IBookingTransactionService directly because the locking strategy
/// (UPDLOCK+HOLDLOCK) requires raw SQL or carefully crafted EF Core queries
/// that don't fit the generic repository pattern.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> FindAsync(ISpecification<T> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Remove(T entity);
}

/// <summary>
/// Marker interface for aggregates that support optimistic concurrency via RowVersion.
/// </summary>
public interface IConcurrencyAware
{
    byte[] RowVersion { get; }
}
