using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

#pragma warning disable MsgPack005 // Abstract contract bases are never serialized directly.

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;

[MessagePackObject]
public abstract record DatabaseBackupEventContract : IEvent<DatabaseRecoveryOperationId>, IDatabaseBackupValidatable
{
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public Guid Id { get; init; }
    [Key(2)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(3)] public long EventId { get; init; }
    [Key(4)] public Guid CommandId { get; init; }
    [Key(5)] public string AggregateId { get; init; } = string.Empty;
    [Key(6)] public string EventSource { get; init; } = string.Empty;
    [Key(7)] public DateTime ReceivedOn { get; init; }
    [Key(8)] public DatabaseSourceEnvelope Source { get; init; } = new();
    [Key(9)] public DatabaseRequestEnvelope? Request { get; init; }
    [Key(10)] public int ProgressPercent { get; init; }
    [Key(11)] public string SafeDiagnosticReference { get; init; } = string.Empty;
    [Key(12)] public DatabaseArtifactReplicaDescriptor? ArtifactReplica { get; init; }
    [Key(13)] public DatabaseVerificationLevel VerificationLevel { get; init; }
    [Key(14)] public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(15)] public DatabaseErrorClassification ErrorClassification { get; init; }
    [Key(16)] public DatabaseCutoverState CutoverState { get; init; }
    [Key(17)] public DatabaseServiceCapabilityState CapabilityState { get; init; }
    [Key(18)] public DatabaseRecoveryRunStatistics? Statistics { get; init; }
    [Key(19)] public DatabaseRestorePointId? RestorePointId { get; init; }
    [Key(20)] public DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    [Key(21)] public DatabaseBackupPolicyDefinition? Policy { get; init; }
    [Key(22)] public DatabaseLogicalDestination[] RequiredDestinations { get; init; } = [];
    [Key(23)] public long ExpectedStateRevision { get; init; }
    [Key(24)] public long ValidationRevision { get; init; }
    [Key(25)] public DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    [Key(26)] public long RetentionPlanRevision { get; init; }
    [Key(27)] public DatabaseRestoreClass RestoreClass { get; init; }
    [Key(28)] public DateTimeOffset EvaluationBoundaryUtc { get; init; }
    [Key(29)] public DatabaseBackupPolicyId? PolicyId { get; init; }
    [Key(30)] public long ManifestRevision { get; init; }

    [IgnoreMember] public string UserName => "DatabaseBackup";
    [IgnoreMember] public string EventName => GetType().Name;
    [IgnoreMember] public abstract string Verb { get; }
    [IgnoreMember] public virtual EventType EventType => EventType.DomainEvent;

    public virtual void Validate()
    {
        Source.Validate();
        Request?.Validate();
        if (Id == Guid.Empty || EntityId != Source.OperationId) throw new ArgumentException("Event identity must match the source operation.");
        if (ProgressPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(ProgressPercent));
        if (RequiredDestinations.Length > DatabaseBackupContractLimits.MaximumCollectionCount) throw new ArgumentOutOfRangeException(nameof(RequiredDestinations));
        if (SafeDiagnosticReference.Length > DatabaseBackupContractLimits.DiagnosticReferenceLength || SafeDiagnosticReference.Any(char.IsControl)) throw new ArgumentOutOfRangeException(nameof(SafeDiagnosticReference));
        DatabaseBackupEnumValidation.RequireOptionalDefined(VerificationLevel, nameof(VerificationLevel));
        DatabaseBackupEnumValidation.RequireOptionalDefined(Outcome, nameof(Outcome));
        DatabaseBackupEnumValidation.RequireOptionalDefined(ErrorClassification, nameof(ErrorClassification));
        DatabaseBackupEnumValidation.RequireOptionalDefined(CutoverState, nameof(CutoverState));
        DatabaseBackupEnumValidation.RequireOptionalDefined(CapabilityState, nameof(CapabilityState));
        DatabaseBackupEnumValidation.RequireOptionalDefined(RestoreClass, nameof(RestoreClass));
        if (EvaluationBoundaryUtc != default && EvaluationBoundaryUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("EvaluationBoundaryUtc must be UTC.", nameof(EvaluationBoundaryUtc));
        if (ManifestRevision < 0) throw new ArgumentOutOfRangeException(nameof(ManifestRevision));
    }
}

[MessagePackObject]
public abstract record DatabaseBackupServiceEventContract : DatabaseBackupEventContract
{
    [IgnoreMember] public override EventType EventType => EventType.ServiceEvent;
}
