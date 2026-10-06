using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Computes and applies futures ADX signal events through command-owned state.</summary>
public static class GenerateFuturesAdxSignal
{
    /// <summary>Computes and validates the Futures ADX Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
    {
        if (command.Observation is { } observation
            && state.AdxSignals.LastOrDefault()?.Metadata?.MarketDataAsOfUtc >= observation.LastMarketEventUtc)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        if (command.Observation is { } observationDuplicate
            && state.AdxSignals.LastOrDefault()?.Metadata?.ObservationId == observationDuplicate.ObservationId)
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        var errorMsg = "unable to apply generated ADX signal event";
        var updated = command.Compute(state.AdxSignal, state.AdxSignals, out var futuresAdxSignalCompute) switch
        {
            _ when command.EntityId.PeriodLength <= 0
                => command.UpdateFailed(ref errorMsg, "ADX period length must be positive"),
            _ when !futuresAdxSignalCompute.IsValid
                => command.UpdateFailed(ref errorMsg, "computed ADX signal contains invalid directional values"),
            _ => state.Update(command.CreateFuturesAdxSignalGeneratedEvent(futuresAdxSignalCompute.SignalDirection, futuresAdxSignalCompute), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures ADX Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="adxSignal">The adx signal business data used by this operation.</param>
    /// <param name="adxSignals">The ordered accepted signal history or computed publication window.</param>
    /// <param name="futuresAdxSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this GenerateFuturesAdxSignalCommand command, FuturesAdxSignalReadModel? adxSignal, IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, out FuturesAdxSignalCompute futuresAdxSignalCompute)
        => FuturesAdxSignalCompute.Create(command.EntityId.PeriodLength, adxSignal, adxSignals, out futuresAdxSignalCompute);

    /// <summary>Creates the Futures ADX Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="trendDirection">The computed business trend direction to record on the signal event.</param>
    /// <param name="futuresAdxSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAdxSignalGeneratedEvent CreateFuturesAdxSignalGeneratedEvent(this GenerateFuturesAdxSignalCommand command, FuturesTrendDirectionType trendDirection, FuturesAdxSignalCompute futuresAdxSignalCompute)
    {
        var entityId = new FuturesAdxSignalEntityId(command.FuturesAdxSignalId.ContractId, command.FuturesAdxSignalId.ValueDate, command.EntityId.TimePeriod, command.EntityId.PeriodLength);
        var signalTimestamp = command.Observation?.LastMarketEventUtc.UtcDateTime ?? DateTime.UtcNow;
        var signal = new FuturesAdxSignalReadModel(
            command.EntityId.ContractId,
            command.EntityId.ValueDate,
            command.EntityId.TimePeriod,
            command.EntityId.PeriodLength,
            TimeOnly.FromDateTime(signalTimestamp),
            command.FuturesPrice,
            futuresAdxSignalCompute.PlusDI,
            futuresAdxSignalCompute.MinusDI,
            futuresAdxSignalCompute.AdxValue,
            trendDirection,
            futuresAdxSignalCompute.TrendDirectionStrength())
        {
            IsWarm = futuresAdxSignalCompute.IsWarm,
            Metadata = command.Observation is { } observation
                ? new MarketAnalyticsSignalMetadata
                {
                    SignalKey = new(
                        observation.MarketSeriesIdentity,
                        MarketAnalyticsSignalKind.Adx,
                        observation.TimeFrame,
                        $"adx-{command.EntityId.PeriodLength}-legacy-v1"),
                    ContractId = observation.ContractId,
                    ValueDate = observation.ValueDate,
                    ObservationId = observation.ObservationId,
                    MarketDataAsOfUtc = observation.LastMarketEventUtc,
                    CalculatedAtUtc = DateTimeOffset.UtcNow,
                    SourceSequence = observation.LastSourceSequence,
                    SchemaVersion = 1,
                    CalculationVersion = "adx-legacy-compatible-v1",
                    CalculationMethod = observation.CalculationMethod,
                    IsValid = observation.IsValid,
                    ValidationIssues = observation.ValidationIssues
                }
                : null
        };
        return new FuturesAdxSignalGeneratedEvent
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalGeneratedEvent.Actor, FuturesAdxSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesAdxSignal = signal,
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        };
    }
}
