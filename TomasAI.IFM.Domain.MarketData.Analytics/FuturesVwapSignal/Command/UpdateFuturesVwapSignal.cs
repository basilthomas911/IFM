using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;

/// <summary>Handles a live futures trade for the VWAP signal.</summary>
public static class UpdateFuturesVwapSignal
{
    /// <summary>Applies one live trade and appends an event only if the accumulator advanced.</summary>
    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state)
    {
        var result = FuturesVwapAccumulator.ApplyLive(command.EntityId, state.Checkpoint,
            command.Observation, command.Configuration);
        return FuturesVwapSignalTransition.Append(command, command.EntityId, result, state);
    }
}
