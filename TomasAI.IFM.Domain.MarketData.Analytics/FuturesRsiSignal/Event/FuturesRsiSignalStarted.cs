using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Provides the FuturesRsiSignalStarted implementation.</summary>
public static class FuturesRsiSignalStarted
{
    static FuturesRsiSignalStarted()
    {
        _serviceId = $"{LogSourceType.FuturesRsiSignalEvent}";
    }

    static string _serviceId { get; } = default!;

    /// <summary>
    /// Executes the start operation for a Futures RSI signal timer, initiating the generation of RSI signals for the specified contract and value date.
    /// </summary>
    /// <param name="e">The event containing details about the futures RSI signal to be processed, including the contract ID and value date.</param>
    /// <param name="context">The event actor context used to dispatch queries and commands.</param>
    /// <param name="statusConsoleWriter">The status console writer used to log messages and errors to the console.</param>
    /// <param name="logger">The logger used to log errors and informational messages related to the execution of the event handler.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiSignalStartedEvent e,
        IEventActorContext context,
        IEventActorContext commandApi,
        IMarketDataApi marketDataApi,
        IStatusConsoleWriter statusConsoleWriter,
        ILogger logger)
    {
        var source = $"FuturesRsiSignalStartedEvent for ContractId: {e.EntityId.ContractId}, TimePeriod: {e.EntityId.TimePeriod}, PeriodLength: {e.EntityId.PeriodLength}";
        ArgumentNullException.ThrowIfNull(marketDataApi);
        try
        {
            FuturesAnalyticsObservationAttachmentRegistry<FuturesRsiSignalEntityId>.Attach(e.EntityId);
            return true;
        }
        catch (Exception ex)
        {
            await statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesRsiSignalEvent, FuturesRsiSignalStartedEvent.ErrorCode, ex.GetErrorMessage());
            logger.LogErrorEvent(_serviceId, ex.GetErrorMessage(), "{Source}:  {ContractId} handler failed", source, e.EntityId.ContractId);
        }
        return false;

    }

}
