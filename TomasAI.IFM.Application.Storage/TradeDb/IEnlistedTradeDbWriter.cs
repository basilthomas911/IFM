using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.TradeDb;
/// <summary>Native trade materialization using the caller-owned source-event transaction. Never commits independently.</summary>
public interface IEnlistedTradeDbWriter
{
    /// <summary>Projects the complete strategy workflow read unit inside its source transaction.</summary>
    Task ProjectWorkflowAsync(IEnlistedPostgresTransaction transaction, TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default);
    /// <summary>Project Order through the typed trade persistence boundary.</summary>
    Task<int> ProjectOrderAsync(IEnlistedPostgresTransaction transaction, TradeOrderDefinition order, DateTime acceptedAtUtc, CancellationToken token = default);
    /// <summary>Project Monitoring Limits through the typed trade persistence boundary.</summary>
    Task ProjectMonitoringLimitsAsync(IEnlistedPostgresTransaction transaction, TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit, IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads, CancellationToken token = default);
    /// <summary>Project Execution through the typed trade persistence boundary.</summary>
    Task ProjectExecutionAsync(IEnlistedPostgresTransaction transaction, OrderExecutionDefinition execution, CancellationToken token = default);
    /// <summary>Project Trade through the typed trade persistence boundary.</summary>
    Task ProjectTradeAsync(IEnlistedPostgresTransaction transaction, EstablishedTradeDefinition trade, CancellationToken token = default);
    /// <summary>Project Position through the typed trade persistence boundary.</summary>
    Task ProjectPositionAsync(IEnlistedPostgresTransaction transaction, StrategyPositionSnapshot position, CancellationToken token = default);
}