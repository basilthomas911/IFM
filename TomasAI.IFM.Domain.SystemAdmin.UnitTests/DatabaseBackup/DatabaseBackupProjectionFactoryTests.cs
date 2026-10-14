using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Repository;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;
public sealed class DatabaseBackupProjectionFactoryTests
{
    static DatabaseBackupEventContract Source(DatabaseBackupEventContract source)
    {
        var id = Guid.NewGuid(); var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        return source with
        {
            Id = id, EventId = 17, EntityId = operation,
            Source = new()
            {
                SourceEventId = id, OperationId = operation, Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new("isolated"), PolicyRevision = 1, OperationKind = DatabaseRecoveryOperationKind.Backup,
                Phase = DatabaseRecoveryPhase.Completed, CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
            }
        };
    }
    [Fact]
    public void Factory_preserves_receipt_hash_and_terminal_observation_facts()
    {
        var source = Source(new DatabaseOperationCompletedEvent()) with { Outcome = DatabaseRecoveryOutcome.Succeeded, VerificationLevel = DatabaseVerificationLevel.Native };
        var snapshot = DatabaseBackupProjectionFactory.Create(source);
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(source, source.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)))));
        Assert.Equal(expected, snapshot.EventHash);
        Assert.Equal(source.Source.ObservedUtc.UtcDateTime, snapshot.CompletedUtc);
        Assert.Equal(source.Source.ObservedUtc.UtcDateTime, snapshot.VerifiedUtc);
        Assert.True(snapshot.WriteOperation); Assert.True(snapshot.Eligible);
        Assert.False(snapshot.RestoreTested);
        var restored = DatabaseBackupProjectionFactory.Create(source with { Source = source.Source with { OperationKind = DatabaseRecoveryOperationKind.RestoreDrill } });
        Assert.True(restored.RestoreTested);
    }
    [Fact]
    public void Legal_hold_and_policy_events_do_not_create_operation_rows()
    {
        var hold = DatabaseBackupProjectionFactory.Create(Source(new DatabaseBackupLegalHoldPlacedEvent()));
        Assert.False(hold.WriteOperation); Assert.True(hold.LegalHold);
        var release = DatabaseBackupProjectionFactory.Create(Source(new DatabaseBackupLegalHoldReleasedEvent()));
        Assert.False(release.WriteOperation); Assert.False(release.LegalHold);
        var policy = DatabaseBackupProjectionFactory.Create(Source(new DatabaseBackupPolicyEnforcedEvent()));
        Assert.False(policy.WriteOperation); Assert.True(policy.Enforced);
    }
    [Fact]
    public void Failure_and_host_health_routing_are_domain_owned()
    {
        var failed = DatabaseBackupProjectionFactory.Create(Source(new DatabaseOperationFailedEvent()) with { Outcome = DatabaseRecoveryOutcome.Failed });
        Assert.True(failed.WriteError); Assert.False(failed.Eligible);
        var source = Source(new DatabaseBackupServiceReconciledEvent()) with { CapabilityState = DatabaseServiceCapabilityState.Ready };
        source = source with { Source = source.Source with { ProducingHostId = new("host") } };
        var health = DatabaseBackupProjectionFactory.Create(source);
        Assert.False(health.WriteOperation); Assert.True(health.WriteServiceHealth); Assert.True(health.Ready); Assert.True(health.Reconciled);
    }
    [Fact]
    public void Unpersisted_event_is_rejected_before_storage_is_called()
    {
        var source = Source(new DatabaseBackupRequestedDomainEvent()) with { EventId = 0 };
        Assert.Throws<ArgumentException>(() => DatabaseBackupProjectionFactory.Create(source));
    }
    [Fact]
    public void Backup_set_completeness_is_composed_from_all_operation_facts()
    {
        var query = new GetDatabaseBackupSetQuery { BackupSetId = new(Guid.NewGuid()) };
        var completed = new DatabaseBackupOperationReadModel { OperationId = new(Guid.NewGuid()), Outcome = DatabaseRecoveryOutcome.Succeeded, StateRevision = 2 };
        var pending = completed with { OperationId = new(Guid.NewGuid()), Outcome = DatabaseRecoveryOutcome.None, StateRevision = 3 };
        var model = GetDatabaseBackupSet.BuildReadModel(query, [completed, pending]);
        Assert.NotNull(model); Assert.False(model.Complete); Assert.Equal(1, model.CompletedOperationCount); Assert.Equal(2, model.RequiredOperationCount); Assert.Equal(3, model.Revision);
        Assert.Null(GetDatabaseBackupSet.BuildReadModel(query, []));
    }
    [Fact]
    public void Run_statistics_retains_all_observations_and_uses_latest_revision()
    {
        var query = new GetDatabaseRecoveryRunStatsQuery { OperationId = new(Guid.NewGuid()) };
        var model = GetDatabaseRecoveryRunStats.BuildReadModel(query,
            [new(BackupSource.LocalWorkstation, 2, new()), new(BackupSource.LocalWorkstation, 5, new())]);
        Assert.NotNull(model); Assert.Equal(5, model.StatisticsRevision); Assert.Equal(2, model.Statistics.Length);
        Assert.Null(GetDatabaseRecoveryRunStats.BuildReadModel(query, []));
    }
}
