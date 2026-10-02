using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;

/// <summary>Bridges a durable intraday RSI window into the Traders Dynamic Index command workflow.</summary>
public static class FuturesRsiSignalsGenerated
{
    static readonly Guid TdiCommandNamespace = new("2d1fa1ea-f02a-4790-bc5b-3ae581dedb41");

    /// <summary>
    /// Validates and bounds the RSI window, then sends one deterministic TDI command.
    /// Non-standard RSI configurations and non-intraday periods are intentionally ignored.
    /// </summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiSignalsGeneratedEvent e,
        IEventActorContext<FuturesTdiSignalEventActor> context,
        ILogger<FuturesTdiSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(context);
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

        await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesTdiSignalAsync(context,
            signalId,
            signals,
            latest.TimePeriod,
            configuration,
            DerivedCommandId(e, signalId)).ConfigureAwait(false);
        return true;
    }

    static Guid DerivedCommandId(FuturesRsiSignalsGeneratedEvent source, FuturesTdiSignalId signalId)
    {
        var sourceId = source.Id == Guid.Empty ? source.CommandId : source.Id;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{TdiCommandNamespace:N}:{sourceId:N}:{signalId.Format()}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}
