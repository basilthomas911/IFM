using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public record FuturesTickDataEventParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public IBlackboardService BlackboardService { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }
    internal ActiveTickerStreamRegistry<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV2ReadModel> Streams { get; } = new();

    public FuturesTickDataEventParameters(
        ApplicationMarketDataApi marketDataApi,
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
