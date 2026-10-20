using ForwardPriceSessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
/// <summary>Request-local completed-only state, restored from the latest source event.</summary>
public sealed class IronCondorForwardPriceFunctionState : BaseEventSourceActorState<IronCondorForwardPriceFunctionState>,
    IEventSourceFunctionState<IronCondorForwardPriceFunctionState, CalculateIronCondorForwardPriceCommand, IronCondorForwardPriceCalculatedEvent>
{
    Guid activeCommandId;
    IronCondorForwardPriceCalculatedEvent? completed;
    public override ActorThreadId Id { get; set; }
    public IronCondorForwardPriceCalculatedEvent? CompletedEvent => completed?.CommandId == activeCommandId ? completed : null;
    public bool IsCompleted => CompletedEvent is not null;
    /// <summary>Associates this loaded state with the exact current request.</summary>
    public IronCondorForwardPriceFunctionState Prepare(CalculateIronCondorForwardPriceCommand request) { activeCommandId = request.CommandId; return this; }
    /// <summary>Checks an exact duplicate's captured inputs before replaying its completion.</summary>
    public bool Matches(CalculateIronCondorForwardPriceCommand request) => CompletedEvent?.RequestFingerprint == request.Fingerprint();
    /// <summary>Records a calculated event using standard state-owned application.</summary>
    public bool TryComplete(IronCondorForwardPriceCalculatedEvent result, CalculateIronCondorForwardPriceCommand request) => !IsCompleted && Update(result, request);
    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent) => domainEvent switch { IronCondorForwardPriceCalculatedEvent result => ApplyPrice(result), _ => false };
    /// <summary>Replaces the single completed source snapshot.</summary>
    bool ApplyPrice(IronCondorForwardPriceCalculatedEvent result) { completed = result; return true; }
}
