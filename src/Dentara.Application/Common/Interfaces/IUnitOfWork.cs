namespace Dentara.Application.Common.Interfaces;

using System.Threading;

/// <summary>
/// Unit of Work abstraction. The Application layer uses this to commit
/// aggregate state changes within a transaction. The Infrastructure layer
/// implements this with EF Core's DbContext + IDatabaseTransaction.
///
/// CRITICAL: For the booking transaction (ADR-003), the Application layer
/// uses a separate IBookingTransactionService that wraps the entire
/// check+allocate+reserve operation in a SQL Server transaction with
/// UPDLOCK+HOLDLOCK. IUnitOfWork is for simpler multi-aggregate commits
/// that don't require the strict concurrency control.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Saves all changes to the underlying store. Returns the number of
    /// state entries written.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a transaction. Useful for Application services that need to
    /// coordinate multiple saves with side effects (e.g. enqueue domain events
    /// only after the save commits).
    /// </summary>
    Task<IAsyncTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an active DB transaction. Disposing commits if not already
/// committed/rolled back.
/// </summary>
public interface IAsyncTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
