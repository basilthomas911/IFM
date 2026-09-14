using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;

public static class CompleteIronCondorTradePlan
{
    public static FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>> Complete(
        this FunctionEventContext<UpdateIronCondorTradePlanCommand> input, TimeProvider clock)
    {
        var command = input.Request ?? throw new ArgumentException("Trade Plan completion requires its command.");
        if (input.Outcome is IronCondorTradePlanUpdatedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
            return FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>.Complete(committed);
        var plan = input.Outcome as StrategyTradePlanSnapshot ?? throw new ArgumentException("Calculated Trade Plan is required.");
        var completed = new IronCondorTradePlanUpdatedEvent
        {
            Subject = new(ActorType.Function, UpdateIronCondorTradePlanCommand.Actor,
                IronCondorTradePlanUpdatedEvent.Verb, command.EntityId.Format()),
            Id = TradePlanContractIdentity.DeterministicId($"{command.CommandId:N}|{IronCondorTradePlanUpdatedEvent.Verb}"),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            Plan = plan,
            RequestFingerprint = command.Fingerprint(),
            SourceEventId = command.SourceEventId
        };
        return FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>.Complete(completed);
    }
}
