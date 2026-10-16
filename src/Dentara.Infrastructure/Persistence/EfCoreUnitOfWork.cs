namespace Dentara.Infrastructure.Persistence;

using Dentara.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// EF Core implementation of IUnitOfWork. Wraps DbContext.SaveChangesAsync
/// and DbContext.Database.BeginTransaction.
///
/// CRITICAL: For the booking transaction (ADR-003), use IBookingTransactionService
/// directly — it requires raw SQL with UPDLOCK+HOLDLOCK that EF Core does not
/// support via DbContext directly. This UnitOfWork is for simpler multi-aggregate
/// commits.
/// </summary>
public sealed class EfCoreUnitOfWork : IUnitOfWork
{
    private readonly DentaraDbContext _dbContext;
    private IDbContextTransaction? _transaction;

    public EfCoreUnitOfWork(DentaraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IAsyncTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfCoreTransaction(_transaction);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _dbContext.Dispose();
    }
}

internal sealed class EfCoreTransaction : IAsyncTransaction
{
    private readonly IDbContextTransaction _transaction;
    private bool _disposed;

    public EfCoreTransaction(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _transaction.DisposeAsync();
        _disposed = true;
    }
}
