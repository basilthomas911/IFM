using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Native trade materialization using the caller-owned source-event transaction. Never commits independently.</summary>
public interface IEnlistedTradeDbWriter
{
    /// <summary>Projects the complete strategy workflow read unit inside its source transaction.</summary>
    Task ProjectWorkflowAsync(IEnlistedPostgresTransaction transaction, TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default);
    Task<int> ProjectOrderAsync(IEnlistedPostgresTransaction transaction,TradeOrderDefinition order,DateTime acceptedAtUtc,CancellationToken token=default);
    Task ProjectMonitoringLimitsAsync(IEnlistedPostgresTransaction transaction,TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel limit,
        IReadOnlyList<TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeTypeLimitReadModel> spreads,CancellationToken token=default);
    Task ProjectExecutionAsync(IEnlistedPostgresTransaction transaction,OrderExecutionDefinition execution,CancellationToken token=default);
    Task ProjectTradeAsync(IEnlistedPostgresTransaction transaction,EstablishedTradeDefinition trade,CancellationToken token=default);
    Task ProjectPositionAsync(IEnlistedPostgresTransaction transaction,StrategyPositionSnapshot position,CancellationToken token=default);
}
