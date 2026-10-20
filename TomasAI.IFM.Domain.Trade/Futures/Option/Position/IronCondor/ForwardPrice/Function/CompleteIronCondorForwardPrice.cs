using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;
public static class CompleteIronCondorForwardPrice
{
    /// <summary>Creates the price completion with the originating command identity; replays preserve the committed result.</summary>
    /// <param name="input">The request, immutable price result and completion phase.</param>
    /// <param name="clock">Supplies the UTC receipt time.</param>
    /// <returns>The typed successful terminal result.</returns>
    public static FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>> Complete(this FunctionEventContext<CalculateIronCondorForwardPriceCommand> input, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(input);
        var command = input.Request ?? throw new ArgumentException("Forward-price completion requires its command.",nameof(input));
        if (input.Outcome is IronCondorForwardPriceCalculatedEvent committed && input.Phase is FunctionEventPhase.Committed or FunctionEventPhase.Replayed)
            return FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>>.Complete(committed);
        var forwardPrice = input.Outcome as IronCondorForwardPrice ?? throw new ArgumentException("Calculated Iron Condor forward price is required.",nameof(input));
        var completed = new IronCondorForwardPriceCalculatedEvent
        {
            Subject = new(ActorType.Function, CalculateIronCondorForwardPriceCommand.Actor, IronCondorForwardPriceCalculatedEvent.Verb, command.EntityId.Format()),
            Id = TradePlanContractIdentity.DeterministicId($"{command.CommandId:N}|{IronCondorForwardPriceCalculatedEvent.Verb}"),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = clock.GetUtcNow().UtcDateTime,
            IronCondorForwardPrice = forwardPrice,
            RequestFingerprint = command.Fingerprint(),
            SourceEventId = command.SourceEventId
        };
        return FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<ForwardPriceSessionId>>.Complete(completed);
    }
}
