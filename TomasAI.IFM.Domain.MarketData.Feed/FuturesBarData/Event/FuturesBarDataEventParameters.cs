using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Event;

public record FuturesBarDataEventParameters
{
    public IFuturesBarDataTimer FuturesBarDataTimer { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public FuturesBarDataEventParameters(
        IFuturesBarDataTimer futuresBarDataTimer,
        IStatusConsoleWriter statusConsoleWriter,
         ILogger logger)
    {
        FuturesBarDataTimer = futuresBarDataTimer;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
