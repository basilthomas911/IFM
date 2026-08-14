using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using ApplicationMarketDataApi = TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event;

public record FuturesOptionTickDataEventParameters
{
    public ApplicationMarketDataApi MarketDataApi { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter {  get; init; }
    public ILogger Logger { get; init; }
    internal ActiveTickerReaderRegistry Readers { get; } = new();

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
