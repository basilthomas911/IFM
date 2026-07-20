using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Trade.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public record MarketDataFeedEventParameters
{
    public IMarketDataApi MarketDataApi { get; init; }
    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; init; }
    public IBlackboardService BlackboardService { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public MarketDataFeedEventParameters(
        IMarketDataApi marketDataApi,
        IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
        IBlackboardService blackboardService,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        MarketDataApi = marketDataApi;
        OptionTradeLiveFeedMap = optionTradeLiveFeedMap;
        BlackboardService = blackboardService;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }

}
