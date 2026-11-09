using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;

using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Domain.MarketData.Feed.Event;

public record MarketDataFeedEventParameters
{
    public TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi MarketDataLifecycle { get; init; }
    public IOptionTradeLiveFeedMap OptionTradeLiveFeedMap { get; init; }

    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public MarketDataFeedEventParameters(
        TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi marketDataLifecycle,
        IOptionTradeLiveFeedMap optionTradeLiveFeedMap,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        MarketDataLifecycle = marketDataLifecycle;
        OptionTradeLiveFeedMap = optionTradeLiveFeedMap;

        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }

}
