namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
/// <summary>Accepted backup projection values prepared by the domain repository; contains no executable event policy.</summary>
public sealed record DatabaseBackupProjectionSnapshot
{
    /// <summary>Accepted Id value.</summary>
    public Guid Id { get; init; }
    /// <summary>Accepted EventId value.</summary>
    public long EventId { get; init; }
    /// <summary>Accepted Source value.</summary>
    public DatabaseSourceEnvelope Source { get; init; } = new();
    /// <summary>Accepted Request value.</summary>
    public DatabaseRequestEnvelope? Request { get; init; }
    /// <summary>Accepted ProgressPercent value.</summary>
    public int ProgressPercent { get; init; }
    /// <summary>Accepted SafeDiagnosticReference value.</summary>
    public string SafeDiagnosticReference { get; init; } = string.Empty;
    /// <summary>Accepted ArtifactReplica value.</summary>
    public DatabaseArtifactReplicaDescriptor? ArtifactReplica { get; init; }
    /// <summary>Accepted VerificationLevel value.</summary>
    public DatabaseVerificationLevel VerificationLevel { get; init; }
    /// <summary>Accepted Outcome value.</summary>
    public DatabaseRecoveryOutcome Outcome { get; init; }
    /// <summary>Accepted ErrorClassification value.</summary>
    public DatabaseErrorClassification ErrorClassification { get; init; }
    /// <summary>Accepted CutoverState value.</summary>
    public DatabaseCutoverState CutoverState { get; init; }
    /// <summary>Accepted CapabilityState value.</summary>
    public DatabaseServiceCapabilityState CapabilityState { get; init; }
    /// <summary>Accepted Statistics value.</summary>
    public DatabaseRecoveryRunStatistics? Statistics { get; init; }
    /// <summary>Accepted RestorePointId value.</summary>
    public DatabaseRestorePointId? RestorePointId { get; init; }
    /// <summary>Accepted FreshTarget value.</summary>
    public DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    /// <summary>Accepted Policy value.</summary>
    public DatabaseBackupPolicyDefinition? Policy { get; init; }
    /// <summary>Accepted ValidationRevision value.</summary>
    public long ValidationRevision { get; init; }
    /// <summary>Accepted RetentionPlanId value.</summary>
    public DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    /// <summary>Accepted RetentionPlanRevision value.</summary>
    public long RetentionPlanRevision { get; init; }
    /// <summary>Accepted RestoreClass value.</summary>
    public DatabaseRestoreClass RestoreClass { get; init; }
    /// <summary>Accepted EvaluationBoundaryUtc value.</summary>
    public DateTimeOffset EvaluationBoundaryUtc { get; init; }
    /// <summary>Accepted PolicyId value.</summary>
    public DatabaseBackupPolicyId? PolicyId { get; init; }
    /// <summary>Accepted ManifestRevision value.</summary>
    public long ManifestRevision { get; init; }
    /// <summary>Accepted BackupLineage value.</summary>
    public DatabaseBackupLineage? BackupLineage { get; init; }

    /// <summary>Deterministic source receipt hash.</summary>
    public string EventHash { get; init; } = string.Empty;
    /// <summary>Whether an operation and phase observation are supplied.</summary>
    public bool WriteOperation { get; init; }
    /// <summary>Whether a recovery error row is supplied.</summary>
    public bool WriteError { get; init; }
    /// <summary>Whether host health is supplied.</summary>
    public bool WriteServiceHealth { get; init; }
    /// <summary>Accepted restore eligibility.</summary>
    public bool Eligible { get; init; }
    /// <summary>Accepted legal hold value.</summary>
    public bool LegalHold { get; init; }
    /// <summary>Whether successful restore testing was recorded.</summary>
    public bool RestoreTested { get; init; }
    /// <summary>Accepted policy enforcement value.</summary>
    public bool Enforced { get; init; }
    /// <summary>Accepted reconciliation value.</summary>
    public bool Reconciled { get; init; }
    /// <summary>Accepted host readiness.</summary>
    public bool Ready { get; init; }
    /// <summary>Accepted retention approval.</summary>
    public bool Approved { get; init; }
    /// <summary>Accepted terminal observation time.</summary>
    public DateTime? CompletedUtc { get; init; }
    /// <summary>Accepted verification observation time.</summary>
    public DateTime? VerifiedUtc { get; init; }
}
