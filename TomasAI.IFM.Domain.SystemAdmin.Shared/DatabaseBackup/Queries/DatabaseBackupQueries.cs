using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

#pragma warning disable MsgPack005 // Abstract query base is never serialized directly.
#pragma warning disable MsgPack015 // Explicit IQuery.EntityId implementation is intentionally non-public.

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;

[MessagePackObject]
public abstract record DatabaseBackupQuery : IQuery, IDatabaseBackupValidatable
{
    public const string Actor = "DatabaseBackupQuery";
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
    [IgnoreMember] public abstract string Verb { get; }

    public virtual void Validate()
    {
        Request.Validate();
        if (EntityId.Value == Guid.Empty) throw new ArgumentException("Query entity ID is required.");
        if (Source != BackupSource.None) DatabaseBackupEnumValidation.RequireConcrete(Source);
        if (PageSize is < 1 or > DatabaseBackupContractLimits.MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(PageSize));
        if (ContinuationIdentity.Length > DatabaseBackupContractLimits.IdentifierLength || ContinuationIdentity.Any(char.IsControl)) throw new ArgumentOutOfRangeException(nameof(ContinuationIdentity));
        if (FromUtc.HasValue && FromUtc.Value.Offset != TimeSpan.Zero) throw new ArgumentException("FromUtc must be UTC.");
        if (ToUtc.HasValue && ToUtc.Value.Offset != TimeSpan.Zero) throw new ArgumentException("ToUtc must be UTC.");
    }

    protected void RequireConcreteSource() => DatabaseBackupEnumValidation.RequireConcrete(Source);
    protected void RequireOperation() { if (OperationId is null || OperationId.Value.Value == Guid.Empty) throw new ArgumentException("Operation ID is required."); }
}

[MessagePackObject] public sealed record GetDatabaseProtectionSetsQuery : DatabaseBackupQuery, IQuery<DatabaseProtectionSetReadModel[]> { [IgnoreMember] public override string Verb => "GetProtectionSets"; }
[MessagePackObject] public sealed record GetDatabaseBackupPolicyQuery : DatabaseBackupQuery, IQuery<DatabaseBackupPolicyReadModel> { [IgnoreMember] public override string Verb => "GetPolicy"; public override void Validate() { base.Validate(); if (PolicyId is null) throw new ArgumentException("Policy ID is required."); } }
[MessagePackObject] public sealed record GetDatabaseBackupOperationQuery : DatabaseBackupQuery, IQuery<DatabaseBackupOperationReadModel> { [IgnoreMember] public override string Verb => "GetBackupOperation"; public override void Validate() { base.Validate(); RequireOperation(); } }
[MessagePackObject] public sealed record ListDatabaseBackupOperationsQuery : DatabaseBackupQuery, IQuery<DatabaseBackupOperationReadModel[]> { [IgnoreMember] public override string Verb => "ListBackupOperations"; }
[MessagePackObject] public sealed record GetDatabaseBackupSetQuery : DatabaseBackupQuery, IQuery<DatabaseBackupSetReadModel> { [IgnoreMember] public override string Verb => "GetBackupSet"; public override void Validate() { base.Validate(); if (BackupSetId is null) throw new ArgumentException("Backup set ID is required."); } }
[MessagePackObject] public sealed record ListDatabaseRestorePointsQuery : DatabaseBackupQuery, IQuery<DatabaseRestorePointReadModel[]> { [IgnoreMember] public override string Verb => "ListRestorePoints"; }
[MessagePackObject] public sealed record GetDatabaseRestorePointQuery : DatabaseBackupQuery, IQuery<DatabaseRestorePointReadModel> { [IgnoreMember] public override string Verb => "GetRestorePoint"; public override void Validate() { base.Validate(); RequireConcreteSource(); if (RestorePointId is null) throw new ArgumentException("Restore point ID is required."); } }
[MessagePackObject] public sealed record GetLatestVerifiedDatabaseBackupQuery : DatabaseBackupQuery, IQuery<DatabaseRestorePointReadModel> { [IgnoreMember] public override string Verb => "GetLatestVerifiedBackup"; public override void Validate() { base.Validate(); RequireConcreteSource(); if (ProtectionSetId is null) throw new ArgumentException("Protection set is required."); } }
[MessagePackObject] public sealed record GetLatestRestoreTestedDatabaseBackupQuery : DatabaseBackupQuery, IQuery<DatabaseRestorePointReadModel> { [IgnoreMember] public override string Verb => "GetLatestRestoreTestedBackup"; public override void Validate() { base.Validate(); RequireConcreteSource(); if (ProtectionSetId is null) throw new ArgumentException("Protection set is required."); } }
[MessagePackObject] public sealed record GetDatabaseRecoveryObjectiveComplianceQuery : DatabaseBackupQuery, IQuery<DatabaseProtectionSetReadModel[]> { [IgnoreMember] public override string Verb => "GetRecoveryObjectiveCompliance"; }
[MessagePackObject] public sealed record GetDatabaseRestoreOperationQuery : DatabaseBackupQuery, IQuery<DatabaseRestoreOperationReadModel> { [IgnoreMember] public override string Verb => "GetRestoreOperation"; public override void Validate() { base.Validate(); RequireOperation(); } }
[MessagePackObject] public sealed record ListDatabaseRestoreDrillsQuery : DatabaseBackupQuery, IQuery<DatabaseRestoreOperationReadModel[]> { [IgnoreMember] public override string Verb => "ListRestoreDrills"; }
[MessagePackObject] public sealed record GetDatabaseRetentionForecastQuery : DatabaseBackupQuery, IQuery<DatabaseRetentionReadModel> { [IgnoreMember] public override string Verb => "GetRetentionForecast"; public override void Validate() { base.Validate(); RequireConcreteSource(); } }
[MessagePackObject] public sealed record GetDatabaseBackupServiceHealthQuery : DatabaseBackupQuery, IQuery<DatabaseBackupHealthReadModel[]> { [IgnoreMember] public override string Verb => "GetServiceHealth"; }
[MessagePackObject] public sealed record GetDatabaseRecoveryRunStatsQuery : DatabaseBackupQuery, IQuery<DatabaseRecoveryRunStatsReadModel> { [IgnoreMember] public override string Verb => "GetRecoveryRunStats"; public override void Validate() { base.Validate(); RequireOperation(); } }
