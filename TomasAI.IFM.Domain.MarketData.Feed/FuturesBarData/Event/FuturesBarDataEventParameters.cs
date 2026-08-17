using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

public record FuturesBarDataEventParameters
{
    public IFuturesBarDataTimer FuturesBarDataTimer { get; init; }
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public FuturesBarDataEventParameters(
        IFuturesBarDataTimer futuresBarDataTimer,
        ApplicationMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
         ILogger logger)
    {
        FuturesBarDataTimer = futuresBarDataTimer;
        MarketDataApi = marketDataApi;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
