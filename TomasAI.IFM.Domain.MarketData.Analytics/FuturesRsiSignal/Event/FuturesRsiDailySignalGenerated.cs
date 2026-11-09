using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;

/// <summary>Acknowledges the signal already persisted and published by its durable projector.</summary>
public static class FuturesRsiDailySignalGenerated
{
    extension(FuturesRsiDailySignalGeneratedEvent source)
    {
        /// <summary>Accepts the notification without creating a second cache copy.</summary>
        public ValueTask<bool> ExecuteAsync(IFuturesRsiSignalEventContext context, ILogger<FuturesRsiSignalEventActor> logger)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(logger);
            return ValueTask.FromResult(true);
        }
    }
}
