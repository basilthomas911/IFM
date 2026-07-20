using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Event;

public record FuturesOptionQuoteDataEventParameters
{
    public IMarketDataSnapshotApi MarketDataSnapshotApi { get; init; }
    public IBlackboardService BlackboardService { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public FuturesOptionQuoteDataEventParameters(
        IMarketDataSnapshotApi marketDataSnapshotApi,
        IBlackboardService blackboardService, 
        IStatusConsoleWriter statusConsoleWriter, 
        ILogger logger)
    {
        MarketDataSnapshotApi = marketDataSnapshotApi;
        BlackboardService = blackboardService;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
