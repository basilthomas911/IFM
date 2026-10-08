using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
/// <summary>Reads execution-created trade projections from the owning Trade query actor.</summary>
public interface IEstablishedTradeQueryApi
{
    /// <summary>Loads the exact persisted trade identity and strategy, without command-state access.</summary>
    Task<ServiceResult<EstablishedTradeDefinition>> GetAsync(TradeEntityId tradeId, TradeStrategyKind strategyKind,
        CancellationToken cancellationToken = default);
}
