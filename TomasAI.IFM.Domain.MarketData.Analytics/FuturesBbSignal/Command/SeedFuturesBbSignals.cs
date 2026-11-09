using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command;

/// <summary>Reconstructs the complete Bollinger state under its owning actor rather than appending history after a cold prefix.</summary>
public static class SeedFuturesBbSignals
{
    extension(SeedFuturesBbSignalsCommand command)
    {
        public ServiceResult<GuidResult> Execute(FuturesBbSignalCommandState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            var through = command.Observations[^1].IntervalEndUtc;
            if (state.FuturesBbCheckpoint?.LastIntervalEndUtc > through
                || state.FuturesBbSignal is { IsWarm: true } && state.FuturesBbCheckpoint?.LastIntervalEndUtc >= through)
                return new ServiceOk<GuidResult>(new(command.CommandId));
            FuturesEmaAccumulatorCheckpoint? ema = null;
            FuturesBbAccumulatorCheckpoint? bands = null;
            FuturesBbAccumulatorResult? result = null;
            foreach (var observation in command.Observations)
            {
                var center = FuturesEmaAccumulator.Apply(ema, observation);
                ema = center.FuturesEmaCheckpoint;
                result = FuturesBbAccumulator.Apply(bands, observation, center.FuturesEmaSignal!);
                bands = result.FuturesBbCheckpoint;
            }
            if (result?.FuturesBbSignal is not { IsWarm: true })
                return command.UpdateFailed("Bollinger historical initialization did not reach a warm state.");
            var fact = new FuturesBbSignalGeneratedEvent
            {
                CommandId = command.CommandId, EntityId = command.EntityId,
                Subject = new(ActorType.Event, FuturesBbSignalGeneratedEvent.Actor, FuturesBbSignalGeneratedEvent.Verb, command.EntityId.Format()),
                FuturesBbSignal = result.FuturesBbSignal, FuturesBbCheckpoint = result.FuturesBbCheckpoint
            };
            return state.Update(fact, command) ? new ServiceOk<GuidResult>(new(command.CommandId))
                : command.UpdateFailed("Unable to apply the Bollinger initialization snapshot.");
        }
    }
}
