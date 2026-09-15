using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Model;

/// <summary>Appends the common VWAP checkpoint event for either command path.</summary>
internal static class FuturesVwapSignalTransition
{
    /// <summary>Returns a no-op for unchanged input and persists a changed checkpoint.</summary>
    internal static ServiceResult<GuidResult> Append(
        ICommand command,
        FuturesVwapSignalEntityId entityId,
        FuturesVwapAccumulatorResult result,
        FuturesVwapSignalCommandState state)
    {
        if (!result.Changed)
            return new ServiceOk<GuidResult>(new(command.CommandId));
        return state.Update(new FuturesVwapSignalUpdatedEvent
        {
            Subject = new(ActorType.Event, FuturesVwapSignalUpdatedEvent.Actor,
                FuturesVwapSignalUpdatedEvent.Verb, entityId.Format()),
            EntityId = entityId,
            Checkpoint = result.Checkpoint,
            Signal = result.Signal
        }, command)
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : new ServiceFailed<GuidResult>(FuturesVwapSignalUpdatedEvent.ErrorCode,
                "VWAP command state rejected the transition.");
    }
}
