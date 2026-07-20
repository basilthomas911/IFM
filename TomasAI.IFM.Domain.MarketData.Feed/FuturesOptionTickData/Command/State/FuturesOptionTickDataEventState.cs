using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;

public class FuturesOptionTickDataEventState(
    IMarketDataApi marketDataApi,
    IMarketDataSnapshotApi marketDataSnapshotApi,
    IBlackboardService blackboardService,
    IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesOptionTickDataEventState> logger)
    : BaseEventActorState<FuturesOptionTickDataEventState>
{
    public IMarketDataApi MarketDataApi { get; } = IsArgumentNull.Set(marketDataApi);
    public IMarketDataSnapshotApi MarketDataSnapshotApi { get; } = IsArgumentNull.Set(marketDataSnapshotApi);
    public IBlackboardService BlackboardService { get; } = IsArgumentNull.Set(blackboardService);
    public StreamingRequestIdModel StreamingRequestId { get; } = blackboardService.StreamingRequestId;
    public FuturesTickDataModel FuturesTickData { get; } = blackboardService.FuturesTickData;
    public FuturesOptionTickDataModel FuturesOptionTickPriceData { get; } = blackboardService.FuturesOptionTickPriceData;
    public RiskFreeRateModel RiskFreeRate { get; } = blackboardService.RiskFreeRate;
    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; } = IsArgumentNull.Set(optionTradeLiveFeedMap);
    public IStatusConsoleWriter StatusConsoleWriter { get; } = IsArgumentNull.Set(statusConsoleWriter);
    public ILogger<FuturesOptionTickDataEventState> Logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// The actor thread identifier for this state instance.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Fluent accessor returning this instance.
    /// </summary>
    public FuturesOptionTickDataEventState As => this;

}
