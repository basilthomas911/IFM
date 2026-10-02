using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command;

/// <summary>Handles one start transition for the futures MACD signal.</summary>
public static class StartFuturesMacdSignal
{
    /// <summary>Applies the start event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesMacdSignalCommand command, FuturesMacdSignalCommandState state)
    {
        var seed = command.HistoricalSeed;
        var applied = state.Update(new FuturesMacdSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesMacdSignalStartedEvent.Actor,
                FuturesMacdSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy,
            RestoredSignal = seed is { Length: > 0 } ? null : state.MacdSignals.LastOrDefault(),
            ResetForHistoricalSeed = seed is { Length: > 0 }
        }, command);
        if (applied && seed is { Length: > 0 })
        {
            foreach (var bar in seed)
            {
                var id = new FuturesMacdSignalId(command.EntityId.ContractId,
                    command.EntityId.ValueDate, command.EntityId.TimePeriod,
                    command.EntityId.SignalEmaPeriod,
                    command.EntityId.FastEmaPeriod, command.EntityId.SlowEmaPeriod,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var generated = new GenerateFuturesMacdSignalCommand(id, bar.Close, bar, true)
                {
                    CommandId = Guid.CreateVersion7(),
                    Subject = new ActorSubject(ActorType.Command,
                        GenerateFuturesMacdSignalCommand.Actor,
                        GenerateFuturesMacdSignalCommand.Verb, command.EntityId.Format())
                };
                if (generated.Execute(state) is not ServiceOk<GuidResult>)
                    return command.UpdateFailed("MACD historical initialization failed.");
            }
            if (state.MacdSignals.LastOrDefault()?.IsWarm != true)
                return command.UpdateFailed("MACD historical initialization did not reach a warm state.");
        }
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
