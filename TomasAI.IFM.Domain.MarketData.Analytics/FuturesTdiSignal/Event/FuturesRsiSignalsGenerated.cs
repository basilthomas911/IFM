using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;

/// <summary>Bridges a durable intraday RSI window into the Traders Dynamic Index command workflow.</summary>
public static class FuturesRsiSignalsGenerated
{
    /// <summary>
    /// Validates and bounds the RSI window, then sends one deterministic TDI command.
    /// Non-standard RSI configurations and non-intraday periods are intentionally ignored.
    /// </summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiSignalsGeneratedEvent e,
        IEventActorContext context,
        IEventActorContext commandApi,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(commandApi);
        ArgumentNullException.ThrowIfNull(logger);

        var configuration = FuturesTdiConfiguration.Standard;
        if (e.PeriodLength != configuration.RsiPeriod
            || !FuturesTdiConfiguration.IsSupportedIntraday(e.EntityId.TimePeriod))
            return true;

        var signals = e.FuturesRsiSignals
            .Where(signal =>
                StringComparer.Ordinal.Equals(signal.ContractId, e.EntityId.ContractId)
                && signal.ValueDate == e.EntityId.ValueDate
                && signal.TimePeriod == e.EntityId.TimePeriod
                && signal.PeriodLength == configuration.RsiPeriod
                && signal.RSI >= 0d)
            .OrderBy(static signal => signal.ValueDate)
            .ThenBy(static signal => signal.Timestamp)
            .TakeLast(configuration.RequiredRsiSamples)
            .ToArray();

        if (signals.Length < configuration.RequiredRsiSamples)
            return true;

        var latest = signals[^1];
        var signalId = new FuturesTdiSignalId(
            latest.ContractId,
            latest.ValueDate,
            latest.TimePeriod,
            latest.Timestamp,
            configuration.ConfigurationId);

        await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesTdiSignalAsync(commandApi,
            signalId,
            signals,
            latest.TimePeriod,
            configuration,
            e.Id == Guid.Empty ? e.CommandId : e.Id).ConfigureAwait(false);
        return true;
    }
}
