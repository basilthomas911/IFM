using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Computes and applies futures ADX signal events through command-owned state.</summary>
public static class GenerateFuturesAdxDailySignal
{
    /// <summary>Computes and validates the Futures ADX Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesAdxDailySignalCommand command, FuturesAdxSignalCommandState state)
    {
        var errorMsg = "unable to apply generated ADX signal event";
        var updated = command.Compute(state.AdxSignal, state.AdxSignals, out var futuresAdxSignalCompute) switch
        {
            _ when command.EntityId.PeriodLength <= 0
                => command.UpdateFailed(ref errorMsg, "ADX period length must be positive"),
            _ when !futuresAdxSignalCompute.IsValid
                => command.UpdateFailed(ref errorMsg, "computed ADX signal contains invalid directional values"),
            _ => state.Update(command.CreateFuturesAdxDailySignalGeneratedEvent(futuresAdxSignalCompute.SignalDirection, futuresAdxSignalCompute), command)
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
    internal static bool Compute(this GenerateFuturesAdxDailySignalCommand command, FuturesAdxSignalReadModel? adxSignal, IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, out FuturesAdxSignalCompute futuresAdxSignalCompute)
        => FuturesAdxSignalCompute.Create(command.EntityId.PeriodLength, adxSignal, adxSignals, out futuresAdxSignalCompute);

    /// <summary>Creates the Futures ADX Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="trendDirection">The computed business trend direction to record on the signal event.</param>
    /// <param name="futuresAdxSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesAdxDailySignalGeneratedEvent CreateFuturesAdxDailySignalGeneratedEvent(this GenerateFuturesAdxDailySignalCommand command, FuturesTrendDirectionType trendDirection, FuturesAdxSignalCompute futuresAdxSignalCompute)
    {
        var entityId = new FuturesAdxDailySignalEntityId(command.FuturesAdxSignalId.ContractId, command.EntityId.TimePeriod, command.EntityId.PeriodLength);
        return new FuturesAdxDailySignalGeneratedEvent
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesAdxDailySignalGeneratedEvent.Actor, FuturesAdxDailySignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesAdxSignal = new(command.EntityId.ContractId, command.FuturesAdxSignalId.ValueDate, command.EntityId.TimePeriod, command.EntityId.PeriodLength, TimeOnly.FromDateTime(DateTime.UtcNow),
               command.FuturesPrice, futuresAdxSignalCompute.PlusDI, futuresAdxSignalCompute.MinusDI, futuresAdxSignalCompute.AdxValue, trendDirection, futuresAdxSignalCompute.TrendDirectionStrength()) { IsWarm = futuresAdxSignalCompute.IsWarm },
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        };
    }
}
