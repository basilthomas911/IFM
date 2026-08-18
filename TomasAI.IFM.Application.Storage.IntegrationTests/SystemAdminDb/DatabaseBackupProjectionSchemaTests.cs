using FluentAssertions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.EventProjector;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SystemAdminDb;

public sealed class DatabaseBackupProjectionSchemaTests
{
    static readonly string[] AuthoritativeTableNames =
    [
        "database_recovery_operation",
        "database_recovery_phase",
        "database_recovery_run_stats",
        "database_restore_point",
        "database_artifact_replica",
        "database_recovery_error",
        "database_backup_policy",
        "database_backup_service_health",
        "database_retention_state",
        "database_backup_projection_checkpoint",
        "database_backup_projection_receipt"
    ];

    [Fact]
    public void Schema_contains_all_gate_four_projection_tables_and_revision_fences()
    {
        var tables = new[]
        {
            SystemAdminSchemaSql.CreateRecoveryOperation,
            SystemAdminSchemaSql.CreateRecoveryPhase,
            SystemAdminSchemaSql.CreateRecoveryRunStats,
            SystemAdminSchemaSql.CreateRestorePoint,
            SystemAdminSchemaSql.CreateArtifactReplica,
            SystemAdminSchemaSql.CreateRecoveryError,
            SystemAdminSchemaSql.CreateBackupPolicy,
            SystemAdminSchemaSql.CreateServiceHealth,
            SystemAdminSchemaSql.CreateRetentionState,
            SystemAdminSchemaSql.CreateProjectionCheckpoint
        };

        tables.Should().AllSatisfy(sql => sql.Should().Contain("last_event_id"));
        SystemAdminSchemaSql.CreateProjectionReceipt.Should().Contain("PRIMARY KEY (projector_name, event_id)");
        SystemAdminDbSql.UpsertOperation.Should().Contain("EXCLUDED.state_revision >");
        SystemAdminDbSql.UpsertRestorePoint.Should().Contain("EXCLUDED.source_revision >");
        SystemAdminDbSql.UpsertPolicy.Should().Contain("EXCLUDED.source_revision >");
    }

    [Fact]
    public void Schema_and_queries_use_authoritative_unversioned_table_names()
    {
        string[] tableDefinitions =
        [
            SystemAdminSchemaSql.CreateRecoveryOperation,
            SystemAdminSchemaSql.CreateRecoveryPhase,
            SystemAdminSchemaSql.CreateRecoveryRunStats,
            SystemAdminSchemaSql.CreateRestorePoint,
            SystemAdminSchemaSql.CreateArtifactReplica,
            SystemAdminSchemaSql.CreateRecoveryError,
            SystemAdminSchemaSql.CreateBackupPolicy,
            SystemAdminSchemaSql.CreateServiceHealth,
            SystemAdminSchemaSql.CreateRetentionState,
            SystemAdminSchemaSql.CreateProjectionCheckpoint,
            SystemAdminSchemaSql.CreateProjectionReceipt
        ];
        var queryDefinitions = typeof(SystemAdminDbSql)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(static field => field.FieldType == typeof(string))
            .Select(static field => (string)field.GetValue(null)!)
            .ToArray();

        tableDefinitions.Should().HaveCount(AuthoritativeTableNames.Length);
        tableDefinitions.Should().OnlyContain(static sql => !sql.Contains("_v1", StringComparison.Ordinal));
        queryDefinitions.Should().NotBeEmpty()
            .And.OnlyContain(static sql => !sql.Contains("_v1", StringComparison.Ordinal));
        foreach (var tableName in AuthoritativeTableNames)
            tableDefinitions.Should().Contain(sql => sql.Contains($"system_admin.{tableName}", StringComparison.Ordinal));
    }

    [Fact]
    public void Rebuild_sql_is_projection_scoped_and_never_references_event_source_or_service_journal()
    {
        SystemAdminDbSql.ClearProjections.Should().Contain("system_admin.");
        SystemAdminDbSql.ClearProjections.ToLowerInvariant().Should().NotContain("event_log");
        SystemAdminDbSql.ClearProjections.ToLowerInvariant().Should().NotContain("event_stream");
        SystemAdminDbSql.ClearProjections.ToLowerInvariant().Should().NotContain("journal");
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task PostgreSql_projection_is_idempotent_and_queryable()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_POSTGRES_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=event-source-test-db";
        var settings = new DbConnectionSettings()
            .Add(SystemAdminDbContext.SystemAdminDbConnection, connection, "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var schema = new SystemAdminSchemaDb(settings, logger);
        await schema.CreateAllAsync();
        var db = new SystemAdminDbContext(settings, logger);
        var eventId = DateTime.UtcNow.Ticks;
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var sourceEventId = Guid.NewGuid();
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
            BaseRestorePointId = new DatabaseRestorePointId("gate4-base"),
            ParentRestorePointId = new DatabaseRestorePointId("gate4-parent"),
            ChainDepth = 2,
            NativeIdentity = "gate4-postgresql-system"
        };
        var domainEvent = new DatabaseBackupRequestedDomainEvent
        {
            Id = sourceEventId, EventId = eventId, CommandId = Guid.NewGuid(), EntityId = operationId,
            AggregateId = operationId.Format(), EventSource = "DatabaseBackupCommandActor", ReceivedOn = DateTime.UtcNow,
            BackupLineage = lineage,
            Source = new DatabaseSourceEnvelope
            {
                SourceEventId = sourceEventId, OperationId = operationId, Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new DatabaseProtectionSetId("gate4-core"), PolicyRevision = 7,
                OperationKind = DatabaseRecoveryOperationKind.Backup, Phase = DatabaseRecoveryPhase.Requested,
                CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
            }
        };

        try
        {
            await db.ClearDatabaseBackupProjectionsAsync("Gate4Integration");
            var first = await db.ApplyDatabaseBackupEventAsync("Gate4Integration", domainEvent);
            var duplicate = await db.ApplyDatabaseBackupEventAsync("Gate4Integration", domainEvent);
            var conflictingEvent = domainEvent with { SafeDiagnosticReference = "different-content" };
            Func<Task> applyConflict = async () =>
                await db.ApplyDatabaseBackupEventAsync("Gate4Integration", conflictingEvent);
            var restorePointId = new DatabaseRestorePointId($"gate4-{Guid.NewGuid():N}");
            var verificationSourceEventId = Guid.NewGuid();
            var verification = new DatabaseOperationVerificationRecordedEvent
            {
                Id = verificationSourceEventId, EventId = eventId + 1, CommandId = domainEvent.CommandId,
                EntityId = operationId, AggregateId = operationId.Format(),
                EventSource = domainEvent.EventSource, ReceivedOn = DateTime.UtcNow,
                RestorePointId = restorePointId, VerificationLevel = DatabaseVerificationLevel.Native,
                ManifestRevision = 17, BackupLineage = lineage,
                Source = domainEvent.Source with
                {
                    SourceEventId = verificationSourceEventId,
                    Phase = DatabaseRecoveryPhase.Verifying,
                    ObservedUtc = DateTimeOffset.UtcNow
                }
            };
            await db.ApplyDatabaseBackupEventAsync("Gate4Integration", verification);
            var row = await db.GetBackupOperationAsync(new GetDatabaseBackupOperationQuery
            {
                EntityId = operationId, OperationId = operationId
            }, CancellationToken.None);
            var restorePoint = await db.GetRestorePointAsync(new GetDatabaseRestorePointQuery
            {
                EntityId = operationId, RestorePointId = restorePointId,
                Source = BackupSource.LocalWorkstation
            }, CancellationToken.None);

            first.Should().Be(EventProjectionApplyOutcome.Applied);
            duplicate.Should().Be(EventProjectionApplyOutcome.AlreadyApplied);
            await applyConflict.Should().ThrowAsync<InvalidOperationException>().WithMessage("*conflicts*");
            row.Should().NotBeNull();
            row!.OperationId.Should().Be(operationId);
            row.StateRevision.Should().Be(eventId + 1);
            row.BackupLineage.Should().BeEquivalentTo(lineage);
            restorePoint.Should().NotBeNull();
            restorePoint!.VerificationLevel.Should().Be(DatabaseVerificationLevel.Native);
            restorePoint.ManifestRevision.Should().Be(17);
            restorePoint.BackupLineage.Should().BeEquivalentTo(lineage);
        }
        finally
        {
            await db.ClearDatabaseBackupProjectionsAsync("Gate4Integration");
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task PostgreSql_authoritative_projection_tables_are_all_queryable()
    {
        var connectionString = Environment.GetEnvironmentVariable("IFM_POSTGRES_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=event-source-test-db";
        var settings = new DbConnectionSettings()
            .Add(SystemAdminDbContext.SystemAdminDbConnection, connectionString, "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new SystemAdminSchemaDb(settings, logger).CreateAllAsync();
        var db = new SystemAdminDbContext(settings, logger);

        foreach (var tableName in AuthoritativeTableNames)
        {
            var count = await db.Use($"SELECT COUNT(*) FROM system_admin.{tableName};")
                .ExecuteScalarAsync(static row => row.GetLong(0));
            count.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task PostgreSql_full_rebuild_recreates_latest_operation_state_from_authoritative_events()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_POSTGRES_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=event-source-test-db";
        var settings = new DbConnectionSettings()
            .Add(SystemAdminDbContext.SystemAdminDbConnection, connection, "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new SystemAdminSchemaDb(settings, logger).CreateAllAsync();
        var db = new SystemAdminDbContext(settings, logger);
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var firstRevision = DateTime.UtcNow.Ticks;
        var requested = DomainEvent<DatabaseBackupRequestedDomainEvent>(
            operationId, firstRevision, DatabaseRecoveryPhase.Requested, DatabaseRecoveryOutcome.None);
        var completed = DomainEvent<DatabaseOperationCompletedEvent>(
            operationId, firstRevision + 1, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded);

        try
        {
            var result = await new DatabaseBackupProjectionRebuilder(db).RebuildAsync([completed, requested]);
            var row = await db.GetBackupOperationAsync(new GetDatabaseBackupOperationQuery
            {
                EntityId = operationId, OperationId = operationId
            }, CancellationToken.None);

            result.Should().Be(new DatabaseBackupProjectionRebuildResult(2, 0, 0, firstRevision + 1));
            row.Should().NotBeNull();
            row!.Phase.Should().Be(DatabaseRecoveryPhase.Completed);
            row.Outcome.Should().Be(DatabaseRecoveryOutcome.Succeeded);
        }
        finally
        {
            await db.ClearDatabaseBackupProjectionsAsync(nameof(DatabaseBackupEventProjector));
        }
    }

    static TEvent DomainEvent<TEvent>(
        DatabaseRecoveryOperationId operationId,
        long eventId,
        DatabaseRecoveryPhase phase,
        DatabaseRecoveryOutcome outcome)
        where TEvent : DatabaseBackupEventContract, new()
    {
        var id = Guid.NewGuid();
        return (TEvent)(new TEvent() with
        {
            Id = id, EventId = eventId, CommandId = Guid.NewGuid(), EntityId = operationId,
            AggregateId = operationId.Format(), EventSource = "DatabaseBackupCommandActor", ReceivedOn = DateTime.UtcNow,
            Outcome = outcome,
            Source = new DatabaseSourceEnvelope
            {
                SourceEventId = id, OperationId = operationId, Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new DatabaseProtectionSetId("gate4-rebuild"), PolicyRevision = 2,
                OperationKind = DatabaseRecoveryOperationKind.Backup, Phase = phase,
                CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
            }
        });
    }
}
