using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct BackupSetKey(Guid BackupSetId) : IBindValue
{
    public object Bind() => Values(Uuid(BackupSetId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertPhaseParameter(DatabaseBackupProjectionSnapshot Event) : IBindValue
{
    public object Bind() => Values(Uuid(Event.Source.OperationId.Value), Smallint((short)Event.Source.Phase), Bigint(Event.EventId), Smallint((short)Event.Outcome), Integer(Event.ProgressPercent), TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Text(Event.Source.ProducingHostId?.Value), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertProjectionReceiptParameter(string ProjectorName, long EventId, string EventHash, Guid SourceEventId, DateTime AppliedUtc) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId), Text(EventHash), Uuid(SourceEventId), TimestampTz(AppliedUtc));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertRunStatisticsParameter(DatabaseBackupProjectionSnapshot Event) : IBindValue
{
    public object Bind()
    {
        var stats = Event.Statistics!;
        return Values(Uuid(Event.Source.OperationId.Value), Smallint((short)Event.Source.Source), Smallint((short)stats.Phase), Smallint((short)stats.Engine), Bigint(Event.EventId), TimestampTz(stats.StartedUtc?.UtcDateTime), TimestampTz(stats.CompletedUtc?.UtcDateTime), Bigint(stats.Elapsed?.Ticks), Bigint(stats.SourceBytes), Bigint(stats.StoredBytes), Bigint(stats.TransferredBytes), Bigint(stats.RestoredBytes), Integer(stats.ArtifactCount), Double(stats.AverageThroughputBytesPerSecond), Double(stats.PeakThroughputBytesPerSecond), Integer(stats.RetryCount), Integer(stats.WarningCount), Bigint(stats.AchievedRpo?.Ticks), Bigint(stats.AchievedRto?.Ticks), Text(Event.Source.ProducingHostId?.Value), Bigint(Event.Source.PolicyRevision), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct LatestRestorePointKey(BackupSource Source, string ProtectionSetId) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OperationKey(Guid OperationId) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct OperationListParameter(BackupSource Source, string? ProtectionSetId, DateTime? FromUtc, DateTime? ToUtc, Guid? Continuation, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId), TimestampTz(FromUtc), TimestampTz(ToUtc), Uuid(Continuation), Integer(PageSize));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PhasePageParameter(Guid OperationId, long AfterRevision, int PageSize) : IBindValue
{
    public object Bind() => Values(Uuid(OperationId), Bigint(AfterRevision), Integer(PageSize));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct PolicyQueryParameter(string Environment, string PolicyId) : IBindValue
{
    public object Bind() => Values(Text(Environment), Text(PolicyId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectionKey(string ProjectorName, long EventId) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ProjectorKey(string ProjectorName) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RestoreDrillListParameter(BackupSource Source, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Integer(PageSize));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RestorePointKey(string RestorePointId, BackupSource Source) : IBindValue
{
    public object Bind() => Values(Text(RestorePointId), Smallint((short)Source));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RestorePointListParameter(BackupSource Source, string? ProtectionSetId, DateTime? FromUtc, DateTime? ToUtc, string? Continuation, int PageSize) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Text(ProtectionSetId), TimestampTz(FromUtc), TimestampTz(ToUtc), Text(Continuation), Integer(PageSize));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RetentionQueryParameter(BackupSource Source, Guid? PlanId) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source), Uuid(PlanId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ServiceHealthQueryParameter(string Environment, BackupSource Source) : IBindValue
{
    public object Bind() => Values(Text(Environment), Smallint((short)Source));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct SourceFilter(BackupSource Source) : IBindValue
{
    public object Bind() => Values(Smallint((short)Source));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertArtifactReplicaParameter(DatabaseBackupProjectionSnapshot Event) : IBindValue
{
    public object Bind()
    {
        var replica = Event.ArtifactReplica!;
        return Values(Text(replica.ReplicaId.Value), Smallint((short)Event.Source.Source), Uuid(Event.Source.OperationId.Value), Text(replica.ArtifactId.Value), Smallint((short)replica.Engine), Smallint((short)replica.State), Text(replica.SafeDestinationReference), Bigint(replica.Bytes), Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertOperationParameter(DatabaseBackupProjectionSnapshot Event) : IBindValue
{
    public object Bind()
    {
        var e = Event;
        return Values(Uuid(e.Source.OperationId.Value), Uuid(e.Source.BackupSetId?.Value), Text(e.Source.ProtectionSetId.Value), Smallint((short)e.Source.Source), Smallint((short)e.Source.OperationKind), Smallint((short)e.Source.Phase), Smallint((short)e.Outcome), Integer(e.ProgressPercent), Bigint(e.EventId), TimestampTz(e.Source.ObservedUtc.UtcDateTime), TimestampTz(e.CompletedUtc), Text(e.SafeDiagnosticReference), Text(e.RestorePointId?.Value), Smallint((short)e.RestoreClass), Text(e.FreshTarget?.Profile ?? string.Empty), Bigint(e.ValidationRevision), Smallint((short)e.CutoverState), Bigint(e.Source.PolicyRevision), Text(e.BackupLineage.SerializeLineage()), Bigint(e.EventId), Uuid(e.Source.SourceEventId));
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertPolicyParameter(DatabaseBackupProjectionSnapshot Event, string DefinitionJson, bool Enforced) : IBindValue
{
    public object Bind() => Values(Text(Event.Request?.EnvironmentIdentity ?? string.Empty), Text(Event.PolicyId!.Value.Value), Bigint(Event.Source.PolicyRevision), Text(DefinitionJson), Boolean(Enforced), Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertProjectionCheckpointParameter(string ProjectorName, long EventId, DateTime UpdatedUtc) : IBindValue
{
    public object Bind() => Values(Text(ProjectorName), Bigint(EventId), TimestampTz(UpdatedUtc));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRecoveryErrorParameter(DatabaseBackupProjectionSnapshot Event) : IBindValue
{
    public object Bind() => Values(Uuid(Event.Source.OperationId.Value), Uuid(Event.Source.SourceEventId), Smallint((short)Event.ErrorClassification), Text(Event.SafeDiagnosticReference), TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRestorePointParameter(DatabaseBackupProjectionSnapshot Event, bool Eligible, bool LegalHold, bool RestoreTested) : IBindValue
{
    public object Bind() => Values(Text(Event.RestorePointId!.Value.Value), Smallint((short)Event.Source.Source), Uuid(Event.Source.BackupSetId?.Value), Text(Event.Source.ProtectionSetId.Value), TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Smallint((short)Event.VerificationLevel), TimestampTz(Event.VerifiedUtc), TimestampTz(RestoreTested ? Event.Source.ObservedUtc.UtcDateTime : null), Boolean(Eligible), Boolean(LegalHold), Bigint(Event.ManifestRevision), Text(Event.BackupLineage.SerializeLineage()), Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertRetentionParameter(DatabaseBackupProjectionSnapshot Event, string RetainJson, string DeleteJson, bool Approved) : IBindValue
{
    public object Bind() => Values(Uuid(Event.RetentionPlanId!.Value.Value), Smallint((short)Event.Source.Source), Bigint(Event.RetentionPlanRevision), TimestampTz(Event.EvaluationBoundaryUtc.UtcDateTime), Text(RetainJson), Text(DeleteJson), Boolean(Approved), Smallint((short)Event.Outcome), Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertServiceHealthParameter(DatabaseBackupProjectionSnapshot Event, bool Reconciled) : IBindValue
{
    public object Bind() => Values(Text(Event.Request?.EnvironmentIdentity ?? "system"), Smallint((short)Event.Source.Source), Text(Event.Source.ProducingHostId!.Value.Value), Smallint((short)Event.CapabilityState), Boolean(Event.Ready), Bigint(Event.Source.SourceRevisionOrSequence), TimestampTz(Event.Source.ObservedUtc.UtcDateTime), Text(Event.SafeDiagnosticReference), Boolean(Reconciled), Bigint(Event.EventId), Bigint(Event.EventId), Uuid(Event.Source.SourceEventId));
}

internal readonly record struct BackupExecutionIdentity(long EventId, Guid EventUuid, DateTime? PublishedUtc = null) : IBindValue
{
    public object Bind() => PublishedUtc is null ? Values(Bigint(EventId), Uuid(EventUuid)) : Values(TimestampTz(PublishedUtc), Bigint(EventId), Uuid(EventUuid));
}