using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;

public class FuturesTickDataEventState(
    IMarketDataApi marketDataApi,
    IMarketDataSnapshotApi marketDataSnapshotApi,
    IBlackboardService blackboardService,
    IStatusConsoleWriter statusConsoleWriter,
    ILogger<FuturesTickDataEventState> logger)
    : BaseEventActorState<FuturesTickDataEventState>
{
    public IMarketDataApi MarketDataApi { get; } = IsArgumentNull.Set(marketDataApi);
    public IMarketDataSnapshotApi MarketDataSnapshotApi { get; } = IsArgumentNull.Set(marketDataSnapshotApi);
    public IBlackboardService BlackboardService { get; } = IsArgumentNull.Set(blackboardService);
    public IStatusConsoleWriter StatusConsoleWriter { get; } = IsArgumentNull.Set(statusConsoleWriter);
    public ILogger<FuturesTickDataEventState> Logger { get; } = IsArgumentNull.Set(logger);

    /// <summary>
    /// The actor thread identifier for this state instance.
    /// </summary>
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// Fluent accessor returning this instance.
    /// </summary>
    public FuturesTickDataEventState As => this;
}
