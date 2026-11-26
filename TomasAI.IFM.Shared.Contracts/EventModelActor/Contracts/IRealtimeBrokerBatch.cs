namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>One bounded local in-flight slot owns ordered broker children; each child retains its own settlement proof.</summary>
public interface IRealtimeBrokerBatch : IActorMessage,IRealtimeBrokerDelivery
{
    IReadOnlyList<IActorMessage> Messages { get; }
}
