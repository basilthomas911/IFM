using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Repository;
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SystemAdminDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresBackupAtomicTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Backup_source_rows_receipt_and_execution_intent_commit_or_rollback_together(bool failBeforeCommit)
    {
        var settings = new TomasAI.IFM.Shared.Storage.DbConnectionSettings().Add(SystemAdminDbContext.SystemAdminDbConnection, fixture.Connection, "System.Data.Postgres");
        await new SystemAdminSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var db = new SystemAdminDbContext(settings, NullLogger<DbProvider>.Instance);
        var id = Guid.NewGuid();
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var source = new DatabaseBackupExecutionRequestedDomainEvent { Id = id, CommandId = Guid.NewGuid(), EntityId = operation,
            AggregateId = operation.Format(), EventSource = "PostgresBackupAtomicTest", ReceivedOn = DateTime.UtcNow,
            Source = new() { SourceEventId = id, OperationId = operation, Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new("isolated-atomic-fixture"), PolicyRevision = 1, OperationKind = DatabaseRecoveryOperationKind.Backup,
                Phase = DatabaseRecoveryPhase.Requested, CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow } };
        var stream = "backup-atomic-" + id;
        Func<Task> save = async () => await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(stream, source.CommandId, source, 0, token);
            await db.ApplyDatabaseBackupSnapshotAsync(transaction, "PostgresBackupAtomicTest", DatabaseBackupProjectionFactory.Create(source), token);
            await DatabaseBackupExecutionIntentRepository.EnqueueAsync(db, transaction, source, token);
            if (failBeforeCommit) throw new InvalidOperationException("Injected after backup rows and outbox before commit.");
            return true;
        });
        if (failBeforeCommit) await save.Should().ThrowAsync<InvalidOperationException>();
        else await save();
        (await fixture.EventCountAsync(stream)).Should().Be(failBeforeCommit ? 0 : 1);
        var operationRows = await fixture.Transactions.ExecuteOnceAsync((transaction, token) => transaction.ScalarAsync(
            "SELECT count(*) FROM system_admin.database_recovery_operation WHERE operation_id=$1;", [operation.Value], token));
        operationRows.Should().Be(failBeforeCommit ? 0L : 1L);
        var receiptRows = await fixture.Transactions.ExecuteOnceAsync((transaction, token) => transaction.ScalarAsync(
            "SELECT count(*) FROM system_admin.database_backup_projection_receipt WHERE projector_name=$1 AND event_id=$2;", ["PostgresBackupAtomicTest", source.EventId], token));
        receiptRows.Should().Be(failBeforeCommit ? 0L : 1L);
        var intents = new DatabaseBackupExecutionIntentRepository(db);
        var recovered = await intents.ReadPendingAsync();
        if (failBeforeCommit) recovered.Should().NotContain(x => x.Id == source.Id);
        else
        {
            recovered.Should().ContainSingle(x => x.Id == source.Id);
            var stored = System.Linq.Enumerable.Single(recovered, x => x.Id == source.Id);
            stored.Source.Should().BeEquivalentTo(source.Source);
            await intents.MarkPublishedAsync(stored);
            (await intents.ReadPendingAsync()).Should().NotContain(x => x.Id == source.Id);
        }
    }
}
