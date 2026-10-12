using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Rebuild;
/// <summary>Captures complete offline configuration facts and restores them without provider or workflow calls.</summary>
public sealed class ConfigurationProjectionBaseline(IPostgresEventTransaction transactions)
{
    const string Stream = "ConfigurationProjectionBaseline";
    static readonly string[] Tables = ["parameter_area", "parameter_component", "parameter_schema_version", "intrinsic_time_strategy_workflow_parameter_set", "regime_discovery_parameter_set", "market_condition_parameter_set", "trade_selection_parameter_set", "order_composition_parameter_set", "risk_management_parameter_set", "strategy_position_risk_parameter_set", "market_condition_assessment_parameter_set", "volatility_series_definition", "lookup_definition", "strategy_catalog_identity", "strategy_catalog_version", ..ConfigurationSchemaSql.StrategyCatalog.Children.Select(x => x.Name), "parameter_set", "parameter_set_version", "parameter_operation", "parameter_set_audit", "parameter_assignment", "parameter_assignment_revision", "parameter_legacy_reference", "parameter_startup_run", "parameter_startup_report", "market_data.strategy_option_chain_parameter_version", "market_data.strategy_option_chain_parameter_current"];
    /// <summary>Locks and saves every configuration row and its schema layout as one accepted source event.</summary>
    public Task<ConfigurationProjectionBaselineEvent> CaptureAsync(bool sourceWritesQuiesced, CancellationToken cancellationToken = default)
    {
        if (!sourceWritesQuiesced)
            throw new ArgumentException("Stop source writers before configuration baseline capture.", nameof(sourceWritesQuiesced));
        return transactions.ExecuteOnceAsync(async (db, token) =>
        {
            await LockAsync(db, token)
            .ConfigureAwait(false);
            var cut = Convert.ToInt64(await db.ScalarAsync("SELECT COALESCE(max(eventversion),0) FROM event_log;", [], token)
            .ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
            var images = new List<ConfigurationBaselineTable>();
            foreach (var table in Tables)
            {
                var columns = await ColumnsAsync(db, table, token)
            .ConfigureAwait(false);
                var rows = await RowsAsync(db, table, token)
            .ConfigureAwait(false);
                images.Add(new(table, JsonSerializer.Serialize(columns), rows, Hash(rows)));
            }

            var sequence = (await db.QueryAsync("SELECT last_value,is_called FROM reference_configuration.lookup_definition_id_seq;", [], reader => (Last: reader.GetInt64(0), Called: reader.GetBoolean(1)), token)
            .ConfigureAwait(false)).Single();
            var source = new ConfigurationProjectionBaselineEvent
            {
                Id = Guid.NewGuid(),
                CommandId = Guid.NewGuid(),
                ReceivedOn = DateTime.UtcNow,
                SourceCut = cut,
                Tables = images.ToArray(),
                LookupSequenceLastValue = sequence.Last,
                LookupSequenceIsCalled = sequence.Called
            };
            var version = Convert.ToInt64(await db.ScalarAsync("SELECT COALESCE(max(e.streamversion),0) FROM event_log e JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1;", [Stream], token)
            .ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
            await db.AppendAsync(Stream, source.CommandId, source, version, token)
            .ConfigureAwait(false);
            await ClaimAsync(db, source, token)
            .ConfigureAwait(false);
            return source;
        }, cancellationToken);
    }

    /// <summary>Reads the single latest baseline from its persisted source bytes.</summary>
    public Task<ConfigurationProjectionBaselineEvent?> ReadLatestAsync(CancellationToken cancellationToken = default) => transactions.ExecuteOnceAsync(async (db, token) =>
    {
        var sources = await db.QueryAsync("SELECT n.eventtypename,l.eventversion,l.eventpayload FROM event_log l JOIN event_name_id n USING(eventnameid) JOIN event_stream_id s USING(eventstreamid) WHERE s.eventstream=$1 ORDER BY l.streamversion DESC LIMIT 1;", [Stream], reader => new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel { EventTypeName = reader.GetString(0), EventVersion = reader.GetInt64(1), EventData = reader.GetFieldValue<byte[]>(2) }.ToDomainEvent(), token)
            .ConfigureAwait(false);
        return sources.SingleOrDefault() as ConfigurationProjectionBaselineEvent;
    }, cancellationToken);
    /// <summary>Rebuilds exact baseline rows into empty configuration tables, allowing identical registry seeds.
    /// Catalog drafts are populated, sealed and restored to their accepted lifecycle with all guards enabled.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S3267", Justification = "Each row verification awaits a query on the same transaction; a synchronous Where predicate cannot perform this operation.")]
    public Task MaterializeAsync(ConfigurationProjectionBaselineEvent source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SchemaVersion != 1 || source.LookupSequenceLastValue < 1 || source.Id == Guid.Empty || source.EventId <= source.SourceCut || !source.Tables.Select(x => x.Name).SequenceEqual(Tables, StringComparer.Ordinal) || source.Tables.Any(x => !string.Equals(Hash(x.RowsJson), x.Sha256, StringComparison.Ordinal)))
            throw new InvalidDataException("Committed complete configuration baseline and matching hashes required.");
        return transactions.ExecuteOnceAsync(async (db, token) =>
        {
            await LockAsync(db, token)
            .ConfigureAwait(false);
            if (!await ClaimAsync(db, source, token)
            .ConfigureAwait(false))
                return false;
            var catalogInserted = false;
            foreach (var image in source.Tables)
            {
                var columns = await ColumnsAsync(db, image.Name, token)
            .ConfigureAwait(false);
                if (!string.Equals(JsonSerializer.Serialize(columns), image.ColumnsJson, StringComparison.Ordinal))
                    throw new InvalidDataException("Configuration baseline columns differ from target: " + image.Name);
                var current = await RowsAsync(db, image.Name, token)
            .ConfigureAwait(false);
                if (string.Equals(current, image.RowsJson, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(current, "[]", StringComparison.Ordinal) && image.Name is not ("parameter_area" or "parameter_component" or "parameter_schema_version"))
                    throw new InvalidOperationException("Configuration baseline requires an empty target table: " + image.Name);
                var rows = image.RowsJson;
                if (string.Equals(image.Name, "strategy_catalog_version", StringComparison.Ordinal))
                {
                    rows = (string)(await db.ScalarAsync("SELECT COALESCE(jsonb_agg(j || jsonb_build_object('status',0,'content_sealed',false,'effective_from_utc',null,'published_by',null,'retired_at_utc',null,'retired_by',null)),'[]'::jsonb)::text FROM jsonb_array_elements($1::jsonb) j;", [rows], token)
            .ConfigureAwait(false))!;
                    catalogInserted = true;
                }

                var names = string.Join(",", columns.Select(x => Quote(x.Name)));
                await db.ExecuteAsync($"INSERT INTO {Qualified(image.Name)} ({names}) OVERRIDING SYSTEM VALUE SELECT {names} FROM jsonb_populate_recordset(NULL::{Qualified(image.Name)},$1::jsonb) ON CONFLICT DO NOTHING;", [rows], token)
            .ConfigureAwait(false);
            }

            if (catalogInserted)
                await RestoreCatalogLifecycleAsync(db, source.Tables.Single(x => string.Equals(x.Name, "strategy_catalog_version", StringComparison.Ordinal)).RowsJson, token)
            .ConfigureAwait(false);
            foreach (var image in source.Tables)
                if (!string.Equals(await RowsAsync(db, image.Name, token)
            .ConfigureAwait(false), image.RowsJson, StringComparison.Ordinal))
                    throw new InvalidDataException("Configuration baseline row mismatch: " + image.Name);
            // Explicit identity values also restore the next generated lookup id.
            await db.ExecuteAsync("SELECT setval(pg_get_serial_sequence('reference_configuration.lookup_definition','id'),$1,$2);", [source.LookupSequenceLastValue, source.LookupSequenceIsCalled], token)
            .ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    static async Task RestoreCatalogLifecycleAsync(EnlistedEventTransaction db, string rows, CancellationToken token)
    {
        await db.ExecuteAsync("UPDATE reference_configuration.strategy_catalog_version v SET content_sealed=true WHERE NOT v.content_sealed AND EXISTS(SELECT 1 FROM jsonb_populate_recordset(NULL::reference_configuration.strategy_catalog_version,$1::jsonb) b WHERE (b.kind,b.id,b.version)=(v.kind,v.id,v.version));", [rows], token)
            .ConfigureAwait(false);
        await db.ExecuteAsync("UPDATE reference_configuration.strategy_catalog_version v SET status=1,effective_from_utc=b.effective_from_utc,published_by=b.published_by FROM jsonb_populate_recordset(NULL::reference_configuration.strategy_catalog_version,$1::jsonb) b WHERE (b.kind,b.id,b.version)=(v.kind,v.id,v.version) AND b.status>=1;", [rows], token)
            .ConfigureAwait(false);
        await db.ExecuteAsync("UPDATE reference_configuration.strategy_catalog_version v SET status=2,retired_at_utc=b.retired_at_utc,retired_by=b.retired_by FROM jsonb_populate_recordset(NULL::reference_configuration.strategy_catalog_version,$1::jsonb) b WHERE (b.kind,b.id,b.version)=(v.kind,v.id,v.version) AND b.status=2;", [rows], token)
            .ConfigureAwait(false);
    }

    static async Task<bool> ClaimAsync(EnlistedEventTransaction db, ConfigurationProjectionBaselineEvent source, CancellationToken token)
    {
        var existing = await db.ScalarAsync("SELECT source_event_id FROM reference_configuration.configuration_baseline_application WHERE baseline_id=$1;", [source.Id], token)
            .ConfigureAwait(false);
        if (existing is long id)
        {
            if (id != source.EventId)
                throw new InvalidDataException("Configuration baseline identity conflict.");
            return false;
        }

        await db.ExecuteAsync("INSERT INTO reference_configuration.configuration_baseline_application(baseline_id,source_event_id,source_cut) VALUES($1,$2,$3);", [source.Id, source.EventId, source.SourceCut], token)
            .ConfigureAwait(false);
        return true;
    }

    static Task<int> LockAsync(EnlistedEventTransaction db, CancellationToken token) => db.ExecuteAsync("LOCK TABLE " + string.Join(",", Tables.Select(Qualified)) + ",reference_configuration.configuration_baseline_application IN SHARE ROW EXCLUSIVE MODE;", [], token);
    static async Task<string> RowsAsync(EnlistedEventTransaction db, string table, CancellationToken token) => (string)(await db.ScalarAsync($"SELECT COALESCE(jsonb_agg(j ORDER BY j::text),'[]'::jsonb)::text FROM (SELECT to_jsonb(t) j FROM {Qualified(table)} t) q;", [], token)
            .ConfigureAwait(false))!;
    static async Task<ColumnLayout[]> ColumnsAsync(EnlistedEventTransaction db, string table, CancellationToken token)
    {
        var qualified = Qualified(table).Split('.');
        return (await db.QueryAsync("SELECT column_name,udt_schema,udt_name,is_nullable,COALESCE(column_default,''),is_identity,identity_generation FROM information_schema.columns WHERE table_schema=$1 AND table_name=$2 AND is_generated='NEVER' ORDER BY ordinal_position;", [qualified[0], qualified[1]], reader => new ColumnLayout(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6)), token)
            .ConfigureAwait(false)).ToArray();
    }

    static string Qualified(string name) => name.Contains('.', StringComparison.Ordinal) ? name : "reference_configuration." + name;
    static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    sealed record ColumnLayout(string Name, string TypeSchema, string TypeName, string Nullable, string Default, string Identity, string? IdentityGeneration);
}
