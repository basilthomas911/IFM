namespace TomasAI.IFM.Shared.EventProjector.ReadModels;

/// <summary>
/// One claimed durable projector publication awaiting dispatch.
/// </summary>
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
