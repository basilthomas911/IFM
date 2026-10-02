using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command;

/// <summary>Handles one start transition for the futures ATR signal.</summary>
public static class StartFuturesAtrSignal
{
    /// <summary>Applies the start event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesAtrSignalCommand command, FuturesAtrSignalCommandState state)
    {
        var seed = command.HistoricalSeed;
        var applied = state.Update(new FuturesAtrSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAtrSignalStartedEvent.Actor,
                FuturesAtrSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy,
            RestoredSignal = seed is { Length: > 0 } ? null : state.AtrSignal,
            ResetForHistoricalSeed = seed is { Length: > 0 }
        }, command);
        if (applied && seed is { Length: > 0 })
        {
            foreach (var bar in seed)
            {
                var id = new FuturesAtrSignalId(command.EntityId.ContractId,
                    command.EntityId.ValueDate, command.EntityId.TimePeriod,
                    command.EntityId.PeriodLength,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var generated = new GenerateFuturesAtrSignalCommand(id, bar.Close, bar, true)
                {
                    CommandId = Guid.CreateVersion7(),
                    Subject = new ActorSubject(ActorType.Command,
                        GenerateFuturesAtrSignalCommand.Actor,
                        GenerateFuturesAtrSignalCommand.Verb, command.EntityId.Format())
                };
                if (generated.Execute(state) is not ServiceOk<GuidResult>)
                    return command.UpdateFailed("ATR historical initialization failed.");
            }
            if (state.AtrSignal is not { IsWarm: true, AtrRatio: not null })
                return command.UpdateFailed("ATR historical initialization did not reach a warm state.");
        }
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
