using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
/// <summary>Reads execution-created trade projections from the owning Trade query actor.</summary>
public interface IEstablishedTradeQueryApi
{
    Task<ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot>> GetRealtimeAsync(
        TradeEntityId tradeId, DateOnly valueDate, CancellationToken cancellationToken = default)
        => Task.FromResult(new ServiceResult<TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot>((TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot)null!));
    /// <summary>Loads the exact persisted trade identity and strategy, without command-state access.</summary>
    Task<ServiceResult<EstablishedTradeDefinition>> GetAsync(TradeEntityId tradeId, TradeStrategyKind strategyKind,
        CancellationToken cancellationToken = default);
}
