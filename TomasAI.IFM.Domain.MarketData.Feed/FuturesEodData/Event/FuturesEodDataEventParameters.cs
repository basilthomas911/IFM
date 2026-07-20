using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Event;

public record FuturesEodDataEventParameters
{
    public IBlackboardService BlackboardService { get; init; }
    public IStatusConsoleWriter StatusConsoleWriter { get; init; }
    public ILogger Logger { get; init; }

    public FuturesEodDataEventParameters(
        IBlackboardService blackboardService, IStatusConsoleWriter statusConsoleWriter, ILogger logger)
    {
        BlackboardService = blackboardService;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }
}
