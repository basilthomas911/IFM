using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

public record FuturesEodDataEventParameters
{

    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public FuturesEodDataEventParameters(
        IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {

        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
