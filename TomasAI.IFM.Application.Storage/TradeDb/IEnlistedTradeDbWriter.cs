using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.TradeDb;
/// <summary>Native trade materialization using the caller-owned source-event transaction. Never commits independently.</summary>
public interface IEnlistedTradeDbWriter
{
    /// <summary>Projects the complete strategy workflow read unit inside its source transaction.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="source">The source supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectWorkflowAsync(IEnlistedPostgresTransaction transaction, TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default);
    /// <summary>Project Order through the typed trade persistence boundary.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="order">The order supplied by the caller.</param>
    /// <param name="acceptedAtUtc">The accepted at utc supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>The database result for the requested operation.</returns>
    Task<int> ProjectOrderAsync(IEnlistedPostgresTransaction transaction, TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default);
    /// <summary>Project Monitoring Limits through the typed trade persistence boundary.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="limit">The maximum number of returned rows.</param>
    /// <param name="spreads">The spreads supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectMonitoringLimitsAsync(IEnlistedPostgresTransaction transaction, TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit, IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads, CancellationToken token = default);
    /// <summary>Project Execution through the typed trade persistence boundary.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="execution">The execution supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectExecutionAsync(IEnlistedPostgresTransaction transaction, OrderExecutionDefinition execution, CancellationToken token = default);
    /// <summary>Project Trade through the typed trade persistence boundary.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="trade">The trade supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectTradeAsync(IEnlistedPostgresTransaction transaction, EstablishedTradeDefinition trade, CancellationToken token = default);
    /// <summary>Replaces one verified daily mark with EOD history, preserving current position and routes.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="position">The position supplied by the caller.</param>
    /// <param name="expectedProjectionSequence">The expected projection sequence supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectPositionHistoryAsync(IEnlistedPostgresTransaction transaction, StrategyPositionSnapshot position, long expectedProjectionSequence, CancellationToken token = default);
    /// <summary>Project Position through the typed trade persistence boundary.</summary>
    /// <param name="transaction">The caller's enlisted PostgreSQL transaction.</param>
    /// <param name="position">The position supplied by the caller.</param>
    /// <param name="token">Cancels the database operation.</param>
    /// <returns>Completion of the database operation.</returns>
    Task ProjectPositionAsync(IEnlistedPostgresTransaction transaction, StrategyPositionSnapshot position, CancellationToken token = default);
}