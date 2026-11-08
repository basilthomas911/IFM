using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command;

/// <summary>Computes an ordered bounded batch before applying any events to actor-owned state.</summary>
public static class SeedFuturesEmaSignals
{
    extension(SeedFuturesEmaSignalsCommand command)
    {
        /// <summary>Applies ordinary full-checkpoint EMA events; persistence and publication remain owned by the command actor.</summary>
        public ServiceResult<GuidResult> Execute(FuturesEmaSignalCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var facts = command.Compute(state.FuturesEmaCheckpoint);
            foreach (var fact in facts)
                if (!state.Update(fact, command)) return command.UpdateFailed("Unable to apply an EMA seed event.");
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }

        /// <summary>Computes every accepted full-state fact without mutating actor state; duplicates and already committed prefixes produce no events.</summary>
        internal FuturesEmaSignalGeneratedEvent[] Compute(FuturesEmaAccumulatorCheckpoint? checkpoint)
        {
            List<FuturesEmaSignalGeneratedEvent> facts = [];
            foreach (var observation in command.Observations)
            {
                var transition = FuturesEmaAccumulator.Apply(checkpoint, observation);
                if (!transition.IsApplied) continue;
                checkpoint = transition.FuturesEmaCheckpoint;
                facts.Add(command.CreateFuturesEmaSeedEvent(transition, observation));
            }
            return facts.ToArray();
        }

        /// <summary>Creates the existing event shape so the latest snapshot independently recreates the aggregate after a batch.</summary>
        internal FuturesEmaSignalGeneratedEvent CreateFuturesEmaSeedEvent(FuturesEmaAccumulatorResult transition,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal.FuturesTradeSessionBarReadModel observation)
            => new()
            {
                CommandId = command.CommandId, EntityId = command.EntityId,
                Subject = new(ActorType.Event, FuturesEmaSignalGeneratedEvent.Actor, FuturesEmaSignalGeneratedEvent.Verb, command.EntityId.Format()),
                FuturesEmaSignal = transition.FuturesEmaSignal ?? throw new InvalidOperationException("Applied EMA seed has no signal."),
                FuturesEmaCheckpoint = transition.FuturesEmaCheckpoint, Observation = observation
            };
    }
}
