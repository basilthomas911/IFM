using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog.StrategyCatalogValidation;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public sealed partial class ConfigurationDbContext
{
    // Each operation owns its connection/transaction. Concurrent calls on a resolved context cannot
    // share the mutable ambient repository transaction. Connections still use the framework provider.
    public async Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition,
        int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken = default)
    {
        var d = Freeze(definition);
        Text(createdBy, 200);
        Require(expectedPreviousVersion >= 0 && (long)d.Key.Version == (long)expectedPreviousVersion + 1, "Version must follow the expected previous version.");
        var hash = ContentHash(d);
        var json = JsonSerializer.Serialize(d, JsonOptions);
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CatalogWriteLock(connection, transaction, cancellationToken).ConfigureAwait(false);
        var latest = await Scalar(connection, transaction, """
SELECT COALESCE(MAX(version),0) FROM reference_configuration.strategy_catalog_version WHERE kind=$1 AND id=$2;
""", cancellationToken, (short)d.Key.Kind, d.Key.Id).ConfigureAwait(false);
        if (Convert.ToInt32(latest) != expectedPreviousVersion)
            throw new InvalidOperationException("Catalog authoring conflict: expected previous version does not match.");
        var code = await Scalar(connection, transaction, "SELECT code FROM reference_configuration.strategy_catalog_identity WHERE kind=$1 AND id=$2;",
            cancellationToken, (short)d.Key.Kind, d.Key.Id).ConfigureAwait(false);
        if (code is string existing && existing != d.Code) throw new InvalidOperationException("Catalog identity code is immutable.");

        // Exact dependencies must exist even for drafts. They need not be published until publication.
        foreach (var key in Dependencies(d))
            if (await ReadCatalog(connection, transaction, key, true, cancellationToken).ConfigureAwait(false) is null)
                throw new ArgumentException($"Missing catalog dependency: {key}.");
        if (d.Key.Kind == StrategyCatalogKind.ParameterSet)
        {
            var schema = await ReadCatalog(connection, transaction, d.Parent!, true, cancellationToken).ConfigureAwait(false);
            ValidateParameters(ReadShape(schema!.Definition.Settings), d.Settings);
        }
        var now = CatalogNow();
        await Execute(connection, transaction, """
INSERT INTO reference_configuration.strategy_catalog_identity(kind,id,code,created_utc,created_by)
VALUES($1,$2,$3,$4,$5) ON CONFLICT(kind,id) DO NOTHING;
""", cancellationToken, (short)d.Key.Kind, d.Key.Id, d.Code, now, createdBy).ConfigureAwait(false);
        await Execute(connection, transaction, """
WITH input AS (SELECT $1::jsonb d)
INSERT INTO reference_configuration.strategy_catalog_version(kind,id,version,schema_version,name,description,
 parent_kind,parent_id,parent_version,horizon,side,bias,premium_mode,settings_json,content_sha256,created_utc,created_by)
SELECT (d->'Key'->>'Kind')::smallint,(d->'Key'->>'Id')::uuid,(d->'Key'->>'Version')::integer,
 (d->>'SchemaVersion')::smallint,d->>'Name',d->>'Description',
 (d->'Parent'->>'Kind')::smallint,(d->'Parent'->>'Id')::uuid,(d->'Parent'->>'Version')::integer,
 (d->>'Horizon')::smallint,d->>'Side',d->>'Bias',d->>'PremiumMode',d->'Settings',$2,$3,$4 FROM input;
""", cancellationToken, json, hash, now, createdBy).ConfigureAwait(false);
        foreach (var child in StrategyCatalogSchemaSql.Children)
            await Execute(connection, transaction, StrategyCatalogSchemaSql.InsertChildren(child), cancellationToken, json).ConfigureAwait(false);
        await Execute(connection, transaction, """
UPDATE reference_configuration.strategy_catalog_version SET content_sealed=true WHERE kind=$1 AND id=$2 AND version=$3;
""", cancellationToken, (short)d.Key.Kind, d.Key.Id, d.Key.Version).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return hash;
    }

    public async Task<StoredStrategyCatalogDefinition?> GetStrategyCatalogAsync(CatalogKey key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        return await ReadCatalog(connection, null, key, false, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StrategyCatalogSummary>> ListStrategyCatalogAsync(StrategyCatalogKind kind,
        int limit = 50, string? afterCode = null, CancellationToken cancellationToken = default)
    {
        Require(Enum.IsDefined(kind) && limit is >= 1 and <= 128, "Invalid catalog list query.");
        if (afterCode is not null) Token(afterCode);
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, """
SELECT v.id,v.version,i.code,v.name,v.status,v.content_sha256
FROM reference_configuration.strategy_catalog_identity i
CROSS JOIN LATERAL (SELECT * FROM reference_configuration.strategy_catalog_version v
 WHERE v.kind=i.kind AND v.id=i.id ORDER BY version DESC LIMIT 1) v
WHERE i.kind=$1 AND i.code COLLATE "C">$2 COLLATE "C" ORDER BY i.code COLLATE "C" LIMIT $3;
""", (short)kind, afterCode ?? "", limit);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<StrategyCatalogSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(new(kind, reader.GetGuid(0), reader.GetInt32(1)), reader.GetString(2), reader.GetString(3),
                (CatalogLifecycleStatus)reader.GetInt16(4), reader.GetString(5)));
        return result;
    }

    public async Task PublishStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime effectiveFromUtc,
        string publishedBy, CancellationToken cancellationToken = default)
    {
        ValidateKey(key); Hash(expectedContentHash); Utc(effectiveFromUtc); Text(publishedBy, 200);
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CatalogWriteLock(connection, transaction, cancellationToken).ConfigureAwait(false);
        var graph = await LoadCatalogGraph(connection, transaction, key, effectiveFromUtc, true, cancellationToken).ConfigureAwait(false);
        var root = graph[key];
        if (root.Status != CatalogLifecycleStatus.Draft || root.ContentHash != expectedContentHash)
            throw new InvalidOperationException("Publish requires the exact Draft content hash.");
        await ValidateCatalogGraph(connection, transaction, graph, effectiveFromUtc, cancellationToken).ConfigureAwait(false);
        var count = await Execute(connection, transaction, """
UPDATE reference_configuration.strategy_catalog_version SET status=1,effective_from_utc=$4,published_by=$5
WHERE kind=$1 AND id=$2 AND version=$3 AND status=0 AND content_sha256=$6;
""", cancellationToken, (short)key.Kind, key.Id, key.Version, effectiveFromUtc, publishedBy, expectedContentHash).ConfigureAwait(false);
        if (count != 1) throw new InvalidOperationException("Catalog publication conflict.");
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RetireStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime retiredAtUtc,
        string retiredBy, CancellationToken cancellationToken = default)
    {
        ValidateKey(key); Hash(expectedContentHash); Utc(retiredAtUtc); Text(retiredBy, 200);
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CatalogWriteLock(connection, transaction, cancellationToken).ConfigureAwait(false);
        var current = await ReadCatalog(connection, transaction, key, true, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Status != CatalogLifecycleStatus.Published || current.ContentHash != expectedContentHash || retiredAtUtc < current.EffectiveFromUtc)
            throw new InvalidOperationException("Retire requires the exact Published version and a timestamp at or after publication.");
        await Execute(connection, transaction, """
UPDATE reference_configuration.strategy_catalog_version SET status=2,retired_at_utc=$4,retired_by=$5
WHERE kind=$1 AND id=$2 AND version=$3;
""", cancellationToken, (short)key.Kind, key.Id, key.Version, retiredAtUtc, retiredBy).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<StrategyCatalogSnapshot> GetPublishedStrategyDeploymentAsync(CatalogKey deployment, DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(deployment); Utc(asOfUtc); Require(deployment.Kind == StrategyCatalogKind.Deployment, "Expected a deployment.");
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        var graph = await LoadCatalogGraph(connection, transaction, deployment, asOfUtc, false, cancellationToken).ConfigureAwait(false);
        await ValidateCatalogGraph(connection, transaction, graph, asOfUtc, cancellationToken).ConfigureAwait(false);
        var definitions = graph.Values.OrderBy(x => x.Definition.Key.Kind).ThenBy(x => x.Definition.Key.Id).ThenBy(x => x.Definition.Key.Version).ToArray();
        var hash = Sha(CanonicalJson(JsonSerializer.SerializeToElement(new
        {
            SchemaVersion = 1, Deployment = deployment,
            Definitions = definitions.Select(x => new { x.Definition.Key, x.ContentHash }).ToArray()
        }, JsonOptions)));
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new(deployment, asOfUtc, definitions, hash);
    }

    static async Task<Dictionary<CatalogKey, StoredStrategyCatalogDefinition>> LoadCatalogGraph(NpgsqlConnection connection,
        NpgsqlTransaction transaction, CatalogKey root, DateTime at, bool allowRootDraft, CancellationToken ct)
    {
        var result = new Dictionary<CatalogKey, StoredStrategyCatalogDefinition>();
        var visiting = new HashSet<CatalogKey>();
        await Visit(root, 0).ConfigureAwait(false);
        return result;
        async Task Visit(CatalogKey key, int depth)
        {
            if (visiting.Contains(key)) throw new InvalidOperationException("Cyclic catalog dependency graph.");
            if (result.ContainsKey(key)) return;
            if (depth >= 32 || result.Count + visiting.Count >= 256) throw new InvalidOperationException("Catalog dependency graph exceeds bounds.");
            var row = await ReadCatalog(connection, transaction, key, true, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Missing exact catalog dependency: {key}.");
            if (!(allowRootDraft && key == root) && (row.Status != CatalogLifecycleStatus.Published || row.EffectiveFromUtc > at))
                throw new InvalidOperationException($"Catalog dependency is not effective and Published: {key}.");
            visiting.Add(key);
            foreach (var dependency in Dependencies(row.Definition)) await Visit(dependency, depth + 1).ConfigureAwait(false);
            visiting.Remove(key); result.Add(key, row);
        }
    }

    async Task ValidateCatalogGraph(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Dictionary<CatalogKey, StoredStrategyCatalogDefinition> graph, DateTime at, CancellationToken ct)
    {
        foreach (var stored in graph.Values)
        {
            ct.ThrowIfCancellationRequested();
            var d = stored.Definition;
            ValidateForPublication(d, graph);
            foreach (var requirement in d.Capabilities) ValidateCapability(requirement, d);
            if (d.Key.Kind == StrategyCatalogKind.ParameterSet)
                foreach (var validator in graph[d.Parent!].Definition.Capabilities.Where(c => c.Role == "validator"))
                    ValidateCapability(validator, d);
            if (d.Products.Length > 0 || d.LegacyFamilies.Length > 0)
            {
                if (catalogReferences is null) throw new InvalidOperationException("Catalog external reference validator is not registered.");
                foreach (var product in d.Products) await catalogReferences.ValidateProductAsync(product, ct).ConfigureAwait(false);
                foreach (var family in d.LegacyFamilies) await catalogReferences.ValidateLegacyFamilyAsync(family, Freeze(d), ct).ConfigureAwait(false);
            }
            foreach (var parameter in d.PipelineParameters)
            {
                var table = PipelineTable(parameter.Kind);
                await using var command = Command(connection, transaction, $"SELECT payload_sha256,status,effective_from_utc FROM reference_configuration.{table} WHERE parameter_set_id=$1 AND version=$2 FOR SHARE;", parameter.Id, parameter.Version);
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.GetString(0) != parameter.Hash || reader.GetInt16(1) != 1 || reader.IsDBNull(2) || reader.GetDateTime(2) > at)
                    throw new InvalidOperationException("Pipeline parameter reference is missing, mismatched or not effective and Published.");
            }
        }
        void ValidateCapability(CatalogCapability capability, StrategyCatalogDefinition owner)
        {
            if (catalogCapabilities is null) throw new InvalidOperationException($"Catalog capability registry is not registered: {capability.Code}.");
            // A registry is trusted server code, but receives a defensive copy to preserve the frozen write/hash.
            var copy = graph.ToDictionary(x => x.Key, x => x.Value with { Definition = Freeze(x.Value.Definition) });
            catalogCapabilities.Validate(capability, Freeze(owner), copy);
        }
    }

    internal static string PipelineTable(CatalogPipelineParameterKind kind) => kind switch
    {
        CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow => "intrinsic_time_strategy_workflow_parameter_set",
        CatalogPipelineParameterKind.RegimeDiscovery => "regime_discovery_parameter_set",
        CatalogPipelineParameterKind.MarketCondition => "market_condition_parameter_set",
        CatalogPipelineParameterKind.TradeSelection => "trade_selection_parameter_set",
        CatalogPipelineParameterKind.OrderComposition => "order_composition_parameter_set",
        CatalogPipelineParameterKind.RiskManagement => "risk_management_parameter_set",
        CatalogPipelineParameterKind.MarketConditionAssessment => "market_condition_assessment_parameter_set",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    static async Task<StoredStrategyCatalogDefinition?> ReadCatalog(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        CatalogKey key, bool lockRow, CancellationToken ct)
    {
        await using var command = Command(connection, transaction, StrategyCatalogSchemaSql.Exact + (lockRow ? " FOR SHARE OF v;" : ";"), (short)key.Kind, key.Id, key.Version);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return null;
        var definition = Freeze(JsonSerializer.Deserialize<StrategyCatalogDefinition>(reader.GetString(0), JsonOptions)!);
        var hash = reader.GetString(1);
        if (definition.Key != key || ContentHash(definition) != hash) throw new InvalidOperationException("Catalog metadata/content hash mismatch.");
        return new(definition, hash, (CatalogLifecycleStatus)reader.GetInt16(2), reader.GetDateTime(3), reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetDateTime(5), reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetDateTime(7), reader.IsDBNull(8) ? null : reader.GetString(8));
    }

    async Task<NpgsqlConnection> OpenCatalogAsync(CancellationToken ct)
    {
        var connection = CreateConnection().As<NpgsqlConnection>(ConnectionString);
        try { await connection.OpenAsync(ct).ConfigureAwait(false); return connection; }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }
    static Task<int> CatalogWriteLock(NpgsqlConnection c, NpgsqlTransaction tx, CancellationToken ct) => Execute(c, tx,
        "SELECT pg_advisory_xact_lock($1);", ct, StrategyCatalogSchemaSql.WriterLock);
    static async Task<int> Execute(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, CancellationToken ct, params object[] args)
    {
        await using var command = Command(c, tx, sql, args);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    static async Task<object?> Scalar(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, CancellationToken ct, params object[] args)
    {
        await using var command = Command(c, tx, sql, args);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
    static NpgsqlCommand Command(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, params object[] args)
    {
        var command = new NpgsqlCommand(sql, c, tx);
        foreach (var arg in args)
        {
            var type = arg switch { short => NpgsqlDbType.Smallint, int => NpgsqlDbType.Integer, long => NpgsqlDbType.Bigint,
                Guid => NpgsqlDbType.Uuid, DateTime => NpgsqlDbType.TimestampTz, string => NpgsqlDbType.Text,
                _ => throw new ArgumentException("Unsupported catalog SQL parameter type.") };
            command.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = type, Value = arg });
        }
        return command;
    }
    internal static DateTime CatalogNow() => new(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);
}
