using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.BDDTests;

public sealed class SystemAdminPersistenceScenarios(SystemAdminBddDatabase database) : IClassFixture<SystemAdminBddDatabase>
{
    [Fact]
    public async Task Given_a_persisted_projection_When_retried_after_restart_Then_receipt_and_read_rows_are_retained()
    {
        const string projector = "BackupConventionRestart";
        var snapshot = Snapshot(101);
        Assert.Equal(EventProjectionApplyOutcome.Applied, await database.Context.ApplyDatabaseBackupSnapshotAsync(projector, snapshot));
        var restarted = database.CreateContext();
        Assert.Same(restarted, ((ISystemAdminDbContext)restarted).DbReader);
        Assert.Same(restarted, ((ISystemAdminDbContext)restarted).DbWriter);
        Assert.Equal(EventProjectionApplyOutcome.AlreadyApplied, await restarted.ApplyDatabaseBackupSnapshotAsync(projector, snapshot));
        var row = await restarted.GetBackupOperationAsync(new() { OperationId = snapshot.Source.OperationId }, default);
        Assert.NotNull(row);
        Assert.Equal(snapshot.Source.OperationId, row.OperationId);
        Assert.Equal(snapshot.EventId, (await restarted.GetDatabaseBackupProjectionCheckpointAsync(projector))!.LastEventId);
        Assert.Single(await restarted.GetProtectionSetsAsync(new() { Source = BackupSource.LocalWorkstation }, default));
    }

    [Fact]
    public async Task Given_a_durable_receipt_When_its_payload_conflicts_Then_the_database_rejects_it_and_keeps_the_original()
    {
        const string projector = "BackupConventionConflict";
        var snapshot = Snapshot(202);
        await database.Context.ApplyDatabaseBackupSnapshotAsync(projector, snapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await database.Context.ApplyDatabaseBackupSnapshotAsync(projector, snapshot with { EventHash = "conflicting" }));
        Assert.Equal(EventProjectionApplyOutcome.AlreadyApplied, await database.CreateContext().ApplyDatabaseBackupSnapshotAsync(projector, snapshot));
        Assert.Equal(snapshot.EventId, (await database.Context.GetDatabaseBackupProjectionCheckpointAsync(projector))!.LastEventId);
    }

    static DatabaseBackupProjectionSnapshot Snapshot(long revision)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id, EventId = revision, EventHash = id.ToString("N"), WriteOperation = true,
            Source = new()
            {
                SourceEventId = id, OperationId = new(Guid.NewGuid()), Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new("isolated-convention-bdd"), PolicyRevision = 1,
                OperationKind = DatabaseRecoveryOperationKind.Backup, Phase = DatabaseRecoveryPhase.Requested,
                SourceRevisionOrSequence = revision, ObservedUtc = DateTimeOffset.UtcNow
            }
        };
    }
}

public sealed class SystemAdminBddDatabase : IAsyncLifetime
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("systemadmin-convention-bdd", includeCql: false);
    IDbConnectionSettings settings = null!;
    public SystemAdminDbContext Context { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await infrastructure.StartAsync();
        var connection = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) { Username = string.Empty, Password = string.Empty };
        if (!connection.Database!.StartsWith("ifm_systemadminconventionbdd_", StringComparison.Ordinal))
            throw new InvalidOperationException("SystemAdmin BDD requires its disposable database.");
        settings = new DbConnectionSettings().Add(SystemAdminDbContext.SystemAdminDbConnection, connection.ConnectionString, "System.Data.Postgres");
        settings.Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection.ConnectionString, "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new SystemAdminSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        Context = CreateContext();
    }

    public SystemAdminDbContext CreateContext() => new(settings, NullLogger<DbProvider>.Instance);
    public async Task DisposeAsync() => await infrastructure.DisposeAsync();
}
