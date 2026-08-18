using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;

internal readonly record struct ProjectionKey(string ProjectorName, long EventId) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId));
}

internal readonly record struct ProjectorKey(string ProjectorName) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName));
}

internal readonly record struct InsertProjectionReceiptParameter(
    string ProjectorName, long EventId, string EventHash, Guid SourceEventId, DateTime AppliedUtc) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId), Text(EventHash), Uuid(SourceEventId), TimestampTz(AppliedUtc));
}

internal readonly record struct UpsertProjectionCheckpointParameter(
    string ProjectorName, long EventId, DateTime UpdatedUtc) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId), TimestampTz(UpdatedUtc));
}

internal readonly record struct UpsertOperationParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind()
    {
        var e = Event;
        var terminal = e.Source.Phase is DatabaseRecoveryPhase.Completed or DatabaseRecoveryPhase.Failed
            or DatabaseRecoveryPhase.Cancelled or DatabaseRecoveryPhase.Rejected;
        return Values(
            Uuid(e.Source.OperationId.Value), Uuid(e.Source.BackupSetId?.Value), Text(e.Source.ProtectionSetId.Value),
            Smallint((short)e.Source.Source), Smallint((short)e.Source.OperationKind), Smallint((short)e.Source.Phase),
            Smallint((short)e.Outcome), Integer(e.ProgressPercent), Bigint(e.EventId), TimestampTz(e.Source.ObservedUtc.UtcDateTime),
            TimestampTz(terminal ? e.Source.ObservedUtc.UtcDateTime : null), Text(e.SafeDiagnosticReference),
            Text(e.RestorePointId?.Value), Smallint((short)e.RestoreClass), Text(e.FreshTarget?.Profile ?? string.Empty),
            Bigint(e.ValidationRevision), Smallint((short)e.CutoverState), Bigint(e.Source.PolicyRevision),
            Text(SystemAdminDbJson.SerializeLineage(e.BackupLineage)), Bigint(e.EventId), Uuid(e.Source.SourceEventId));
    }
}

internal readonly record struct InsertPhaseParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind() => Values(
        Uuid(Event.Source.OperationId.Value), Smallint((short)Event.Source.Phase), Bigint(Event.EventId),
        Smallint((short)Event.Outcome), Integer(Event.ProgressPercent), TimestampTz(Event.Source.ObservedUtc.UtcDateTime),
        Text(Event.Source.ProducingHostId?.Value), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

internal readonly record struct UpsertRestorePointParameter(
    DatabaseBackupEventContract Event, bool Eligible, bool LegalHold, bool RestoreTested) : IBindValue
{
    public object Bind() => Values(
        Text(Event.RestorePointId!.Value.Value), Smallint((short)Event.Source.Source), Uuid(Event.Source.BackupSetId?.Value),
        Text(Event.Source.ProtectionSetId.Value), TimestampTz(Event.Source.ObservedUtc.UtcDateTime),
        Smallint((short)Event.VerificationLevel), TimestampTz(Event.VerificationLevel == DatabaseVerificationLevel.None ? null : Event.Source.ObservedUtc.UtcDateTime),
        TimestampTz(RestoreTested ? Event.Source.ObservedUtc.UtcDateTime : null), Boolean(Eligible), Boolean(LegalHold),
        Bigint(Event.ManifestRevision), Text(SystemAdminDbJson.SerializeLineage(Event.BackupLineage)), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

static class SystemAdminDbJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    internal static string SerializeLineage(DatabaseBackupLineage? lineage)
        => lineage is null
            ? string.Empty
            : JsonSerializer.Serialize(DatabaseBackupLineageDocument.From(lineage), Options);

    internal static DatabaseBackupLineage? DeserializeLineage(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var document = JsonSerializer.Deserialize<DatabaseBackupLineageDocument>(value, Options)
            ?? throw new InvalidOperationException("Stored database backup lineage JSON is invalid.");
        return document.ToLineage();
    }

    sealed record DatabaseBackupLineageDocument
    {
        public DatabaseBackupMode RequestedMode { get; init; }
        public DatabaseBackupMode ResolvedMode { get; init; }
        public DatabaseNativeBackupKind NativeKind { get; init; }
        public string? BaseRestorePointId { get; init; }
        public string? ParentRestorePointId { get; init; }
        public int ChainDepth { get; init; }
        public string NativeIdentity { get; init; } = string.Empty;

        public static DatabaseBackupLineageDocument From(DatabaseBackupLineage value) => new()
        {
            RequestedMode = value.RequestedMode,
            ResolvedMode = value.ResolvedMode,
            NativeKind = value.NativeKind,
            BaseRestorePointId = value.BaseRestorePointId?.Value,
            ParentRestorePointId = value.ParentRestorePointId?.Value,
            ChainDepth = value.ChainDepth,
            NativeIdentity = value.NativeIdentity
        };

        public DatabaseBackupLineage ToLineage() => new()
        {
            RequestedMode = RequestedMode,
            ResolvedMode = ResolvedMode,
            NativeKind = NativeKind,
            BaseRestorePointId = string.IsNullOrWhiteSpace(BaseRestorePointId)
                ? null : new DatabaseRestorePointId(BaseRestorePointId),
            ParentRestorePointId = string.IsNullOrWhiteSpace(ParentRestorePointId)
                ? null : new DatabaseRestorePointId(ParentRestorePointId),
            ChainDepth = ChainDepth,
            NativeIdentity = NativeIdentity
        };
    }
}

internal readonly record struct UpsertArtifactReplicaParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind()
    {
        var replica = Event.ArtifactReplica!;
        return Values(
            Text(replica.ReplicaId.Value), Smallint((short)Event.Source.Source), Uuid(Event.Source.OperationId.Value),
            Text(replica.ArtifactId.Value), Smallint((short)replica.Engine), Smallint((short)replica.State),
            Text(replica.SafeDestinationReference), Bigint(replica.Bytes), Bigint(Event.EventId), Bigint(Event.EventId),
            Uuid(Event.Source.SourceEventId));
    }
}

internal readonly record struct UpsertRecoveryErrorParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind() => Values(
        Uuid(Event.Source.OperationId.Value), Uuid(Event.Source.SourceEventId), Smallint((short)Event.ErrorClassification),
        Text(Event.SafeDiagnosticReference), TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

internal readonly record struct UpsertPolicyParameter(DatabaseBackupEventContract Event, string DefinitionJson, bool Enforced) : IBindValue
{
    public object Bind() => Values(
        Text(Event.Request?.EnvironmentIdentity ?? string.Empty), Text(Event.PolicyId!.Value.Value),
        Bigint(Event.Source.PolicyRevision), Text(DefinitionJson), Boolean(Enforced), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

internal readonly record struct UpsertServiceHealthParameter(DatabaseBackupEventContract Event, bool Reconciled) : IBindValue
{
    public object Bind() => Values(
        Text(Event.Request?.EnvironmentIdentity ?? "system"), Smallint((short)Event.Source.Source),
        Text(Event.Source.ProducingHostId!.Value.Value), Smallint((short)Event.CapabilityState),
        Boolean(Event.CapabilityState == DatabaseServiceCapabilityState.Ready), Bigint(Event.Source.SourceRevisionOrSequence),
        TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Text(Event.SafeDiagnosticReference), Boolean(Reconciled),
        Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

internal readonly record struct UpsertRetentionParameter(DatabaseBackupEventContract Event, string RetainJson, string DeleteJson, bool Approved) : IBindValue
{
    public object Bind() => Values(
        Uuid(Event.RetentionPlanId!.Value.Value), Smallint((short)Event.Source.Source), Bigint(Event.RetentionPlanRevision),
        TimestampTz((Event.EvaluationBoundaryUtc == default ? Event.Source.ObservedUtc : Event.EvaluationBoundaryUtc).UtcDateTime),
        Text(RetainJson), Text(DeleteJson), Boolean(Approved), Smallint((short)Event.Outcome), Bigint(Event.EventId),
        Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

internal readonly record struct InsertRunStatisticsParameter(DatabaseBackupEventContract Event) : IBindValue
{
    public object Bind()
    {
        var stats = Event.Statistics!;
        return Values(
            Uuid(Event.Source.OperationId.Value), Smallint((short)Event.Source.Source), Smallint((short)stats.Phase),
            Smallint((short)stats.Engine), Bigint(Event.EventId), TimestampTz(stats.StartedUtc?.UtcDateTime),
            TimestampTz(stats.CompletedUtc?.UtcDateTime), Bigint(stats.Elapsed?.Ticks), Bigint(stats.SourceBytes),
            Bigint(stats.StoredBytes), Bigint(stats.TransferredBytes), Bigint(stats.RestoredBytes), Integer(stats.ArtifactCount),
            Double(stats.AverageThroughputBytesPerSecond), Double(stats.PeakThroughputBytesPerSecond), Integer(stats.RetryCount),
            Integer(stats.WarningCount), Bigint(stats.AchievedRpo?.Ticks), Bigint(stats.AchievedRto?.Ticks),
            Text(Event.Source.ProducingHostId?.Value), Bigint(Event.Source.PolicyRevision), Bigint(Event.EventId),
            Uuid(Event.Source.SourceEventId));
    }
}

internal readonly record struct SourceFilter(BackupSource Source) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source));
}

internal readonly record struct PolicyQueryParameter(string Environment, string PolicyId) : IBindValue
{
    public object Bind() => Values(Text(Environment), Text(PolicyId));
}

internal readonly record struct OperationKey(Guid OperationId) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId));
}

internal readonly record struct OperationListParameter(
    BackupSource Source, string? ProtectionSetId, DateTime? FromUtc, DateTime? ToUtc,
    Guid? Continuation, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId), TimestampTz(FromUtc), TimestampTz(ToUtc), Uuid(Continuation), Integer(PageSize));
}

internal readonly record struct BackupSetKey(Guid BackupSetId) : IBindValue
{
    public object Bind() => Values(Uuid(BackupSetId));
}

internal readonly record struct RestorePointListParameter(
    BackupSource Source, string? ProtectionSetId, DateTime? FromUtc, DateTime? ToUtc,
    string? Continuation, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId), TimestampTz(FromUtc), TimestampTz(ToUtc), Text(Continuation), Integer(PageSize));
}

internal readonly record struct RestorePointKey(string RestorePointId, BackupSource Source) : IBindValue
{
    public object Bind() => Values(Text(RestorePointId), Smallint((short)Source));
}

internal readonly record struct LatestRestorePointKey(BackupSource Source, string ProtectionSetId) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId));
}

internal readonly record struct RestoreDrillListParameter(BackupSource Source, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Integer(PageSize));
}

internal readonly record struct RetentionQueryParameter(BackupSource Source, Guid? PlanId) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Uuid(PlanId));
}

internal readonly record struct ServiceHealthQueryParameter(string Environment, BackupSource Source) : IBindValue
{
    public object Bind() => Values(Text(Environment), Smallint((short)Source));
}
