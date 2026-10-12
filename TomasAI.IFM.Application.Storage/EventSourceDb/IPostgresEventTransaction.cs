













namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Runs business writes and the existing event-store append on one connection/transaction.</summary>
public interface IPostgresEventTransaction
{
    /// <summary>Executes once without replaying the delegate after a confirmed rollback.</summary>
    Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
