namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Immutable payload staged atomically with a projector state transition.
/// </summary>
public sealed record EventProjectorOutboxMessage(
    EventProjectorEffectIdentity Identity,
    string EventTypeName,
    byte[] EventPayload)
{
    public string MessageId => Identity.MessageId;
}
