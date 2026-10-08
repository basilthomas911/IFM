using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public record FuturesOptionTickDataEventParameters
{
    /// <summary>Gets supported supervised individual-option ownership.</summary>
    public Actor.QualifiedIndividualOptionFeeds? QualifiedFeeds { get; init; }
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }
    internal ActiveTickerStreamRegistry<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel> Streams { get; } = new();

    public FuturesOptionTickDataEventParameters(
        ApplicationMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        MarketDataApi = marketDataApi;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
