namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Identifies the single transport allowed for an actor subject namespace.
/// </summary>
public enum ActorDeliveryType
{
    Unknown = 0,
    NatsCore = 1,
    NatsJetStream = 2
}
