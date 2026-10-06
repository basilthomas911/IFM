using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class GenerateFuturesTdiSignal
{
    /// <summary>Computes and validates the Futures TDI Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this GenerateFuturesTdiSignalCommand command, FuturesTdiSignalCommandState state)
    {
        var errorMsg = "unable to apply generated TDI signal event";
        var updated = command.Compute(state.TdiSignal, out var futuresTdiSignalCompute) switch
        {
            _ when futuresTdiSignalCompute is null
                => command.UpdateFailed(ref errorMsg, "unable to compute TDI signal"),
            _ => state.Update(command.CreateFuturesTdiSignalGeneratedEvent(futuresTdiSignalCompute), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures TDI Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="tdiSignal">The tdi signal business data used by this operation.</param>
    /// <param name="futuresTdiSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(
        this GenerateFuturesTdiSignalCommand command,
        FuturesTdiSignalReadModel? tdiSignal,
        out FuturesTdiSignalCompute? futuresTdiSignalCompute)
        => FuturesTdiSignalCompute.Create(command.FuturesRsiSignals, tdiSignal, command.Configuration, out futuresTdiSignalCompute);

    /// <summary>Creates the Futures TDI Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresTdiSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesTdiSignalGeneratedEvent CreateFuturesTdiSignalGeneratedEvent(
        this GenerateFuturesTdiSignalCommand command,
        FuturesTdiSignalCompute futuresTdiSignalCompute)
    {
        var entityId = new FuturesTdiSignalEntityId(
            command.FuturesTdiSignalId.ContractId,
            command.FuturesTdiSignalId.ValueDate,
            command.EntityId.TimePeriod,
            command.Configuration.ConfigurationId);
        var currentFuturesRsiSignal = futuresTdiSignalCompute.CurrentRsiSignal;
        return new FuturesTdiSignalGeneratedEvent
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesTdiSignalGeneratedEvent.Actor, FuturesTdiSignalGeneratedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            FuturesTdiSignal = new(
                command.FuturesTdiSignalId.ContractId,
                command.FuturesTdiSignalId.ValueDate,
                command.EntityId.TimePeriod,
                currentFuturesRsiSignal.Timestamp,
                command.Configuration,
                currentFuturesRsiSignal.Price,
                currentFuturesRsiSignal.RSI,
                futuresTdiSignalCompute.PriceLine,
                futuresTdiSignalCompute.SignalLine,
                futuresTdiSignalCompute.MarketBaseLine,
                futuresTdiSignalCompute.UpperVolatilityBand,
                futuresTdiSignalCompute.LowerVolatilityBand,
                futuresTdiSignalCompute.TrendDirection,
                futuresTdiSignalCompute.TrendStrength,
                futuresTdiSignalCompute.Cross,
                futuresTdiSignalCompute.MarketState,
                currentFuturesRsiSignal.SourceSequence,
                currentFuturesRsiSignal.SourceEventTimestamp),
            CreatedBy = command.OriginatedBy,
            CreatedOn = command.OriginatedOn
        };
    }
}
