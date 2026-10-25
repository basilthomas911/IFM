namespace TomasAI.IFM.Shared.EventProjector;

/// <summary>
/// Immutable payload staged atomically with a projector state transition.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
public sealed record EventProjectorOutboxMessage(
    EventProjectorEffectIdentity Identity,
    string EventTypeName,
    byte[] EventPayload)
{
    public string MessageId => Identity.MessageId;
}
