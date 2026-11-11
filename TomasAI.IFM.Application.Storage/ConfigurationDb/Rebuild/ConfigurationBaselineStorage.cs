using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Rebuild;
/// <summary>Technical PostgreSQL reads and atomic row materialization for configuration maintenance.</summary>
public static class ConfigurationBaselineStorage
{
    static readonly string[] LegacyTables = ["parameter_area", "parameter_component", "parameter_schema_version", "intrinsic_time_strategy_workflow_parameter_set", "regime_discovery_parameter_set", "market_condition_parameter_set", "trade_selection_parameter_set", "order_composition_parameter_set", "risk_management_parameter_set", "strategy_position_risk_parameter_set", "market_condition_assessment_parameter_set", "volatility_series_definition", "lookup_definition", "strategy_catalog_identity", "strategy_catalog_version", ..ConfigurationSchemaSql.StrategyCatalog.Children.Select(x => x.Name), "parameter_set", "parameter_set_version", "parameter_operation", "parameter_set_audit", "parameter_assignment", "parameter_assignment_revision", "parameter_legacy_reference", "parameter_startup_run", "parameter_startup_report", "market_data.strategy_option_chain_parameter_version", "market_data.strategy_option_chain_parameter_current"];
    static readonly string[] Tables=[..LegacyTables.Where(x=>!x.StartsWith("market_data.strategy_option_chain_parameter_",StringComparison.Ordinal)),"market_data.strategy_option_chain_policy_version","market_data.strategy_option_chain_policy_current","parameter_value_version",..ParameterValueMapping.Tables.Select(x=>x.Name),
        "parameter_projection_record","parameter_catalog_operation","parameter_catalog_startup_state","parameter_catalog_migration",..ParameterProjectionMapping.Tables.Select(x=>x.Name)];

    /// <summary>Locks the configured tables and reads one consistent image and sequence state.</summary>
    public static async Task<ConfigurationBaselineDatabaseImage> CaptureAsync(EnlistedEventTransaction db, CancellationToken token)
    {
        await LockAsync(db, token)
            .ConfigureAwait(false);
        var cut = Convert.ToInt64(await db.ScalarAsync(ConfigurationDbSql.BaselineSourceCut, [], token)
            .ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
        var images = new List<ConfigurationBaselineTable>(Tables.Length);
        foreach (var table in Tables)
        {
            var columns = await ColumnsAsync(db, table, token)
            .ConfigureAwait(false);
            var rows = await RowsAsync(db, table, token)
            .ConfigureAwait(false);
            images.Add(new(table, JsonSerializer.Serialize(columns), rows, Hash(rows)));
        }
        var sequence = (await db.QueryAsync(ConfigurationDbSql.BaselineLookupSequence, [], MapToLookupSequence, token)
            .ConfigureAwait(false)).Single();
        return new(cut, images.ToArray(), sequence.Last, sequence.Called);
    }

    /// <summary>Reads the exact stream head under the caller's maintenance lock.</summary>
    public static async Task<long> ReadRevisionAsync(EnlistedEventTransaction db, string stream, CancellationToken token)
        => Convert.ToInt64(await db.ScalarAsync(ConfigurationDbSql.BaselineStreamRevision, [stream], token)
            .ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Returns the latest committed raw event for the requested baseline stream.</summary>
    public static async Task<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel?> ReadLatestAsync(
        EnlistedEventTransaction db, string stream, CancellationToken token)
        => (await db.QueryAsync(ConfigurationDbSql.ReadBaselineLatestEvent, [stream], MapToBaselineEvent, token)
            .ConfigureAwait(false)).SingleOrDefault();

    static (long Last, bool Called) MapToLookupSequence(System.Data.Common.DbDataReader reader)
        => (reader.GetInt64(0), reader.GetBoolean(1));

    static TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel MapToBaselineEvent(System.Data.Common.DbDataReader reader)
        => new() { EventTypeName = reader.GetString(0), EventVersion = reader.GetInt64(1), EventData = reader.GetFieldValue<byte[]>(2) };

    /// <summary>Verifies that the payload names match a supported physical schema image.</summary>
    public static bool MatchesLayout(int version, IEnumerable<string> names)
        => names.SequenceEqual(version == 1 ? LegacyTables : Tables, StringComparer.Ordinal);

    /// <summary>Rebuilds exact baseline rows into empty configuration tables, allowing identical registry seeds.
    /// Catalog drafts are populated, sealed and restored to their accepted lifecycle with all guards enabled.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S3267", Justification = "Each row verification awaits a query on the same transaction; a synchronous Where predicate cannot perform this operation.")]
    public static async Task<bool> MaterializeAsync(EnlistedEventTransaction db, ConfigurationProjectionBaselineEvent source, CancellationToken token)
    {
        // A checked-in table allowlist is required before using any SQL identifier from an image.
        if (!MatchesLayout(source.SchemaVersion, source.Tables.Select(table => table.Name)))
            throw new InvalidDataException("Configuration baseline layout is not a supported physical table set.");

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
                rows = (string)(await db.ScalarAsync(ConfigurationDbSql.ConfigurationBaselineStorage_MaterializeAsync_1, [rows], token)
            .ConfigureAwait(false))!;
                catalogInserted = true;
            }

            var names = string.Join(",", columns.Select(x => Quote(x.Name)));
            await db.ExecuteAsync(ConfigurationDbSql.InsertBaselineRows(Qualified(image.Name), names), [rows], token)
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
        await db.ExecuteAsync(ConfigurationDbSql.ConfigurationBaselineStorage_MaterializeAsync_2, [source.LookupSequenceLastValue, source.LookupSequenceIsCalled], token)
            .ConfigureAwait(false);
        return true;
    }

    static async Task RestoreCatalogLifecycleAsync(EnlistedEventTransaction db, string rows, CancellationToken token)
    {
        await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineCatalogLifecycle1, [rows], token)
            .ConfigureAwait(false);
        await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineCatalogLifecycle2, [rows], token)
            .ConfigureAwait(false);
        await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineCatalogLifecycle3, [rows], token)
            .ConfigureAwait(false);
    }

    public static async Task<bool> ClaimAsync(EnlistedEventTransaction db, ConfigurationProjectionBaselineEvent source, CancellationToken token)
    {
        var existing = await db.ScalarAsync(ConfigurationDbSql.ConfigurationBaselineStorage_ClaimAsync_1, [source.Id], token)
            .ConfigureAwait(false);
        if (existing is long id)
        {
            if (id != source.EventId)
                throw new InvalidDataException("Configuration baseline identity conflict.");
            return false;
        }

        await db.ExecuteAsync(ConfigurationDbSql.ConfigurationBaselineStorage_ClaimAsync_2, [source.Id, source.EventId, source.SourceCut], token)
            .ConfigureAwait(false);
        return true;
    }

    static Task<int> LockAsync(EnlistedEventTransaction db, CancellationToken token) => db.ExecuteAsync(ConfigurationDbSql.LockBaselineTables(string.Join(",", Tables.Select(Qualified))), [], token);
    static async Task<string> RowsAsync(EnlistedEventTransaction db, string table, CancellationToken token) => (string)(await db.ScalarAsync(ConfigurationDbSql.ReadBaselineRows(Qualified(table)), [], token)
            .ConfigureAwait(false))!;
    static async Task<ColumnLayout[]> ColumnsAsync(EnlistedEventTransaction db, string table, CancellationToken token)
    {
        var qualified = Qualified(table).Split('.');
        return (await db.QueryAsync(ConfigurationDbSql.ConfigurationBaselineStorage_ColumnsAsync_1, [qualified[0], qualified[1]], MapToColumnsRow1, token)
            .ConfigureAwait(false)).ToArray();
    }

    static string Qualified(string name) => name.Contains('.', StringComparison.Ordinal) ? name : "reference_configuration." + name;
    static string Quote(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    sealed record ColumnLayout(string Name, string TypeSchema, string TypeName, string Nullable, string Default, string Identity, string? IdentityGeneration);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::TomasAI.IFM.Application.Storage.ConfigurationDb.Rebuild.ConfigurationBaselineStorage.ColumnLayout MapToColumnsRow1(global::System.Data.Common.DbDataReader reader) => new ColumnLayout(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6));
}


/// <summary>One locked configuration table image and its database source cut.</summary>
public sealed record ConfigurationBaselineDatabaseImage(long SourceCut, ConfigurationBaselineTable[] Tables, long SequenceLast, bool SequenceCalled);
