using System.Diagnostics.Metrics;
using Amazon.CloudWatch;
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
    readonly Histogram<double> _intentAge;
    readonly Histogram<double> _uploadThroughput;
    readonly Histogram<double> _walLag;
    readonly Histogram<double> _replicationLag;
    readonly Histogram<double> _rpo;
    readonly Histogram<double> _rto;
    readonly Histogram<double> _estimatedCost;
    readonly Histogram<long> _outboxBacklog;
    readonly Histogram<long> _staleMultipart;
    readonly AwsCloudWatchMetricBuffer? _cloudWatch;

    public AwsDatabaseBackupTelemetry(AwsCloudWatchMetricBuffer? cloudWatch = null)
    {
        _cloudWatch = cloudWatch;
        _runtimeFailures = _meter.CreateCounter<long>("ifm.database_backup.aws.runtime.failures", "{failure}");
        _journalConflicts = _meter.CreateCounter<long>("ifm.database_backup.aws.journal.conflicts", "{conflict}");
        _walGaps = _meter.CreateCounter<long>("ifm.database_backup.aws.wal.gaps", "{gap}");
        _replicationFailures = _meter.CreateCounter<long>("ifm.database_backup.aws.replication.failures", "{failure}");
        _kmsDenials = _meter.CreateCounter<long>("ifm.database_backup.aws.kms.denials", "{denial}");
        _restoreVerifications = _meter.CreateCounter<long>("ifm.database_backup.aws.restore.verifications", "{verification}");
        _retentionDrift = _meter.CreateCounter<long>("ifm.database_backup.aws.retention.drift", "{drift}");
        _phaseAge = _meter.CreateHistogram<double>("ifm.database_backup.aws.operation.phase_age", "s");
        _intentAge = _meter.CreateHistogram<double>("ifm.database_backup.aws.intent.age", "s");
        _uploadThroughput = _meter.CreateHistogram<double>("ifm.database_backup.aws.upload.throughput", "By/s");
        _walLag = _meter.CreateHistogram<double>("ifm.database_backup.aws.wal.lag", "s");
        _replicationLag = _meter.CreateHistogram<double>("ifm.database_backup.aws.replication.lag", "s");
        _rpo = _meter.CreateHistogram<double>("ifm.database_backup.aws.rpo", "s");
        _rto = _meter.CreateHistogram<double>("ifm.database_backup.aws.rto", "s");
        _estimatedCost = _meter.CreateHistogram<double>("ifm.database_backup.aws.estimated_cost", "USD");
        _outboxBacklog = _meter.CreateHistogram<long>("ifm.database_backup.aws.outbox.backlog", "{event}");
        _staleMultipart = _meter.CreateHistogram<long>("ifm.database_backup.aws.multipart.stale", "{upload}");
    }

    public void RecordRuntimeFailure(string category)
    {
        var tag = Tag("category", category);
        _runtimeFailures.Add(1, tag);
        RecordCloudWatch("ifm.database_backup.aws.runtime.failures", 1, StandardUnit.Count, tag);
    }

    public void RecordJournalConflict(string category)
    {
        var tag = Tag("category", category);
        _journalConflicts.Add(1, tag);
        RecordCloudWatch("ifm.database_backup.aws.journal.conflicts", 1, StandardUnit.Count, tag);
    }

    public void RecordWalGap()
    {
        _walGaps.Add(1);
        RecordCloudWatch("ifm.database_backup.aws.wal.gaps", 1, StandardUnit.Count);
    }

    public void RecordReplicationFailure(string category)
    {
        var tag = Tag("category", category);
        _replicationFailures.Add(1, tag);
        RecordCloudWatch("ifm.database_backup.aws.replication.failures", 1, StandardUnit.Count, tag);
    }

    public void RecordKmsDenial(string operation)
    {
        var tag = Tag("operation", operation);
        _kmsDenials.Add(1, tag);
        RecordCloudWatch("ifm.database_backup.aws.kms.denials", 1, StandardUnit.Count, tag);
    }

    public void RecordRestoreVerification(DatabaseEngine engine, bool succeeded)
    {
        var engineTag = EngineTag(engine);
        var outcomeTag = new KeyValuePair<string, object?>("outcome", succeeded ? "succeeded" : "failed");
        _restoreVerifications.Add(1, engineTag, outcomeTag);
        RecordCloudWatch("ifm.database_backup.aws.restore.verifications", 1, StandardUnit.Count, engineTag, outcomeTag);
    }

    public void RecordRetentionDrift(string category)
    {
        var tag = Tag("category", category);
        _retentionDrift.Add(1, tag);
        RecordCloudWatch("ifm.database_backup.aws.retention.drift", 1, StandardUnit.Count, tag);
    }

    public void RecordPhaseAge(DatabaseEngine engine, string phase, TimeSpan age)
    {
        var engineTag = EngineTag(engine);
        var phaseTag = Tag("phase", phase);
        var seconds = Seconds(age);
        _phaseAge.Record(seconds, engineTag, phaseTag);
        RecordCloudWatch("ifm.database_backup.aws.operation.phase_age", seconds, StandardUnit.Seconds, engineTag, phaseTag);
    }

    public void RecordIntentAge(TimeSpan age)
    {
        var seconds = Seconds(age);
        _intentAge.Record(seconds);
        RecordCloudWatch("ifm.database_backup.aws.intent.age", seconds, StandardUnit.Seconds);
    }

    public void RecordUpload(DatabaseEngine engine, long bytes, TimeSpan elapsed)
    {
        var engineTag = EngineTag(engine);
        var throughput = elapsed > TimeSpan.Zero ? bytes / elapsed.TotalSeconds : 0;
        _uploadThroughput.Record(throughput, engineTag);
        RecordCloudWatch("ifm.database_backup.aws.upload.throughput", throughput, StandardUnit.BytesSecond, engineTag);
    }

    public void RecordWalLag(TimeSpan lag)
    {
        var seconds = Seconds(lag);
        _walLag.Record(seconds);
        RecordCloudWatch("ifm.database_backup.aws.wal.lag", seconds, StandardUnit.Seconds);
    }

    public void RecordReplicationLag(DatabaseEngine engine, TimeSpan lag)
    {
        var engineTag = EngineTag(engine);
        var seconds = Seconds(lag);
        _replicationLag.Record(seconds, engineTag);
        RecordCloudWatch("ifm.database_backup.aws.replication.lag", seconds, StandardUnit.Seconds, engineTag);
    }

    public void RecordRecoveryObjectives(DatabaseEngine engine, TimeSpan rpo, TimeSpan rto)
    {
        var engineTag = EngineTag(engine);
        var rpoSeconds = Seconds(rpo);
        var rtoSeconds = Seconds(rto);
        _rpo.Record(rpoSeconds, engineTag);
        _rto.Record(rtoSeconds, engineTag);
        RecordCloudWatch("ifm.database_backup.aws.rpo", rpoSeconds, StandardUnit.Seconds, engineTag);
        RecordCloudWatch("ifm.database_backup.aws.rto", rtoSeconds, StandardUnit.Seconds, engineTag);
    }

    public void RecordEstimatedCost(string category, decimal usd)
    {
        var tag = Tag("category", category);
        var value = (double)Math.Max(0, usd);
        _estimatedCost.Record(value, tag);
        RecordCloudWatch("ifm.database_backup.aws.estimated_cost", value, StandardUnit.None, tag);
    }

    public void RecordOutboxBacklog(long count)
    {
        var value = Math.Max(0, count);
        _outboxBacklog.Record(value);
        RecordCloudWatch("ifm.database_backup.aws.outbox.backlog", value, StandardUnit.Count);
    }

    public void RecordStaleMultipart(long count)
    {
        var value = Math.Max(0, count);
        _staleMultipart.Record(value);
        RecordCloudWatch("ifm.database_backup.aws.multipart.stale", value, StandardUnit.Count);
    }

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

    void RecordCloudWatch(
        string name,
        double value,
        StandardUnit unit,
        params KeyValuePair<string, object?>[] tags)
    {
        if (_cloudWatch is null) return;
        var dimensions = tags.ToDictionary(
            static tag => tag.Key,
            static tag => Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            StringComparer.Ordinal);
        _cloudWatch.Record(new AwsCloudWatchMetricSample(
            name, value, unit, DateTime.UtcNow, dimensions));
    }

    public void Dispose() => _meter.Dispose();
}
