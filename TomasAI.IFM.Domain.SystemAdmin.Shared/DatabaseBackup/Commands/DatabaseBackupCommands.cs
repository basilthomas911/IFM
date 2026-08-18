using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

#pragma warning disable MsgPack005 // Abstract contract base is never serialized directly.

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;

[MessagePackObject]
public abstract record DatabaseBackupCommand : ICommand<DatabaseRecoveryOperationId>, IDatabaseBackupValidatable
{
    public const string Actor = "DatabaseBackupCommand";

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.SystemAdminBoundedContext;
    [Key(6)] public DatabaseRequestEnvelope Request { get; init; } = new();
    [Key(7)] public BackupSource Source { get; init; }
    [Key(8)] public DatabaseProtectionSetId? ProtectionSetId { get; init; }
    [Key(9)] public DatabaseConsistencyMode ConsistencyMode { get; init; }
    [Key(10)] public DatabaseLogicalDestination[] RequiredDestinations { get; init; } = [];
    [Key(11)] public long ExpectedPolicyRevision { get; init; }
    [Key(12)] public long ExpectedStateRevision { get; init; }
    [Key(13)] public string SafeReason { get; init; } = string.Empty;
    [Key(14)] public DatabaseRestorePointId? RestorePointId { get; init; }
    [Key(15)] public DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    [Key(16)] public DatabaseRestoreClass RestoreClass { get; init; }
    [Key(17)] public long ExpectedManifestRevision { get; init; }
    [Key(18)] public string ApprovalIdentity { get; init; } = string.Empty;
    [Key(19)] public string ApprovalReference { get; init; } = string.Empty;
    [Key(20)] public long ValidationRevision { get; init; }
    [Key(21)] public string DisposableTargetProfile { get; init; } = string.Empty;
    [Key(22)] public string ValidationProfile { get; init; } = string.Empty;
    [Key(23)] public DatabaseBackupPolicyId? PolicyId { get; init; }
    [Key(24)] public DatabaseBackupPolicyDefinition? Policy { get; init; }
    [Key(25)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(26)] public string LegalHoldReference { get; init; } = string.Empty;
    [Key(27)] public long ExpectedLegalHoldRevision { get; init; }
    [Key(28)] public DateTimeOffset EvaluationBoundaryUtc { get; init; }
    [Key(29)] public DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    [Key(30)] public long RetentionPlanRevision { get; init; }
    [Key(31)] public DatabaseBackupMode RequestedBackupMode { get; init; }

    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public abstract string Verb { get; }

    public virtual void Validate()
    {
        Request.Validate();
        if (CommandId == Guid.Empty || EntityId.Value == Guid.Empty) throw new ArgumentException("Command and entity IDs are required.");
        if (RequiredDestinations.Length > DatabaseBackupContractLimits.MaximumCollectionCount) throw new ArgumentOutOfRangeException(nameof(RequiredDestinations));
        if (ExpectedPolicyRevision < 0 || ExpectedStateRevision < 0 || ExpectedManifestRevision < 0) throw new ArgumentOutOfRangeException(nameof(ExpectedStateRevision));
        if (Source != BackupSource.None) DatabaseBackupEnumValidation.RequireConcrete(Source);
        DatabaseBackupEnumValidation.RequireOptionalDefined(ConsistencyMode, nameof(ConsistencyMode));
        DatabaseBackupEnumValidation.RequireOptionalDefined(RestoreClass, nameof(RestoreClass));
        DatabaseBackupEnumValidation.RequireOptionalDefined(RequestedBackupMode, nameof(RequestedBackupMode));
    }

    protected void RequireSourceAndProtectionSet()
    {
        DatabaseBackupEnumValidation.RequireConcrete(Source);
        if (ProtectionSetId is null) throw new ArgumentException("Protection set is required.", nameof(ProtectionSetId));
    }

    protected static void RequireSafeText(string value, string parameterName)
        => DatabaseRequestEnvelope.ValidateSafeText(value, parameterName);
}

[MessagePackObject]
public sealed record RequestDatabaseBackupCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9101;
    [IgnoreMember] public override string Verb => "RequestBackup";
    public override void Validate() { base.Validate(); RequireSourceAndProtectionSet(); DatabaseBackupEnumValidation.RequireDefined(ConsistencyMode, nameof(ConsistencyMode)); if (RequiredDestinations.Length == 0) throw new ArgumentException("At least one logical destination is required."); foreach (var destination in RequiredDestinations) _ = new DatabaseArtifactReplicaId(destination.Name); }
}

[MessagePackObject]
public sealed record CancelDatabaseBackupCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9102;
    [IgnoreMember] public override string Verb => "CancelBackup";
    public override void Validate() { base.Validate(); RequireSafeText(SafeReason, nameof(SafeReason)); }
}

[MessagePackObject]
public sealed record RequestDatabaseRestoreCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9103;
    [IgnoreMember] public override string Verb => "RequestRestore";
    public override void Validate() { base.Validate(); RequireSourceAndProtectionSet(); if (RestorePointId is null || FreshTarget is null) throw new ArgumentException("Restore point and fresh target are required."); _ = new DatabaseProtectionSetId(FreshTarget.Profile); _ = new DatabaseProtectionSetId(FreshTarget.LogicalTarget); DatabaseBackupEnumValidation.RequireDefined(RestoreClass, nameof(RestoreClass)); }
}

[MessagePackObject]
public sealed record ApproveDatabaseRestoreCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9104;
    [IgnoreMember] public override string Verb => "ApproveRestore";
    public override void Validate() { base.Validate(); RequireSafeText(ApprovalIdentity, nameof(ApprovalIdentity)); RequireSafeText(ApprovalReference, nameof(ApprovalReference)); }
}

[MessagePackObject]
public sealed record CancelDatabaseRestoreCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9105;
    [IgnoreMember] public override string Verb => "CancelRestore";
    public override void Validate() { base.Validate(); RequireSafeText(SafeReason, nameof(SafeReason)); }
}

[MessagePackObject]
public sealed record ApproveDatabaseCutoverCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9106;
    [IgnoreMember] public override string Verb => "ApproveCutover";
    public override void Validate() { base.Validate(); RequireSafeText(ApprovalIdentity, nameof(ApprovalIdentity)); RequireSafeText(ApprovalReference, nameof(ApprovalReference)); if (ValidationRevision <= 0) throw new ArgumentOutOfRangeException(nameof(ValidationRevision)); }
}

[MessagePackObject]
public sealed record RequestDatabaseRestoreDrillCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9107;
    [IgnoreMember] public override string Verb => "RequestRestoreDrill";
    public override void Validate() { base.Validate(); RequireSourceAndProtectionSet(); if (RestorePointId is null) throw new ArgumentException("Restore point is required."); _ = new DatabaseProtectionSetId(DisposableTargetProfile); _ = new DatabaseProtectionSetId(ValidationProfile); }
}

[MessagePackObject]
public sealed record UpdateDatabaseBackupPolicyCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9108;
    [IgnoreMember] public override string Verb => "UpdatePolicy";
    public override void Validate() { base.Validate(); if (PolicyId is null || Policy is null) throw new ArgumentException("Policy identity and definition are required."); if (Policy.EnabledSources.Length == 0 || Policy.EnabledSources.Length > DatabaseBackupContractLimits.MaximumCollectionCount || Policy.ProtectedSets.Length == 0 || Policy.ProtectedSets.Length > DatabaseBackupContractLimits.MaximumCollectionCount || Policy.Verification.Levels.Length > DatabaseBackupContractLimits.MaximumCollectionCount) throw new ArgumentOutOfRangeException(nameof(Policy)); foreach (var source in Policy.EnabledSources) DatabaseBackupEnumValidation.RequireConcrete(source); foreach (var level in Policy.Verification.Levels) DatabaseBackupEnumValidation.RequireDefined(level, nameof(Policy.Verification.Levels)); }
}

[MessagePackObject]
public sealed record PlaceBackupLegalHoldCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9109;
    [IgnoreMember] public override string Verb => "PlaceLegalHold";
    public override void Validate() { base.Validate(); if (RestorePointId is null && BackupSetId is null) throw new ArgumentException("A restore point or backup set scope is required."); RequireSafeText(SafeReason, nameof(SafeReason)); RequireSafeText(LegalHoldReference, nameof(LegalHoldReference)); }
}

[MessagePackObject]
public sealed record ReleaseBackupLegalHoldCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9110;
    [IgnoreMember] public override string Verb => "ReleaseLegalHold";
    public override void Validate() { base.Validate(); if (RestorePointId is null && BackupSetId is null) throw new ArgumentException("A restore point or backup set scope is required."); RequireSafeText(LegalHoldReference, nameof(LegalHoldReference)); }
}

[MessagePackObject]
public sealed record RequestBackupRetentionEvaluationCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9111;
    [IgnoreMember] public override string Verb => "EvaluateRetention";
    public override void Validate() { base.Validate(); DatabaseBackupEnumValidation.RequireConcrete(Source); DatabaseRequestEnvelope.RequireUtc(EvaluationBoundaryUtc, nameof(EvaluationBoundaryUtc)); }
}

[MessagePackObject]
public sealed record ExecuteBackupRetentionPlanCommand : DatabaseBackupCommand
{
    public const int ErrorId = 9112;
    [IgnoreMember] public override string Verb => "ExecuteRetention";
    public override void Validate() { base.Validate(); DatabaseBackupEnumValidation.RequireConcrete(Source); if (RetentionPlanId is null || RetentionPlanRevision <= 0) throw new ArgumentException("A revision-bound retention plan is required."); RequireSafeText(ApprovalReference, nameof(ApprovalReference)); }
}
