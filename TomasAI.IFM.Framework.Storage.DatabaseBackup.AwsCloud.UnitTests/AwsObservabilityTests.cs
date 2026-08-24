using System.Diagnostics.Metrics;
using Amazon.CloudWatch;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Observability;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsObservabilityTests
{
    [Theory]
    [InlineData(false, false, 0, 0, AwsBackupOperationalState.Disabled)]
    [InlineData(true, false, 0, 0, AwsBackupOperationalState.Unavailable)]
    [InlineData(true, false, 0, 1, AwsBackupOperationalState.Failed)]
    [InlineData(true, true, 0, 1, AwsBackupOperationalState.Degraded)]
    [InlineData(true, true, 1, 0, AwsBackupOperationalState.Pending)]
    [InlineData(true, true, 0, 0, AwsBackupOperationalState.Ready)]
    [Trait("Category", "Gate15")]
    public void Operator_state_distinguishes_disabled_unavailable_degraded_pending_failed_and_ready(
        bool enabled,
        bool ready,
        int pending,
        int failed,
        AwsBackupOperationalState expected)
    {
        AwsBackupOperationalStatePolicy.Resolve(enabled, ready, pending, failed).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Gate15")]
    public void Required_metrics_are_emitted_with_bounded_tags()
    {
        var measurements = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == AwsDatabaseBackupTelemetry.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.SetMeasurementEventCallback<double>((instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.Start();
        using var telemetry = new AwsDatabaseBackupTelemetry();

        telemetry.RecordRuntimeFailure("dependency");
        telemetry.RecordJournalConflict("lease");
        telemetry.RecordWalGap();
        telemetry.RecordReplicationFailure("timeout");
        telemetry.RecordKmsDenial("decrypt");
        telemetry.RecordRestoreVerification(DatabaseEngine.ScyllaDb, true);
        telemetry.RecordRetentionDrift("legal-hold");
        telemetry.RecordPhaseAge(DatabaseEngine.PostgreSql, "verifying", TimeSpan.FromSeconds(3));
        telemetry.RecordIntentAge(TimeSpan.FromSeconds(5));
        telemetry.RecordUpload(DatabaseEngine.ScyllaDb, 1024, TimeSpan.FromSeconds(1));
        telemetry.RecordWalLag(TimeSpan.FromSeconds(2));
        telemetry.RecordReplicationLag(DatabaseEngine.ScyllaDb, TimeSpan.FromSeconds(4));
        telemetry.RecordRecoveryObjectives(DatabaseEngine.ScyllaDb, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
        telemetry.RecordEstimatedCost("storage", 1.25m);
        telemetry.RecordOutboxBacklog(2);
        telemetry.RecordStaleMultipart(1);

        measurements.Distinct().Should().HaveCount(17);
        measurements.Should().Contain("ifm.database_backup.aws.replication.lag");
        measurements.Should().Contain("ifm.database_backup.aws.retention.drift");
        measurements.Should().Contain("ifm.database_backup.aws.estimated_cost");
    }

    [Fact]
    [Trait("Category", "Gate15")]
    public void High_cardinality_or_unbounded_metric_tag_is_rejected()
    {
        using var telemetry = new AwsDatabaseBackupTelemetry();

        var action = () => telemetry.RecordRuntimeFailure("operation/11111111-1111-1111-1111-111111111111");

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Gate15")]
    public void CloudWatch_export_uses_fixed_namespace_environment_and_low_cardinality_dimensions()
    {
        var options = ValidOptions();
        var buffer = new AwsCloudWatchMetricBuffer(1000);
        using var telemetry = new AwsDatabaseBackupTelemetry(buffer);
        telemetry.RecordRestoreVerification(DatabaseEngine.ScyllaDb, true);
        var exporter = new AwsCloudWatchMetricExporter(Substitute.For<IAmazonCloudWatch>(), options, buffer);

        var request = exporter.CreateRequest(buffer.Take(1000));

        request.Namespace.Should().Be("IFM/DatabaseBackup");
        request.MetricData.Should().HaveCount(2);
        request.MetricData[0].MetricName.Should().Be("ifm.database_backup.aws.restore.verifications");
        request.MetricData[0].Dimensions.Should().BeEquivalentTo([
            new Amazon.CloudWatch.Model.Dimension { Name = "engine", Value = "scylladb" },
            new Amazon.CloudWatch.Model.Dimension { Name = "environment", Value = "development" },
            new Amazon.CloudWatch.Model.Dimension { Name = "outcome", Value = "succeeded" }
        ]);
        request.MetricData[1].Dimensions.Should().BeEquivalentTo([
            new Amazon.CloudWatch.Model.Dimension { Name = "environment", Value = "development" }
        ]);
    }

    [Fact]
    [Trait("Category", "Gate15")]
    public void CloudWatch_metric_buffer_is_bounded_and_returns_failed_batches()
    {
        var buffer = new AwsCloudWatchMetricBuffer(1000);
        var dimensions = new Dictionary<string, string>();
        for (var index = 0; index < 1001; index++)
            buffer.Record(new AwsCloudWatchMetricSample("metric", index, StandardUnit.Count, DateTime.UtcNow, dimensions));

        buffer.DroppedCount.Should().Be(1);
        var first = buffer.Take(1000);
        first.Should().HaveCount(1000);
        buffer.Return(first);
        buffer.Take(1000).Should().HaveCount(1000);
    }

    static AwsCloudDatabaseBackupOptions ValidOptions() => new()
    {
        Enabled = true,
        Environment = AwsBackupEnvironment.Development,
        WorkloadAccountId = "107651266250",
        PrimaryVaultAccountId = "107651266250",
        RecoveryVaultAccountId = "107651266250",
        PrimaryRegion = "ca-central-1",
        RecoveryRegion = "ca-west-1",
        PrimaryBucketName = "ifm-primary-development",
        RecoveryBucketName = "ifm-recovery-development",
        JournalTableName = "ifm-database-backup-journal-development",
        UploadRoleArn = "arn:aws:iam::107651266250:role/upload",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/recovery",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/primary",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/recovery",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/signing",
        WalSpoolPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ifm-wal"))
    };
}
