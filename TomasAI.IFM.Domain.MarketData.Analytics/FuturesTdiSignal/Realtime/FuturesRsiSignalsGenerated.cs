using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

/// <summary>Computes TDI from each eligible realtime RSI-13 window.</summary>
public static class FuturesRsiSignalsGenerated
{
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesRsiSignalsGeneratedEvent source,
        IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector,
        IMarketDataDbReadContext marketDataDb,
        FuturesTdiSignalRealtimeState state,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(projector);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(state);
        var configuration = FuturesTdiConfiguration.Standard;
        if (source.PeriodLength != configuration.RsiPeriod
            || !FuturesTdiConfiguration.IsSupportedIntraday(source.EntityId.TimePeriod))
            return true;

        var signals = source.FuturesRsiSignals
            .Where(signal =>
                StringComparer.Ordinal.Equals(signal.ContractId, source.EntityId.ContractId)
                // TDI is a rolling RSI window: prior-session seed observations remain required after rollover.
                && signal.ValueDate <= source.EntityId.ValueDate
                && signal.TimePeriod == source.EntityId.TimePeriod
                && signal.PeriodLength == configuration.RsiPeriod
                && signal.IsWarm
                && signal.RSI >= 0d
                && signal.Metadata is not { IsValid: false })
            .OrderBy(FuturesTdiSignalCompute.SampleTime)
            .TakeLast(configuration.RequiredRsiSamples)
            .ToArray();
        if (signals.Length < configuration.RequiredRsiSamples)
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write43(logger, null, nameof(ExecuteAsync), source.EntityId.ContractId, source.EntityId.ValueDate, source.EntityId.TimePeriod, signals.Length, configuration.RequiredRsiSamples);
            return true;
        }

        await state.SeedAsync(source, configuration, marketDataDb).ConfigureAwait(false);
        if (!state.TryEvaluate(source, signals, configuration, out var evaluation))
        {
            global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write44(logger, null, source.EntityId.ContractId);
            return true;
        }
        if (!await projector.ProcessRealtimeEventAsync(evaluation.Generated).ConfigureAwait(false))
            return false;
        state.Confirm(evaluation);
        global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write45(logger, null, source.EntityId.TimePeriod, source.EntityId.ContractId);
        return true;
    }
}
