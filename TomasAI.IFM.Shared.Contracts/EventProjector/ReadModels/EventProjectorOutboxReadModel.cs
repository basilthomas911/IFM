namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>
/// One claimed durable projector publication awaiting dispatch.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
public sealed record EventProjectorOutboxReadModel(
    string ProjectorName,
    long EventId,
    EventProjectorEffectKind EffectKind,
    string MessageId,
    string EventTypeName,
    byte[] EventPayload,
    EventProjectorOutboxStatus Status,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    string LastError,
    Guid DispatchToken,
    DateTime DispatchLeaseExpiresAtUtc);
