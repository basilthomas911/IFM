using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public record FuturesTickDataEventParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }

    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }
    internal ActiveTickerStreamRegistry<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel> Streams { get; } = new();

    public FuturesTickDataEventParameters(
        ApplicationMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {

        MarketDataApi = marketDataApi;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }

}
