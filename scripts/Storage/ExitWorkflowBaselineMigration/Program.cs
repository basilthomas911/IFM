using TomasAI.IFM.Application.EventSourcing.Projections.TradePlan;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

if (args is not ["--capture-writers-stopped"] and not ["--verify-existing"])
    throw new ArgumentException("Usage: --capture-writers-stopped or --verify-existing.", nameof(args));
var connection = Environment.GetEnvironmentVariable("IFM_EXIT_WORKFLOW_BASELINE_CONNECTION")
    ?? throw new InvalidOperationException("Set IFM_EXIT_WORKFLOW_BASELINE_CONNECTION to the prepared local Development database.");
var identity = new NpgsqlConnectionStringBuilder(connection);
if (identity.Host is not ("localhost" or "127.0.0.1") || identity.Database is not ("ifm_application_development" or "ifm_storage_refactor_test"))
    throw new InvalidOperationException("This offline migration is restricted to the prepared local Development or isolated fixture database.");
var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection, connection, "System.Data.Postgres")
    .Add(TradePlanDbContext.TradePlanDbConnection, connection, "System.Data.Postgres");
var transactions = new PostgresEventTransaction(settings);
var target = new TradePlanDbContext(settings, NullLogger<DbProvider>.Instance);
var baselines = new ExitWorkflowProjectionBaseline(transactions, target);
var source = await baselines.ReadLatestAsync().ConfigureAwait(false);
if (string.Equals(args[0], "--capture-writers-stopped", StringComparison.Ordinal))
{
    if (source is not null) throw new InvalidOperationException("An exit-workflow baseline already exists; verify it rather than duplicating it.");
    var captured = await baselines.CaptureAsync(true).ConfigureAwait(false);
    source = await baselines.ReadLatestAsync().ConfigureAwait(false)
        ?? throw new InvalidDataException("The saved baseline cannot be loaded from persisted source bytes.");
    if (captured.Id != source.Id || captured.Snapshots.Length != source.Snapshots.Length || !string.Equals(captured.PayloadSha256, source.PayloadSha256, StringComparison.Ordinal))
        throw new InvalidDataException("Saved baseline differs from the captured complete stage facts.");
}
if (source is null) throw new InvalidDataException("No exit-workflow baseline exists.");
// Natural-key writes are verified twice; this does not dispatch actor or broker operations.
await baselines.MaterializeAsync(source).ConfigureAwait(false);
await baselines.MaterializeAsync(source).ConfigureAwait(false);
Console.WriteLine(JsonSerializer.Serialize(new { database = identity.Database, baselineId = source.Id, sourceEventId = source.EventId,
    sourceCut = source.SourceCut, schemaVersion = source.SchemaVersion, stageRows = source.Snapshots.Length,
    sha256 = source.PayloadSha256, verifiedPersistedBytesAtUtc = DateTime.UtcNow }));
