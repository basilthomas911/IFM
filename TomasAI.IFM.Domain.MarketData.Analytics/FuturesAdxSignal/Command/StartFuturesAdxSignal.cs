using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command;

/// <summary>Handles one start transition for the futures ADX signal.</summary>
public static class StartFuturesAdxSignal
{
    /// <summary>Applies the start event to the loaded signal state.</summary>
    public static ServiceResult<GuidResult> Execute(this StartFuturesAdxSignalCommand command, FuturesAdxSignalCommandState state)
    {
        var seed = command.HistoricalSeed;
        var applied = state.Update(new FuturesAdxSignalStartedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesAdxSignalStartedEvent.Actor,
                FuturesAdxSignalStartedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            StartedOn = command.OriginatedOn,
            StartedBy = command.OriginatedBy,
            RestoredSignal = seed is { Length: > 0 } ? null : state.AdxSignals.LastOrDefault(),
            ResetForHistoricalSeed = seed is { Length: > 0 }
        }, command);
        if (applied && seed is { Length: > 0 })
        {
            foreach (var bar in seed)
            {
                var id = new FuturesAdxSignalId(command.EntityId.ContractId,
                    command.EntityId.ValueDate, command.EntityId.TimePeriod,
                    command.EntityId.PeriodLength,
                    TimeOnly.FromDateTime(bar.LastMarketEventUtc.UtcDateTime));
                var generated = new GenerateFuturesAdxSignalCommand(id, bar.Close, bar, true)
                {
                    CommandId = Guid.CreateVersion7(),
                    Subject = new ActorSubject(ActorType.Command,
                        GenerateFuturesAdxSignalCommand.Actor,
                        GenerateFuturesAdxSignalCommand.Verb, command.EntityId.Format())
                };
                if (generated.Execute(state) is not ServiceOk<GuidResult>)
                    return command.UpdateFailed("ADX historical initialization failed.");
            }
            if (state.AdxSignals.LastOrDefault()?.IsWarm != true)
                return command.UpdateFailed("ADX historical initialization did not reach a warm state.");
        }
        return applied
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: unable to apply lifecycle event");
    }
}
