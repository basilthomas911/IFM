using System.Diagnostics.Metrics;
using FluentAssertions;
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
        telemetry.RecordUpload(DatabaseEngine.ScyllaDb, 1024, TimeSpan.FromSeconds(1));
        telemetry.RecordWalLag(TimeSpan.FromSeconds(2));
        telemetry.RecordReplicationLag(DatabaseEngine.ScyllaDb, TimeSpan.FromSeconds(4));
        telemetry.RecordRecoveryObjectives(DatabaseEngine.ScyllaDb, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));
        telemetry.RecordEstimatedCost("storage", 1.25m);
        telemetry.RecordOutboxBacklog(2);
        telemetry.RecordStaleMultipart(1);

        measurements.Distinct().Should().HaveCount(16);
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
}
