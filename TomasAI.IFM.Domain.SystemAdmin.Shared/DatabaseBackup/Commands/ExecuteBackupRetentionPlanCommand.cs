using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;

/// <summary>Direct-key command for ExecuteRetention.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ExecuteBackupRetentionPlanCommand : IDatabaseBackupCommand
{
    /// <summary>Creates an empty command for serialization.</summary>
    public ExecuteBackupRetentionPlanCommand() { }

    /// <summary>Rehydrates the direct numeric-key command schema.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="request">The Request field.</param>
    /// <param name="source">The Source field.</param>
    /// <param name="protectionSetId">The ProtectionSetId field.</param>
    /// <param name="consistencyMode">The ConsistencyMode field.</param>
    /// <param name="requiredDestinations">The RequiredDestinations field.</param>
    /// <param name="expectedPolicyRevision">The ExpectedPolicyRevision field.</param>
    /// <param name="expectedStateRevision">The ExpectedStateRevision field.</param>
    /// <param name="safeReason">The SafeReason field.</param>
    /// <param name="restorePointId">The RestorePointId field.</param>
    /// <param name="freshTarget">The FreshTarget field.</param>
    /// <param name="restoreClass">The RestoreClass field.</param>
    /// <param name="expectedManifestRevision">The ExpectedManifestRevision field.</param>
    /// <param name="approvalIdentity">The ApprovalIdentity field.</param>
    /// <param name="approvalReference">The ApprovalReference field.</param>
    /// <param name="validationRevision">The ValidationRevision field.</param>
    /// <param name="disposableTargetProfile">The DisposableTargetProfile field.</param>
    /// <param name="validationProfile">The ValidationProfile field.</param>
    /// <param name="policyId">The PolicyId field.</param>
    /// <param name="policy">The Policy field.</param>
    /// <param name="backupSetId">The BackupSetId field.</param>
    /// <param name="legalHoldReference">The LegalHoldReference field.</param>
    /// <param name="expectedLegalHoldRevision">The ExpectedLegalHoldRevision field.</param>
    /// <param name="evaluationBoundaryUtc">The EvaluationBoundaryUtc field.</param>
    /// <param name="retentionPlanId">The RetentionPlanId field.</param>
    /// <param name="retentionPlanRevision">The RetentionPlanRevision field.</param>
    /// <param name="requestedBackupMode">The RequestedBackupMode field.</param>
    [SerializationConstructor]
    public ExecuteBackupRetentionPlanCommand(Guid commandId, ActorSubject subject, bool postEvents, DatabaseRecoveryOperationId entityId, int errorCode, BoundedContextName routeTo, DatabaseRequestEnvelope request, BackupSource source, DatabaseProtectionSetId? protectionSetId, DatabaseConsistencyMode consistencyMode, DatabaseLogicalDestination[] requiredDestinations, long expectedPolicyRevision, long expectedStateRevision, string safeReason, DatabaseRestorePointId? restorePointId, DatabaseFreshTargetDescriptor? freshTarget, DatabaseRestoreClass restoreClass, long expectedManifestRevision, string approvalIdentity, string approvalReference, long validationRevision, string disposableTargetProfile, string validationProfile, DatabaseBackupPolicyId? policyId, DatabaseBackupPolicyDefinition? policy, DatabaseBackupSetId? backupSetId, string legalHoldReference, long expectedLegalHoldRevision, DateTimeOffset evaluationBoundaryUtc, DatabaseRetentionPlanId? retentionPlanId, long retentionPlanRevision, DatabaseBackupMode requestedBackupMode)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Request = request;
        Source = source;
        ProtectionSetId = protectionSetId;
        ConsistencyMode = consistencyMode;
        RequiredDestinations = requiredDestinations;
        ExpectedPolicyRevision = expectedPolicyRevision;
        ExpectedStateRevision = expectedStateRevision;
        SafeReason = safeReason;
        RestorePointId = restorePointId;
        FreshTarget = freshTarget;
        RestoreClass = restoreClass;
        ExpectedManifestRevision = expectedManifestRevision;
        ApprovalIdentity = approvalIdentity;
        ApprovalReference = approvalReference;
        ValidationRevision = validationRevision;
        DisposableTargetProfile = disposableTargetProfile;
        ValidationProfile = validationProfile;
        PolicyId = policyId;
        Policy = policy;
        BackupSetId = backupSetId;
        LegalHoldReference = legalHoldReference;
        ExpectedLegalHoldRevision = expectedLegalHoldRevision;
        EvaluationBoundaryUtc = evaluationBoundaryUtc;
        RetentionPlanId = retentionPlanId;
        RetentionPlanRevision = retentionPlanRevision;
        RequestedBackupMode = requestedBackupMode;
    }

    public const string Actor = DatabaseBackupCommandRoute.Actor;
    public const string VerbValue = "ExecuteRetention";
    public const int ErrorId = 9112;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
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
    [IgnoreMember] public string CommandName => nameof(ExecuteBackupRetentionPlanCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor;
    [IgnoreMember] public string Verb => VerbValue;

    /// <summary>Applies the established validation rules to the canonical payload.</summary>
    public void Validate() => DatabaseBackupCommandRules.Validate(this);
}
