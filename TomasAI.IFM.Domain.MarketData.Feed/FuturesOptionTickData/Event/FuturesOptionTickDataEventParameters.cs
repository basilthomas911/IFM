using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public record FuturesOptionTickDataEventParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public IBlackboardService BlackboardService { get; init; }

    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter {  get; init; }
    public ILogger Logger { get; init; }

    public FuturesOptionTickDataEventParameters(
        ApplicationMarketDataApi marketDataApi,
        IBlackboardService blackboardService,
         IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
         IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        MarketDataApi = marketDataApi;
        BlackboardService = blackboardService;
        OptionTradeLiveFeedMap = optionTradeLiveFeedMap;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
