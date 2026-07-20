using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.Trade.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public record FuturesOptionTickDataEventParameters
{
    public IMarketDataApi MarketDataApi { get; init; }
    public IMarketDataSnapshotApi MarketDataSnapshotApi { get; init; }
    public IBlackboardService BlackboardService { get; init; }

    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter {  get; init; }
    public ILogger Logger { get; init; }

    public FuturesOptionTickDataEventParameters(
        IMarketDataApi marketDataApi,
        IMarketDataSnapshotApi marketDataSnapshotApi,
        IBlackboardService blackboardService,
         IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
         IStatusConsoleWriter statusConsoleWriter,
         ILogger logger)
    {
        MarketDataApi = marketDataApi;
        MarketDataSnapshotApi = marketDataSnapshotApi;
        BlackboardService = blackboardService;
        OptionTradeLiveFeedMap = optionTradeLiveFeedMap;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
