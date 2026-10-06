using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class StartFuturesRsiSignal
{
    /// <summary>Computes and validates the Futures RSI Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <param name="calendar">The market-session calendar used by the existing historical seed validation policy; optional when no historical seed requires it.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this StartFuturesRsiSignalCommand command,
        FuturesRsiSignalCommandState state, IMarketSessionCalendar? calendar = null)
    {
        var errorMsg = "unable to apply RSI initialization events";
        var updated = command.Compute(state.FuturesRsiCheckpoint, state.FuturesRsiSignals, calendar,
            out var futuresRsiInitialization) switch
        {
            _ when !futuresRsiInitialization.Accepted
                => command.UpdateFailed(ref errorMsg, futuresRsiInitialization.RejectionReason!),
            _ => state.Update(command.CreateFuturesRsiInitializationEvents(futuresRsiInitialization), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures RSI Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="currentFuturesRsiCheckpoint">The previously accepted current futures rsi checkpoint used only as computation input.</param>
    /// <param name="previousFuturesRsiSignals">The previously accepted previous futures rsi signals used only as computation input.</param>
    /// <param name="calendar">The market-session calendar used by the existing historical seed validation policy; optional when no historical seed requires it.</param>
    /// <param name="futuresRsiInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this StartFuturesRsiSignalCommand command,
        FuturesRsiAccumulatorCheckpoint? currentFuturesRsiCheckpoint,
        IReadOnlyCollection<FuturesRsiSignalReadModel> previousFuturesRsiSignals,
        IMarketSessionCalendar? calendar, out FuturesRsiInitialization futuresRsiInitialization)
    {
        var historicalSeed = FuturesRsiHistoricalSeedModel.Decide(command, currentFuturesRsiCheckpoint, calendar, DateTimeOffset.UtcNow);
        string? rejectionReason = command.ForceHistoricalInitialization && historicalSeed.Observations.Length == 0
            ? $"RSI four-hour initialization rejected: {historicalSeed.Reason}." : null;
        var futuresRsiCheckpoint = command.ForceHistoricalInitialization ? null : currentFuturesRsiCheckpoint;
        FuturesRsiSignalReadModel? futuresRsiSignal = null;
        var historicalWarmSignals = new List<FuturesRsiSignalReadModel>();
        foreach (var observation in historicalSeed.Observations)
        {
            var futuresRsiWilderResult = FuturesRsiWilderAccumulator.Apply(futuresRsiCheckpoint, observation, command.EntityId.PeriodLength);
            futuresRsiCheckpoint = futuresRsiWilderResult.Checkpoint;
            futuresRsiSignal = FuturesRsiWilderSignalFactory.Create(observation, command.EntityId.PeriodLength, futuresRsiWilderResult);
            if (futuresRsiSignal is { IsWarm: true, Metadata.IsValid: true }) historicalWarmSignals.Add(futuresRsiSignal);
        }
        var restoredSignal = command.ForceHistoricalInitialization || currentFuturesRsiCheckpoint is null
            ? null : previousFuturesRsiSignals.LastOrDefault();
        var warmHistory = historicalWarmSignals.TakeLast(FuturesTdiConfiguration.Standard.RequiredRsiSamples).SkipLast(1).ToArray();
        var acceptedHistory = command.ForceHistoricalInitialization
            ? new List<FuturesRsiSignalReadModel>() : previousFuturesRsiSignals.ToList();
        if (acceptedHistory.Count == 0) acceptedHistory.AddRange(warmHistory);
        if (acceptedHistory.Count == 0 && restoredSignal is not null) acceptedHistory.Add(restoredSignal);
        if (futuresRsiSignal is not null) acceptedHistory.Add(futuresRsiSignal);
        FuturesRsiSignalReadModel[]? futuresRsiSignals = null;
        if (command.ForceHistoricalInitialization)
        {
            if (futuresRsiSignal is { IsWarm: true } && acceptedHistory.Count >= FuturesTdiConfiguration.Standard.RequiredRsiSamples)
                futuresRsiSignals = acceptedHistory.TakeLast(FuturesTdiConfiguration.Standard.RequiredRsiSamples).ToArray();
            else rejectionReason ??= "RSI four-hour initialization did not provide a warm TDI window.";
        }
        futuresRsiInitialization = new(restoredSignal,
            command.ForceHistoricalInitialization ? null : currentFuturesRsiCheckpoint,
            command.ForceHistoricalInitialization, historicalSeed.Observations.Length, historicalSeed.Reason,
            warmHistory, futuresRsiSignal, futuresRsiCheckpoint, futuresRsiSignals, rejectionReason);
        return futuresRsiInitialization.Accepted;
    }

    /// <summary>Creates the Futures RSI Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresRsiInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static IReadOnlyList<IEvent> CreateFuturesRsiInitializationEvents(this StartFuturesRsiSignalCommand command,
        FuturesRsiInitialization futuresRsiInitialization)
    {
        var events = new List<IEvent> { command.CreateFuturesRsiSignalStartedEvent(futuresRsiInitialization) };
        if (futuresRsiInitialization.FuturesRsiSignal is { } futuresRsiSignal)
        {
            // Only publish the final seed result, preserving the existing cache ordering semantics.
            events.Add(new FuturesRsiSignalGeneratedEvent
            {
                CommandId = command.CommandId,
                Subject = new(ActorType.Event, FuturesRsiSignalGeneratedEvent.Actor, FuturesRsiSignalGeneratedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                FuturesRsiSignal = futuresRsiSignal,
                FuturesRsiCheckpoint = futuresRsiInitialization.FuturesRsiCheckpoint,
                CreatedBy = command.OriginatedBy,
                CreatedOn = command.OriginatedOn
            });
            if (futuresRsiInitialization.FuturesRsiSignals is { } futuresRsiSignals)
                events.Add(new FuturesRsiSignalsGeneratedEvent
                {
                    CommandId = command.CommandId,
                    Subject = new(ActorType.Event, FuturesRsiSignalsGeneratedEvent.Actor, FuturesRsiSignalsGeneratedEvent.Verb, command.EntityId.Format()),
                    EntityId = command.EntityId,
                    FuturesRsiSignalsId = new(futuresRsiSignal.ContractId, futuresRsiSignal.ValueDate, futuresRsiSignal.Timestamp),
                    FuturesRsiSignals = futuresRsiSignals,
                    PeriodLength = command.EntityId.PeriodLength,
                    CreatedBy = command.OriginatedBy,
                    CreatedOn = command.OriginatedOn
                });
        }
        return events;
    }

    /// <summary>Creates the Futures RSI Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresRsiInitialization">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesRsiSignalStartedEvent CreateFuturesRsiSignalStartedEvent(this StartFuturesRsiSignalCommand command,
        FuturesRsiInitialization futuresRsiInitialization) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, FuturesRsiSignalStartedEvent.Actor, FuturesRsiSignalStartedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        ValueDate = command.EntityId.ValueDate,
        StartedOn = command.OriginatedOn,
        StartedBy = command.OriginatedBy,
        HistoricalSeedCount = futuresRsiInitialization.HistoricalSeedCount,
        HistoricalSeedReason = futuresRsiInitialization.HistoricalSeedReason,
        RestoredSignal = futuresRsiInitialization.RestoredSignal,
        RestoredCheckpoint = futuresRsiInitialization.RestoredCheckpoint,
        ResetForHistoricalSeed = futuresRsiInitialization.ResetForHistoricalSeed,
        HistoricalWarmSignals = futuresRsiInitialization.HistoricalWarmSignals
    };
}
