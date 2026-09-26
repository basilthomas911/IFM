using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;

/// <summary>Direct-key query for GetLatestVerifiedBackup.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetLatestVerifiedDatabaseBackupQuery : IDatabaseBackupQuery, IQuery<DatabaseRestorePointReadModel>
{
    /// <summary>Creates an empty query for serialization.</summary>
    public GetLatestVerifiedDatabaseBackupQuery() { }

    /// <summary>Rehydrates the direct numeric-key query schema.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="request">The Request field.</param>
    /// <param name="source">The Source field.</param>
    /// <param name="operationId">The OperationId field.</param>
    /// <param name="backupSetId">The BackupSetId field.</param>
    /// <param name="restorePointId">The RestorePointId field.</param>
    /// <param name="policyId">The PolicyId field.</param>
    /// <param name="protectionSetId">The ProtectionSetId field.</param>
    /// <param name="retentionPlanId">The RetentionPlanId field.</param>
    /// <param name="pageSize">The PageSize field.</param>
    /// <param name="continuationIdentity">The ContinuationIdentity field.</param>
    /// <param name="fromUtc">The FromUtc field.</param>
    /// <param name="toUtc">The ToUtc field.</param>
    [SerializationConstructor]
    public GetLatestVerifiedDatabaseBackupQuery(ActorSubject subject, DatabaseRecoveryOperationId entityId, DatabaseRequestEnvelope request, BackupSource source, DatabaseRecoveryOperationId? operationId, DatabaseBackupSetId? backupSetId, DatabaseRestorePointId? restorePointId, DatabaseBackupPolicyId? policyId, DatabaseProtectionSetId? protectionSetId, DatabaseRetentionPlanId? retentionPlanId, int pageSize, string continuationIdentity, DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        Subject = subject;
        EntityId = entityId;
        Request = request;
        Source = source;
        OperationId = operationId;
        BackupSetId = backupSetId;
        RestorePointId = restorePointId;
        PolicyId = policyId;
        ProtectionSetId = protectionSetId;
        RetentionPlanId = retentionPlanId;
        PageSize = pageSize;
        ContinuationIdentity = continuationIdentity;
        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public DatabaseRecoveryOperationId EntityId { get; init; }
    [Key(2)] public DatabaseRequestEnvelope Request { get; init; } = new();
    [Key(3)] public BackupSource Source { get; init; }
    [Key(4)] public DatabaseRecoveryOperationId? OperationId { get; init; }
    [Key(5)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(6)] public DatabaseRestorePointId? RestorePointId { get; init; }
    [Key(7)] public DatabaseBackupPolicyId? PolicyId { get; init; }
    [Key(8)] public DatabaseProtectionSetId? ProtectionSetId { get; init; }
    [Key(9)] public DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    [Key(10)] public int PageSize { get; init; } = 50;
    [Key(11)] public string ContinuationIdentity { get; init; } = string.Empty;
    [Key(12)] public DateTimeOffset? FromUtc { get; init; }
    [Key(13)] public DateTimeOffset? ToUtc { get; init; }
    [IgnoreMember] IActorEntityId IQuery.EntityId => EntityId;
    [IgnoreMember] public int ErrorCode => 9200;
    [IgnoreMember] public string? QueryParams => string.IsNullOrEmpty(ContinuationIdentity) ? null : ContinuationIdentity;
    [IgnoreMember] public string Verb => "GetLatestVerifiedBackup";

    /// <summary>Applies the established validation rules to the canonical payload.</summary>
    public void Validate() => DatabaseBackupQueryRules.Validate(this);
}
