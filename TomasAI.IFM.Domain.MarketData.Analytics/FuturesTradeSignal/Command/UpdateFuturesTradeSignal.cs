using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class UpdateFuturesTradeSignal
{

    /// <param name="command">
    /// The originating concrete command, including its identity and domain inputs.
    /// </param>
    extension(UpdateFuturesTradeSignalCommand command)
    {
        /// <summary>Computes and validates the Futures Trade Signal command, then applies accepted events through actor-owned state.</summary>
        ///
        /// <param name="state">The command actor state that owns the current business values and pending events.</param>
        /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesTradeSignalCommandState state, TimeProvider? timeProvider = null)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var futuresTradeSignalComputed = command.Compute(out var futuresTradeSignalCompute, timeProvider);
            if (futuresTradeSignalComputed && !state.HasFuturesTradeSignalChanged(futuresTradeSignalCompute.FuturesTradeSignal))
                return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
            var errorMsg = "unable to apply trade signal events";
            var updated = futuresTradeSignalComputed switch
            {
                _ when !futuresTradeSignalComputed
                    => command.UpdateFailed(ref errorMsg, "unable to compute trade signal"),
                _ => state.Update(command.CreateFuturesTradeSignalEvents(futuresTradeSignalCompute,
                    state.HasFuturesItiSignalHoldTradeChanged(futuresTradeSignalCompute.FuturesTradeSignal)), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
        }

        /// <summary>Creates the Futures Trade Signal event payload from accepted business data without changing state or publishing messages.</summary>
        ///
        /// <param name="futuresTradeSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
        /// <param name="holdTradeChanged">Whether the computed trade signal requires a correlated ITI hold transition.</param>
        /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
        internal IReadOnlyList<IEvent> CreateFuturesTradeSignalEvents(FuturesTradeSignalCompute futuresTradeSignalCompute, bool holdTradeChanged)
        {
            var events = new List<IEvent> { command.CreateFuturesTradeSignalUpdatedEvent(futuresTradeSignalCompute) };
            if (holdTradeChanged) events.Add(command.CreateFuturesItiSignalHoldTradeChangedEvent(futuresTradeSignalCompute));
            return events;
        }

        /// <summary>Computes the proposed Futures Trade Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
        ///
        /// <param name="futuresTradeSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
        /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
        /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
        internal bool Compute(out FuturesTradeSignalCompute futuresTradeSignalCompute, TimeProvider? timeProvider = null)
            => FuturesTradeSignalCompute.Create(command, out futuresTradeSignalCompute, timeProvider);

        /// <summary>Creates the Futures Trade Signal event payload from accepted business data without changing state or publishing messages.</summary>
        ///
        /// <param name="futuresTradeSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
        /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
        internal FuturesTradeSignalUpdatedEvent CreateFuturesTradeSignalUpdatedEvent(FuturesTradeSignalCompute futuresTradeSignalCompute)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(
                    ActorType.Event,
                    FuturesTradeSignalUpdatedEvent.Actor,
                    FuturesTradeSignalUpdatedEvent.Verb,
                    command.EntityId.Format()),
                EntityId = command.EntityId,
                FuturesTradeSignal = futuresTradeSignalCompute.FuturesTradeSignal,
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };

        /// <summary>Creates the Futures Trade Signal event payload from accepted business data without changing state or publishing messages.</summary>
        ///
        /// <param name="futuresTradeSignalCompute">The computed business decision, including accepted domain values and any no-change or rejection information.</param>
        /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
        internal FuturesItiSignalHoldTradeChangedEvent CreateFuturesItiSignalHoldTradeChangedEvent(FuturesTradeSignalCompute futuresTradeSignalCompute)
        {
            var entityId = new FuturesItiSignalEntityId(
                command.EntityId.ContractId,
                command.EntityId.ValueDate,
                command.EntityId.TimePeriod);
            var signalId = FuturesItiSignalId.Create(
                command.EntityId.ContractId,
                command.EntityId.ValueDate,
                command.EntityId.TimePeriod,
                command.OriginatedOn);
            return new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(
                    ActorType.Event,
                    FuturesItiSignalHoldTradeChangedEvent.Actor,
                    FuturesItiSignalHoldTradeChangedEvent.Verb,
                    entityId.Format()),
                EntityId = entityId,
                FuturesItiSignalId = signalId,
                HoldTrade = futuresTradeSignalCompute.FuturesTradeSignal.TradeExecuteState == TradeExecuteState.Hold,
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };
        }
    }


}
