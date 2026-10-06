using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Model;
/// <summary>Immutable proposed backup lifecycle fact, without event or mutation authority.</summary>
internal sealed record DatabaseBackupChange
{
    /// <summary>Gets the permanent source-event family for this business transition.</summary>
    public Type EventFamily { get; init; } = default!;
    /// <summary>Gets the proposed DatabaseSource business information.</summary>
    public DatabaseSourceEnvelope DatabaseSource { get; init; } = new();
    /// <summary>Gets the proposed DatabaseRequest business information.</summary>
    public DatabaseRequestEnvelope? DatabaseRequest { get; init; } 
    /// <summary>Gets the proposed ProgressPercent business information.</summary>
    public int ProgressPercent { get; init; } 
    /// <summary>Gets the proposed SafeDiagnosticReference business information.</summary>
    public string SafeDiagnosticReference { get; init; } = string.Empty;
    /// <summary>Gets the proposed ArtifactReplica business information.</summary>
    public DatabaseArtifactReplicaDescriptor? ArtifactReplica { get; init; } 
    /// <summary>Gets the proposed VerificationLevel business information.</summary>
    public DatabaseVerificationLevel VerificationLevel { get; init; } 
    /// <summary>Gets the proposed Outcome business information.</summary>
    public DatabaseRecoveryOutcome Outcome { get; init; } 
    /// <summary>Gets the proposed ErrorClassification business information.</summary>
    public DatabaseErrorClassification ErrorClassification { get; init; } 
    /// <summary>Gets the proposed CutoverState business information.</summary>
    public DatabaseCutoverState CutoverState { get; init; } 
    /// <summary>Gets the proposed CapabilityState business information.</summary>
    public DatabaseServiceCapabilityState CapabilityState { get; init; } 
    /// <summary>Gets the proposed Statistics business information.</summary>
    public DatabaseRecoveryRunStatistics? Statistics { get; init; } 
    /// <summary>Gets the proposed RestorePointId business information.</summary>
    public DatabaseRestorePointId? RestorePointId { get; init; } 
    /// <summary>Gets the proposed FreshTarget business information.</summary>
    public DatabaseFreshTargetDescriptor? FreshTarget { get; init; } 
    /// <summary>Gets the proposed BackupPolicy business information.</summary>
    public DatabaseBackupPolicyDefinition? BackupPolicy { get; init; } 
    /// <summary>Gets the proposed RequiredDestinations business information.</summary>
    public DatabaseLogicalDestination[] RequiredDestinations { get; init; } = [];
    /// <summary>Gets the proposed ExpectedStateRevision business information.</summary>
    public long ExpectedStateRevision { get; init; } 
    /// <summary>Gets the proposed ValidationRevision business information.</summary>
    public long ValidationRevision { get; init; } 
    /// <summary>Gets the proposed RetentionPlanId business information.</summary>
    public DatabaseRetentionPlanId? RetentionPlanId { get; init; } 
    /// <summary>Gets the proposed RetentionPlanRevision business information.</summary>
    public long RetentionPlanRevision { get; init; } 
    /// <summary>Gets the proposed RestoreClass business information.</summary>
    public DatabaseRestoreClass RestoreClass { get; init; } 
    /// <summary>Gets the proposed EvaluationBoundaryUtc business information.</summary>
    public DateTimeOffset EvaluationBoundaryUtc { get; init; } 
    /// <summary>Gets the proposed PolicyId business information.</summary>
    public DatabaseBackupPolicyId? PolicyId { get; init; } 
    /// <summary>Gets the proposed ManifestRevision business information.</summary>
    public long ManifestRevision { get; init; } 
    /// <summary>Gets the proposed BackupLineage business information.</summary>
    public DatabaseBackupLineage? BackupLineage { get; init; } 
}
/// <summary>Ordered immutable lifecycle changes or a rejected business decision.</summary>
internal sealed record DatabaseBackupTransition(IReadOnlyList<DatabaseBackupChange> LifecycleChanges, string? RejectionReason = null);
