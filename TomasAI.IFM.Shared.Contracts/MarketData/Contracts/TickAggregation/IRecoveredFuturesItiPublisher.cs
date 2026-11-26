using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
/// <summary>Original-generation source replay must not resample current causal inputs for ITI.</summary>
public interface IRecoveredFuturesItiPublisher
{
    ValueTask PublishRecoveredAsync(FuturesMarketPriceUpdatedRealtimeEvent source,CancellationToken token);
    ValueTask PublishRecoveredAsync(FuturesTradeReplayBatchRealtimeEvent source,CancellationToken token);
}
