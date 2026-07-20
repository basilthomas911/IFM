using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public record FuturesTickDataEventParameters
{
    public IMarketDataApi MarketDataApi { get; init; }
    public IBlackboardService BlackboardService { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public FuturesTickDataEventParameters(
        IMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        BlackboardService = blackboardService;
        MarketDataApi = marketDataApi;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }

}
