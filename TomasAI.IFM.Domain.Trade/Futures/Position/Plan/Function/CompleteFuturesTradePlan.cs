using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Function;

public static class CompleteFuturesTradePlan
{
    public static FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>> Complete(
        this FunctionEventContext<UpdateFuturesTradePlanCommand> input, TimeProvider clock)
    {
        var command = input.Request ?? throw new ArgumentException("Trade Plan completion requires its command.");
        if (input.Outcome is FuturesTradePlanUpdatedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
            return FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>.Complete(committed);
        var plan = input.Outcome as StrategyTradePlanSnapshot ?? throw new ArgumentException("Calculated Trade Plan is required.");
        var completed = new FuturesTradePlanUpdatedEvent
        {
            Subject = new(ActorType.Function, UpdateFuturesTradePlanCommand.Actor,
                FuturesTradePlanUpdatedEvent.Verb, command.EntityId.Format()),
            Id = TradePlanContractIdentity.DeterministicId($"{command.CommandId:N}|{FuturesTradePlanUpdatedEvent.Verb}"),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            Plan = plan,
            RequestFingerprint = command.Fingerprint(),
            SourceEventId = command.SourceEventId
        };
        return FunctionResult<FuturesTradePlanUpdatedEvent, TradePlanFailedEvent<FuturesTradePlanId>>.Complete(completed);
    }
}
