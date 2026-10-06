using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class GenerateFuturesMacdDailySignal
{
    /// <summary>Computes and validates the Futures MACD Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesMacdDailySignalCommand command, FuturesMacdSignalCommandState state)
    {
        var errorMsg = "unable to apply generated MACD signal event";
        var updated = command.Compute(state.MacdSignals, out var futuresMacdSignalCompute) switch
        {
            _ when !futuresMacdSignalCompute.IsValid
                => command.UpdateFailed(ref errorMsg, "futuresMacdSignalCompute MACD signal contains invalid accumulator values"),
            _ => state.Update(command.CreateFuturesMacdDailySignalGeneratedEvent(
                futuresMacdSignalCompute.SignalDirection, futuresMacdSignalCompute), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures MACD Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="previousMacdSignals">The previously accepted previous macd signals used only as computation input.</param>
    /// <param name="futuresMacdSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(
        this GenerateFuturesMacdDailySignalCommand command,
        IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals,
        out FuturesMacdSignalCompute futuresMacdSignalCompute)
       => FuturesMacdSignalCompute.Create(
           command.FuturesPrice,
           previousMacdSignals,
           command.EntityId.Configuration,
           out futuresMacdSignalCompute);

    /// <summary>Creates the Futures MACD Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="trendDirection">The computed business trend direction to record on the signal event.</param>
    /// <param name="futuresMacdSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesMacdDailySignalGeneratedEvent CreateFuturesMacdDailySignalGeneratedEvent(this GenerateFuturesMacdDailySignalCommand command, FuturesTrendDirectionType trendDirection, FuturesMacdSignalCompute futuresMacdSignalCompute)
    {
        var entityId = command.FuturesMacdSignalId.ToDailyEntityId();
        return new FuturesMacdDailySignalGeneratedEvent
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesMacdDailySignalGeneratedEvent.Actor, FuturesMacdDailySignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesMacdSignal = new(
                command.FuturesMacdSignalId.ContractId,
                command.FuturesMacdSignalId.ValueDate,
                command.FuturesMacdSignalId.TimePeriod,
                command.FuturesMacdSignalId.SignalEmaPeriod,
                command.FuturesMacdSignalId.FastEmaPeriod,
                command.FuturesMacdSignalId.SlowEmaPeriod,
                command.FuturesMacdSignalId.Timestamp,
                command.FuturesPrice,
                futuresMacdSignalCompute.MacdLine,
                futuresMacdSignalCompute.SignalLine,
                futuresMacdSignalCompute.Histogram,
                trendDirection,
                futuresMacdSignalCompute.TrendDirectionStrength(),
                futuresMacdSignalCompute.FastEma,
                futuresMacdSignalCompute.SlowEma),
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        };
    }

}
