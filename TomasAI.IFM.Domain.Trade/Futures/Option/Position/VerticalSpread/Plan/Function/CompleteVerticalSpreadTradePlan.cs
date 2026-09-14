using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Plan.Function;

public static class CompleteVerticalSpreadTradePlan
{
    public static FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>> Complete(
        this FunctionEventContext<UpdateVerticalSpreadTradePlanCommand> input, TimeProvider clock)
    {
        var command = input.Request ?? throw new ArgumentException("Trade Plan completion requires its command.");
        if (input.Outcome is VerticalSpreadTradePlanUpdatedEvent committed &&
            input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
            return FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>.Complete(committed);
        var plan = input.Outcome as StrategyTradePlanSnapshot ?? throw new ArgumentException("Calculated Trade Plan is required.");
        var completed = new VerticalSpreadTradePlanUpdatedEvent
        {
            Subject = new(ActorType.Function, UpdateVerticalSpreadTradePlanCommand.Actor,
                VerticalSpreadTradePlanUpdatedEvent.Verb, command.EntityId.Format()),
            Id = TradePlanContractIdentity.DeterministicId($"{command.CommandId:N}|{VerticalSpreadTradePlanUpdatedEvent.Verb}"),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            Plan = plan,
            RequestFingerprint = command.Fingerprint(),
            SourceEventId = command.SourceEventId
        };
        return FunctionResult<VerticalSpreadTradePlanUpdatedEvent, TradePlanFailedEvent<VerticalSpreadTradePlanId>>.Complete(completed);
    }
}
