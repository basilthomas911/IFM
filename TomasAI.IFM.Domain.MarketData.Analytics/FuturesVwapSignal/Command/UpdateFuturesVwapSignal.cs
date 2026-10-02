using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command;

/// <summary>Handles a live trade or a complete tick-source checkpoint for VWAP.</summary>
public static class UpdateFuturesVwapSignal
{
    /// <summary>Appends an event only when the incoming VWAP state advances.</summary>
    public static ServiceResult<GuidResult> Execute(
        this UpdateFuturesVwapSignalCommand command,
        FuturesVwapSignalCommandState state)
    {
        var result = command.SourceCheckpoint is { } source
            ? FuturesVwapAccumulator.ApplySourceCheckpoint(command.EntityId, state.Checkpoint,
                source, command.Configuration, command.SessionStartUtc, command.SessionEndUtc)
            : FuturesVwapAccumulator.ApplyLive(command.EntityId, state.Checkpoint,
                command.Observation, command.Configuration);
        return FuturesVwapSignalTransition.Append(command, command.EntityId, result, state);
    }
}
