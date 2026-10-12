namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Optional transport completion carried with a mailbox message. Durable transports use it to acknowledge only after
/// the actor handler reaches its terminal outcome.
/// </summary>
public interface IActorDeliveryCompletion
{
    ValueTask CompleteDeliveryAsync(bool succeeded);
}

