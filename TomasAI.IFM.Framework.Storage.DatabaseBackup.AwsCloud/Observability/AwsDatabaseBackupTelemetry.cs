using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

public enum AwsBackupOperationalState
{
    Disabled = 0,
    Unavailable = 1,
    Degraded = 2,
    Pending = 3,
    Failed = 4,
    Ready = 5
}

public static class AwsBackupOperationalStatePolicy
{
    public static AwsBackupOperationalState Resolve(
        bool enabled,
        bool ready,
        int pendingOperations,
        int failedOperations)
    {
        if (pendingOperations < 0 || failedOperations < 0)
            throw new ArgumentOutOfRangeException(nameof(pendingOperations));
        if (!enabled) return AwsBackupOperationalState.Disabled;
        if (!ready) return failedOperations > 0 ? AwsBackupOperationalState.Failed : AwsBackupOperationalState.Unavailable;
        if (failedOperations > 0) return AwsBackupOperationalState.Degraded;
        if (pendingOperations > 0) return AwsBackupOperationalState.Pending;
        return AwsBackupOperationalState.Ready;
    }
}

public sealed class AwsDatabaseBackupTelemetry : IDisposable
{
    public const string MeterName = "TomasAI.IFM.DatabaseBackup.AwsCloud";
    readonly Meter _meter = new(MeterName);
    readonly Counter<long> _runtimeFailures;
    readonly Counter<long> _journalConflicts;
    readonly Counter<long> _walGaps;
    readonly Counter<long> _replicationFailures;
    readonly Counter<long> _kmsDenials;
    readonly Counter<long> _restoreVerifications;
    readonly Counter<long> _retentionDrift;
    readonly Histogram<double> _phaseAge;
    readonly Histogram<double> _uploadThroughput;
    readonly Histogram<double> _walLag;
    readonly Histogram<double> _replicationLag;
    readonly Histogram<double> _rpo;
    readonly Histogram<double> _rto;
    readonly Histogram<double> _estimatedCost;
    readonly Histogram<long> _outboxBacklog;
    readonly Histogram<long> _staleMultipart;

    public AwsDatabaseBackupTelemetry()
    {
        _runtimeFailures = _meter.CreateCounter<long>("ifm.database_backup.aws.runtime.failures", "{failure}");
        _journalConflicts = _meter.CreateCounter<long>("ifm.database_backup.aws.journal.conflicts", "{conflict}");
        _walGaps = _meter.CreateCounter<long>("ifm.database_backup.aws.wal.gaps", "{gap}");
        _replicationFailures = _meter.CreateCounter<long>("ifm.database_backup.aws.replication.failures", "{failure}");
        _kmsDenials = _meter.CreateCounter<long>("ifm.database_backup.aws.kms.denials", "{denial}");
        _restoreVerifications = _meter.CreateCounter<long>("ifm.database_backup.aws.restore.verifications", "{verification}");
        _retentionDrift = _meter.CreateCounter<long>("ifm.database_backup.aws.retention.drift", "{drift}");
        _phaseAge = _meter.CreateHistogram<double>("ifm.database_backup.aws.operation.phase_age", "s");
        _uploadThroughput = _meter.CreateHistogram<double>("ifm.database_backup.aws.upload.throughput", "By/s");
        _walLag = _meter.CreateHistogram<double>("ifm.database_backup.aws.wal.lag", "s");
        _replicationLag = _meter.CreateHistogram<double>("ifm.database_backup.aws.replication.lag", "s");
        _rpo = _meter.CreateHistogram<double>("ifm.database_backup.aws.rpo", "s");
        _rto = _meter.CreateHistogram<double>("ifm.database_backup.aws.rto", "s");
        _estimatedCost = _meter.CreateHistogram<double>("ifm.database_backup.aws.estimated_cost", "USD");
        _outboxBacklog = _meter.CreateHistogram<long>("ifm.database_backup.aws.outbox.backlog", "{event}");
        _staleMultipart = _meter.CreateHistogram<long>("ifm.database_backup.aws.multipart.stale", "{upload}");
    }

    public void RecordRuntimeFailure(string category) => _runtimeFailures.Add(1, Tag("category", category));
    public void RecordJournalConflict(string category) => _journalConflicts.Add(1, Tag("category", category));
    public void RecordWalGap() => _walGaps.Add(1);
    public void RecordReplicationFailure(string category) => _replicationFailures.Add(1, Tag("category", category));
    public void RecordKmsDenial(string operation) => _kmsDenials.Add(1, Tag("operation", operation));
    public void RecordRestoreVerification(DatabaseEngine engine, bool succeeded)
        => _restoreVerifications.Add(1, EngineTag(engine), new("outcome", succeeded ? "succeeded" : "failed"));
    public void RecordRetentionDrift(string category) => _retentionDrift.Add(1, Tag("category", category));
    public void RecordPhaseAge(DatabaseEngine engine, string phase, TimeSpan age)
        => _phaseAge.Record(Seconds(age), EngineTag(engine), Tag("phase", phase));
    public void RecordUpload(DatabaseEngine engine, long bytes, TimeSpan elapsed)
        => _uploadThroughput.Record(elapsed > TimeSpan.Zero ? bytes / elapsed.TotalSeconds : 0, EngineTag(engine));
    public void RecordWalLag(TimeSpan lag) => _walLag.Record(Seconds(lag));
    public void RecordReplicationLag(DatabaseEngine engine, TimeSpan lag)
        => _replicationLag.Record(Seconds(lag), EngineTag(engine));
    public void RecordRecoveryObjectives(DatabaseEngine engine, TimeSpan rpo, TimeSpan rto)
    {
        _rpo.Record(Seconds(rpo), EngineTag(engine));
        _rto.Record(Seconds(rto), EngineTag(engine));
    }
    public void RecordEstimatedCost(string category, decimal usd)
        => _estimatedCost.Record((double)Math.Max(0, usd), Tag("category", category));
    public void RecordOutboxBacklog(long count) => _outboxBacklog.Record(Math.Max(0, count));
    public void RecordStaleMultipart(long count) => _staleMultipart.Record(Math.Max(0, count));

    static KeyValuePair<string, object?> EngineTag(DatabaseEngine engine)
        => new("engine", engine == DatabaseEngine.ScyllaDb ? "scylladb" : "postgresql");

    static KeyValuePair<string, object?> Tag(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
            throw new ArgumentException("AWS backup metric tags must be bounded low-cardinality values.", nameof(value));
        return new(key, value.ToLowerInvariant());
    }

    static double Seconds(TimeSpan value)
    {
        if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value));
        return value.TotalSeconds;
    }

    public void Dispose() => _meter.Dispose();
}
