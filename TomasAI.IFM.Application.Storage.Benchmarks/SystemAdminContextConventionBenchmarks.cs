using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Exercises actual backup projection writes, idempotent retries and typed PostgreSQL reads.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class SystemAdminContextConventionBenchmarks
{
    SystemAdminDbContext context = null!;
    DatabaseBackupProjectionSnapshot original = null!;
    readonly Guid operation = Guid.NewGuid();
    long revision = 1;
    const string Projector = "SystemAdminContextQualification";

    [Params(false, true)]
    public bool ColdConnection { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable context benchmark database first.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");
        await using (var guard = new NpgsqlConnection(connection))
        {
            await guard.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await command.ExecuteReaderAsync();
            await row.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }
        settings.Pooling = !ColdConnection;
        settings.Username = string.Empty;
        settings.Password = string.Empty;
        var databases = new DbConnectionSettings().Add(SystemAdminDbContext.SystemAdminDbConnection, settings.ConnectionString, "System.Data.Postgres");
        databases.Add(EventSourceActorDbContext.EventSourceActorDbConnection, settings.ConnectionString, "System.Data.Postgres");
        await new EventSourceSchemaDb(databases, NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new SystemAdminSchemaDb(databases, NullLogger<DbProvider>.Instance).CreateAllAsync();
        context = new(databases, NullLogger<DbProvider>.Instance);
        // Distinct durable receipt keys across independent benchmark instances in the same arm.
        revision = DateTime.UtcNow.Ticks;
        original = NewSnapshot();
        if (await context.ApplyDatabaseBackupSnapshotAsync(Projector, original) != EventProjectionApplyOutcome.Applied
            || await context.ApplyDatabaseBackupSnapshotAsync(Projector, original) != EventProjectionApplyOutcome.AlreadyApplied
            || await ReadOperation() is null || (await ReadProtectionSets()).Length == 0)
            throw new InvalidOperationException("SystemAdmin projection preflight failed.");
    }

    [Benchmark]
    public ValueTask<DatabaseProtectionSetReadModel[]> ReadProtectionSets()
        => context.GetProtectionSetsAsync(new() { Source = BackupSource.LocalWorkstation }, default);

    [Benchmark]
    public ValueTask<DatabaseBackupOperationReadModel?> ReadOperation()
        => context.GetBackupOperationAsync(new() { OperationId = new(operation) }, default);

    [Benchmark]
    public ValueTask<DatabaseBackupProjectionCheckpointReadModel?> ReadCheckpoint()
        => context.GetDatabaseBackupProjectionCheckpointAsync(Projector);

    [Benchmark]
    public ValueTask<EventProjectionApplyOutcome> ApplyNewRevision()
        => context.ApplyDatabaseBackupSnapshotAsync(Projector, NewSnapshot());

    [Benchmark]
    public ValueTask<EventProjectionApplyOutcome> RetryAppliedRevision()
        => context.ApplyDatabaseBackupSnapshotAsync(Projector, original);

    DatabaseBackupProjectionSnapshot NewSnapshot()
    {
        var id = Guid.NewGuid();
        var next = Interlocked.Increment(ref revision);
        return new()
        {
            Id = id, EventId = next, EventHash = id.ToString("N"), WriteOperation = true,
            Source = new()
            {
                SourceEventId = id, OperationId = new(operation), Source = BackupSource.LocalWorkstation,
                ProtectionSetId = new("isolated-qualification"), PolicyRevision = 1,
                OperationKind = DatabaseRecoveryOperationKind.Backup, Phase = DatabaseRecoveryPhase.Requested,
                SourceRevisionOrSequence = next, ObservedUtc = DateTimeOffset.UtcNow
            }
        };
    }
}
