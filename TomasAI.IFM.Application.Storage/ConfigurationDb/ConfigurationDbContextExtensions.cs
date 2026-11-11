using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage.Postgres;
using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using static TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical.StrategyCatalogValidation;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
/// <summary>Provides ConfigurationDb persistence helpers and stored-data integrity checks.</summary>
internal static class ConfigurationDbContextExtensions
{
    extension(ConfigurationDbContext context)
    {
        /// <summary>Deserializes and verifies the stored Regime Discovery payload and returns its resolved version.</summary>
        /// <param name = "row">The row.</param>
        /// <returns>The resolve result.</returns>
        internal ResolvedRegimeDiscoveryParameterSet Resolve(ConfigurationParameterSet row)
        {
            var typed = JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(row.PayloadJson) ?? throw new InvalidOperationException("Stored Regime Discovery configuration cannot be deserialized.");
            var canonicalPayload = RegimeDiscoveryParameterPayload.Serialize(typed);
            if (!string.Equals(row.PayloadSha256, RegimeDiscoveryParameterPayload.ComputeSha256(canonicalPayload), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Stored Regime Discovery configuration hash is invalid.");
            var errors = new RegimeDiscoveryParameterSetValidationRules().Execute(typed);
            if (errors.Length != 0)
                throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.ErrorMessage)));
            return new(typed, canonicalPayload, row.PayloadSha256, row.EffectiveFromUtc ?? DateTime.MinValue);
        }

        /// <summary>Deserializes and verifies the stored Market Condition payload and returns its resolved version.</summary>
        /// <param name = "row">The row.</param>
        /// <returns>The resolve market condition result.</returns>
        internal ResolvedMarketConditionParameterSet ResolveMarketCondition(ConfigurationParameterSet row)
        {
            var typed = JsonSerializer.Deserialize<MarketConditionParameterSet>(row.PayloadJson) ?? throw new InvalidOperationException("Stored Market Condition configuration cannot be deserialized.");
            var canonicalPayload = MarketConditionParameterPayload.Serialize(typed);
            var canonicalHash = MarketConditionParameterPayload.ComputeSha256(canonicalPayload);
            if (!string.Equals(row.PayloadSha256, canonicalHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Stored Market Condition configuration hash is invalid.");
            if (row.ParameterSetId != typed.ParameterSetId || row.Version != typed.Version || row.SchemaVersion != typed.SchemaVersion)
                throw new InvalidOperationException("Stored Market Condition configuration identity or schema metadata is invalid.");
            var errors = new MarketConditionParameterSetValidationRules().Execute(typed);
            if (errors.Length != 0)
                throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.ErrorMessage)));
            return new(typed, canonicalPayload, canonicalHash, row.EffectiveFromUtc ?? DateTime.MinValue);
        }

        /// <summary>Requires exactly one persisted draft or published version to complete the lifecycle transition.</summary>
        /// <param name = "affectedRows">The affected rows.</param>
        /// <param name = "kind">The kind.</param>
        /// <param name = "parameterSetId">The parameter set id.</param>
        /// <param name = "version">The version.</param>
        /// <param name = "operation">The operation.</param>
        /// <param name = "requiredState">The required state.</param>
        internal void EnsureSingleLifecycleTransition(long[] affectedRows, StrategyParameterSetKind kind, Guid parameterSetId, int version, string operation, string requiredState)
        {
            if (affectedRows.Length == 1 && affectedRows[0] == 1)
                return;
            throw new InvalidOperationException($"Cannot {operation} {kind} parameter set {parameterSetId:D} version {version}; " + $"exactly one {requiredState} version must exist.");
        }

        /// <summary>Loads the exact dependency graph using the caller-owned catalog transaction.</summary>
        /// <param name = "connection">The PostgreSQL connection.</param>
        /// <param name = "transaction">The caller-owned transaction.</param>
        /// <param name = "root">The root.</param>
        /// <param name = "at">The UTC effective timestamp.</param>
        /// <param name = "allowRootDraft">The allow root draft.</param>
        /// <param name = "ct">The cancellation token.</param>
        /// <returns>The load catalog graph result.</returns>
        internal async Task<Dictionary<CatalogKey, StoredStrategyCatalogDefinition>> LoadCatalogGraph(NpgsqlConnection connection, NpgsqlTransaction transaction, CatalogKey root, DateTime at, bool allowRootDraft, bool lockRows, CancellationToken ct)
        {
            var result = new Dictionary<CatalogKey, StoredStrategyCatalogDefinition>();
            var visiting = new HashSet<CatalogKey>();
            await Visit(root, 0)
            .ConfigureAwait(false);
            return result;
            async Task Visit(CatalogKey key, int depth)
            {
                if (visiting.Contains(key))
                    throw new InvalidOperationException("Cyclic catalog dependency graph.");
                if (result.ContainsKey(key))
                    return;
                if (depth >= 32 || result.Count + visiting.Count >= 256)
                    throw new InvalidOperationException("Catalog dependency graph exceeds bounds.");
                var row = await context.ReadCatalog(connection, transaction, key, lockRows, ct)
            .ConfigureAwait(false) ?? throw new InvalidOperationException($"Missing exact catalog dependency: {key}.");
                if (!(allowRootDraft && key == root) && (row.Status != CatalogLifecycleStatus.Published || row.EffectiveFromUtc > at))
                    throw new InvalidOperationException($"Catalog dependency is not effective and Published: {key}.");
                visiting.Add(key);
                foreach (var dependency in Dependencies(row.Definition))
                    await Visit(dependency, depth + 1)
            .ConfigureAwait(false);
                visiting.Remove(key);
                result.Add(key, row);
            }
        }

        /// <summary>Reads stored graph and policy facts without invoking application validation.</summary>
        internal async Task<StrategyCatalogQualificationFacts> ReadStrategyCatalogQualificationCoreAsync(NpgsqlConnection connection,
            NpgsqlTransaction transaction, CatalogKey root, DateTime at, bool allowRootDraft, bool lockRows, CancellationToken token)
        {
            var graph = await context.LoadCatalogGraph(connection, transaction, root, at, allowRootDraft, lockRows, token)
                .ConfigureAwait(false);
            var definitions = graph.Values.OrderBy(item => item.Definition.Key.Kind)
                .ThenBy(item => item.Definition.Key.Id).ThenBy(item => item.Definition.Key.Version).ToArray();
            var references = definitions.SelectMany(item => item.Definition.PipelineParameters)
                .Select(item => (item.Kind, item.Id, item.Version)).Distinct().OrderBy(item => item.Kind)
                .ThenBy(item => item.Id).ThenBy(item => item.Version);
            var policies = new List<CatalogPipelinePolicyFact>();
            foreach (var (kind, id, version) in references)
            {
                await using var command = context.Command(connection, transaction,
                    ConfigurationDbSql.GetPipelinePolicySelection(kind, lockRows), new GuidVersion(id, version));
                await using var reader = await command.ExecuteReaderAsync(token)
                    .ConfigureAwait(false);
                if (await reader.ReadAsync(token)
                    .ConfigureAwait(false))
                    policies.Add(ConfigurationDbContext.MapToCatalogPipelinePolicy(reader, kind, id, version));
            }
            return new(root, at, definitions, [.. policies]);
        }

        /// <summary>Reads one catalog version on the supplied connection and verifies its persisted identity and content hash.</summary>
        /// <param name = "connection">The PostgreSQL connection.</param>
        /// <param name = "transaction">The caller-owned transaction.</param>
        /// <param name = "key">The key.</param>
        /// <param name = "lockRow">The lock row.</param>
        /// <param name = "ct">The cancellation token.</param>
        /// <returns>The read catalog result.</returns>
        internal async Task<StoredStrategyCatalogDefinition?> ReadCatalog(NpgsqlConnection connection, NpgsqlTransaction? transaction, CatalogKey key, bool lockRow, CancellationToken ct)
        {
            var command = context.Command(connection, transaction, ConfigurationDbSql.GetStrategyCatalog(lockRow), new CatalogVersion((short)key.Kind, key.Id, key.Version));
            await using var commandDisposal = command.ConfigureAwait(false);
            var reader = await command.ExecuteReaderAsync(ct)
            .ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            if (!await reader.ReadAsync(ct)
            .ConfigureAwait(false))
                return null;
            var stored = ConfigurationDbContext.MapToStrategyCatalog(new AdoNetDataRecord().SetReader(reader));
            if (stored.Definition.Key != key || !string.Equals(ContentHash(stored.Definition), stored.ContentHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Catalog metadata/content hash mismatch.");
            return stored;
        }

        /// <summary>Opens an independent PostgreSQL connection and disposes it if opening fails.</summary>
        /// <param name = "ct">The cancellation token.</param>
        /// <returns>The open catalog result.</returns>
        internal async Task<NpgsqlConnection> OpenCatalogAsync(CancellationToken ct)
        {
            var connection = context.CreateConnection().As<NpgsqlConnection>(context.ConnectionString);
            try
            {
                await connection.OpenAsync(ct)
            .ConfigureAwait(false);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync()
            .ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>Acquires the transaction-scoped advisory lock for catalog mutations.</summary>
        /// <param name = "c">The PostgreSQL connection.</param>
        /// <param name = "tx">The caller-owned transaction.</param>
        /// <param name = "ct">The cancellation token.</param>
        /// <returns>The catalog write lock result.</returns>
        internal Task<int> CatalogWriteLock(NpgsqlConnection c, NpgsqlTransaction tx, CancellationToken ct) => context.Execute(c, tx, ConfigurationDbSql.CatalogWriteLock, ct, new LongValue(ConfigurationSchemaSql.StrategyCatalog.WriterLock));
        /// <summary>Executes a bound SQL command on the supplied connection and transaction.</summary>
        /// <param name = "c">The PostgreSQL connection.</param>
        /// <param name = "tx">The caller-owned transaction.</param>
        /// <param name = "sql">The SQL statement from the configuration statement catalog.</param>
        /// <param name = "ct">The cancellation token.</param>
        /// <param name = "parameters">The typed parameter bindings.</param>
        /// <returns>The execute result.</returns>
        internal async Task<int> Execute(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, CancellationToken ct, IBindValue? parameters = null)
        {
            var command = context.Command(c, tx, sql, parameters);
            await using var commandDisposal = command.ConfigureAwait(false);
            return await command.ExecuteNonQueryAsync(ct)
            .ConfigureAwait(false);
        }

        /// <summary>Executes a bound SQL scalar query on the supplied connection and transaction.</summary>
        /// <param name = "c">The PostgreSQL connection.</param>
        /// <param name = "tx">The caller-owned transaction.</param>
        /// <param name = "sql">The SQL statement from the configuration statement catalog.</param>
        /// <param name = "ct">The cancellation token.</param>
        /// <param name = "parameters">The typed parameter bindings.</param>
        /// <returns>The scalar result.</returns>
        internal async Task<object?> Scalar(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, CancellationToken ct, IBindValue? parameters = null)
        {
            var command = context.Command(c, tx, sql, parameters);
            await using var commandDisposal = command.ConfigureAwait(false);
            return await command.ExecuteScalarAsync(ct)
            .ConfigureAwait(false);
        }

        /// <summary>Creates a PostgreSQL command with the supplied transaction and typed bound parameters.</summary>
        /// <param name = "c">The PostgreSQL connection.</param>
        /// <param name = "tx">The caller-owned transaction.</param>
        /// <param name = "sql">The SQL statement from the configuration statement catalog.</param>
        /// <param name = "parameters">The typed parameter bindings.</param>
        /// <returns>The command result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
        internal NpgsqlCommand Command(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, IBindValue? parameters = null)
        {
            var command = new NpgsqlCommand(sql, c, tx);
            if (parameters?.Bind()is object?[] values)
                PostgresValueBinding.AddParameters(command, values);
            return command;
        }

        internal object?[] BindProjection(IBindValue? values) => values?.Bind() is object?[] parameters ? parameters : [];
            /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
        internal async Task<string> InsertStrategyCatalogDraftCoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            StrategyCatalogDefinition definition, int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken)
        {
            var d = Canonicalize(definition);
            var hash = CanonicalContentHash(d);
            var json = JsonSerializer.Serialize(d, JsonOptions);
            await context.CatalogWriteLock(connection, transaction, cancellationToken)
            .ConfigureAwait(false);
            var latest = await context.Scalar(connection, transaction, ConfigurationDbSql.GetLatestStrategyCatalogVersion, cancellationToken, new CatalogIdentity((short)d.Key.Kind, d.Key.Id))
            .ConfigureAwait(false);
            if (Convert.ToInt32(latest, System.Globalization.CultureInfo.InvariantCulture) != expectedPreviousVersion)
                throw new InvalidOperationException("Catalog authoring conflict: expected previous version does not match.");
            var code = await context.Scalar(connection, transaction, ConfigurationDbSql.GetStrategyCatalogIdentityCode, cancellationToken, new CatalogIdentity((short)d.Key.Kind, d.Key.Id))
            .ConfigureAwait(false);
            if (code is string existing && !string.Equals(existing, d.Code, StringComparison.Ordinal))
                throw new InvalidOperationException("Catalog identity code is immutable.");
            // Exact dependencies must exist even for drafts. They need not be published until publication.
            foreach (var key in Dependencies(d))
                if (await context.ReadCatalog(connection, transaction, key, true, cancellationToken)
            .ConfigureAwait(false)is null)
                    throw new InvalidOperationException($"Missing catalog dependency: {key}.");
            if (d.Key.Kind == StrategyCatalogKind.ParameterSet)
            {
                var schema = await context.ReadCatalog(connection, transaction, d.Parent!, true, cancellationToken)
            .ConfigureAwait(false);
                ValidateParameters(ReadShape(schema!.Definition.Settings), d.Settings);
            }

            await context.MaterializeCatalogDraftAsync(connection, transaction, d, hash, json, DateTime.UtcNow.CatalogNow(), createdBy, cancellationToken)
            .ConfigureAwait(false);
            return hash;
        }
        /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
        internal async Task MaterializeCatalogDraftAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            StrategyCatalogDefinition d, string hash, string json, DateTime now, string principal, CancellationToken token)
        {
            await context.Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogIdentity, token, new InsertCatalogIdentity((short)d.Key.Kind, d.Key.Id, d.Code, now, principal))
            .ConfigureAwait(false);
            await context.Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogVersion, token, new InsertCatalogVersion(json, hash, now, principal))
            .ConfigureAwait(false);
            foreach (var child in ConfigurationSchemaSql.StrategyCatalog.Children)
                await context.Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogChildren(child), token, new TextValue(json))
            .ConfigureAwait(false);
            await context.Execute(connection, transaction, ConfigurationDbSql.SealStrategyCatalogVersion, token, new CatalogVersion((short)d.Key.Kind, d.Key.Id, d.Key.Version))
            .ConfigureAwait(false);
        }
        /// <summary>Fences a prepared publication against the current catalog and pipeline policy facts.</summary>
        internal async Task PublishPreparedStrategyCatalogCoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            StrategyCatalogPublicationProof proof, string expectedContentHash, string publishedBy, CancellationToken token)
        {
            await context.Execute(connection, transaction, ConfigurationDbSql.AcquireParameterWriterLock, token)
                .ConfigureAwait(false);
            await context.CatalogWriteLock(connection, transaction, token)
                .ConfigureAwait(false);
            var key = proof.Root;
            var facts = await context.ReadStrategyCatalogQualificationCoreAsync(connection, transaction, key,
                proof.EffectiveFromUtc, true, lockRows: true, token)
                    .ConfigureAwait(false);
            if (!string.Equals(facts.Fingerprint(), proof.Fingerprint, StringComparison.Ordinal))
                throw new InvalidOperationException("The qualified catalog graph or policy changed before publication.");
            var parameters = new PublishCatalog((short)key.Kind, key.Id, key.Version, proof.EffectiveFromUtc, publishedBy, expectedContentHash);
            var count = await context.Execute(connection, transaction, ConfigurationDbSql.PublishStrategyCatalog, token, parameters)
                .ConfigureAwait(false);
            if (count != 1)
                throw new InvalidOperationException("Catalog publication conflict.");
            await context.Execute(connection, transaction, ConfigurationDbSql.TypedCatalog.AdvanceRevisionFence, token)
                .ConfigureAwait(false);
        }

        /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
        internal async Task RetireStrategyCatalogCoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            CatalogKey key, string expectedContentHash, DateTime retiredAtUtc, string retiredBy, CancellationToken cancellationToken)
        {
            await context.Execute(connection, transaction, ConfigurationDbSql.AcquireParameterWriterLock, cancellationToken)
            .ConfigureAwait(false);
            await context.CatalogWriteLock(connection, transaction, cancellationToken)
            .ConfigureAwait(false);
            var current = await context.ReadCatalog(connection, transaction, key, true, cancellationToken)
            .ConfigureAwait(false);
            if (current is null || current.Status != CatalogLifecycleStatus.Published || !string.Equals(current.ContentHash, expectedContentHash, StringComparison.Ordinal) || retiredAtUtc < current.EffectiveFromUtc)
                throw new InvalidOperationException("Retire requires the exact Published version and a timestamp at or after publication.");
            await context.Execute(connection, transaction, ConfigurationDbSql.RetireStrategyCatalog, cancellationToken, new RetireCatalog((short)key.Kind, key.Id, key.Version, retiredAtUtc, retiredBy))
            .ConfigureAwait(false);
            await context.Execute(connection, transaction, ConfigurationDbSql.TypedCatalog.AdvanceRevisionFence, cancellationToken)
            .ConfigureAwait(false);
        }
        /// <summary>Executes repository preparation under the owning context or caller transaction.</summary>
        internal async Task MaterializeCatalogVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            StoredStrategyCatalogDefinition accepted, StoredStrategyCatalogDefinition? previous, CancellationToken token)
        {
            var definition = Canonicalize(accepted.Definition);
            var key = definition.Key;
            if (previous is null)
            {
                var json = JsonSerializer.Serialize(definition, JsonOptions);
                await context.MaterializeCatalogDraftAsync(connection, transaction, definition, accepted.ContentHash, json,
                    accepted.CreatedUtc, accepted.CreatedBy, token)
            .ConfigureAwait(false);
            }
            if (accepted.EffectiveFromUtc is { } effective && (previous is null || previous.Status == CatalogLifecycleStatus.Draft))
            {
                var count = await context.Execute(connection, transaction, ConfigurationDbSql.PublishStrategyCatalog, token,
                    new PublishCatalog((short)key.Kind, key.Id, key.Version, effective, accepted.PublishedBy!, accepted.ContentHash))
            .ConfigureAwait(false);
                if (count != 1)
                    throw new InvalidDataException("Accepted catalog publication could not be materialized.");
            }
            if (accepted.RetiredAtUtc is { } retired && (previous is null || previous.Status != CatalogLifecycleStatus.Retired))
            {
                var count = await context.Execute(connection, transaction, ConfigurationDbSql.RetireStrategyCatalog, token,
                    new RetireCatalog((short)key.Kind, key.Id, key.Version, retired, accepted.RetiredBy!))
            .ConfigureAwait(false);
                if (count != 1)
                    throw new InvalidDataException("Accepted catalog retirement could not be materialized.");
            }
        }
    }

    extension(VolatilitySeriesDefinition definition)
    {
        /// <summary>Rejects a persisted volatility definition with invalid identity, governance or observation limits.</summary>
        internal void ValidateStoredVolatilityDefinition()
        {
            if (definition is null)
                throw new InvalidDataException("Stored volatility series definition is missing.");
            if (definition.Identity is null || string.IsNullOrWhiteSpace(definition.Identity.SeriesId) || string.IsNullOrWhiteSpace(definition.Identity.MethodologyVersion))
                throw new InvalidDataException("Stored volatility series identity is invalid.");
            if (definition.SchemaVersion != VolatilitySeriesDefinition.CurrentSchemaVersion || string.IsNullOrWhiteSpace(definition.DataIdentity.Environment) || string.IsNullOrWhiteSpace(definition.Governance.ApprovedConfigurationVersion) || string.IsNullOrWhiteSpace(definition.Governance.Owner) || string.IsNullOrWhiteSpace(definition.Governance.ApprovalEvidenceId) || definition.Governance.EffectiveFromUtc.Offset != TimeSpan.Zero || definition.Governance.EffectiveUntilUtc is { } until && until.Offset != TimeSpan.Zero || definition.Governance.EffectiveUntilUtc <= definition.Governance.EffectiveFromUtc || definition.Construction.IntradayCoalescingInterval <= TimeSpan.Zero || definition.Construction.MaximumIntradayCheckpointsPerValueDate <= 0)
                throw new InvalidDataException("Stored volatility series definition is invalid.");
        }
    }

    extension(string value)
    {
        /// <summary>Calculates the lowercase SHA-256 fingerprint of a UTF-8 configuration payload.</summary>
        /// <returns>The sha256 result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1308", Justification = "Persisted SHA-256 fingerprints use canonical lowercase hexadecimal; changing case would invalidate existing evidence identities.")]
        internal string Sha256() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    extension(DateTime timestamp)
    {
        /// <summary>Normalizes a UTC catalog timestamp to PostgreSQL microsecond precision.</summary>
        /// <returns>The catalog now result.</returns>
        internal DateTime CatalogNow() => new(timestamp.Ticks / 10 * 10, DateTimeKind.Utc);
    }
    extension(IEnlistedPostgresTransaction transaction)
    {
    internal async Task WriteTypedValueAsync(ParameterSetValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        // Freeze mutable child arrays before hashing or binding them.
        value = ParameterValueCodec.Snapshot(value);
        var rows = ParameterValueMapping.Write(value);
        var hash = ParameterValueCodec.Hash(value);
        await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.Lock, [value.SetId.ToString("D")], cancellationToken)
            .ConfigureAwait(false);
        var existing = await transaction.ScalarAsync(ConfigurationDbSql.TypedParameters.Hash, [value.SetId, value.Version], cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals((string)existing, hash, StringComparison.Ordinal)) throw new InvalidOperationException("PARAM.IMMUTABLE_CONTENT_CONFLICT");
            // Verify rows as well: a header alone is not proof that the projection is complete.
            await transaction.ReadTypedValueAsync( value.SetId, value.Version, cancellationToken)
            .ConfigureAwait(false);
            return;
        }
        await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.InsertHeader,
            [value.SetId, value.Version, value.ComponentCode, value.SchemaVersion, hash, ParameterValueCodec.Codec], cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in rows)
            await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.Insert(row.Table),
                [value.SetId, value.Version, row.Path, row.ParentPath, row.Ordinal, ..row.Values], cancellationToken)
            .ConfigureAwait(false);
    }
    internal async Task<ParameterSetValue?> ReadTypedValueAsync(Guid setId, int version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var headers = await transaction.QueryAsync(ConfigurationDbSql.TypedParameters.ReadHeader, [setId, version], ConfigurationDbContext.MapToReadTypedValueRow1, cancellationToken)
            .ConfigureAwait(false);
        if (headers.Count == 0) return null;
        var header = headers.Single();
        if (header.Codec != ParameterValueCodec.Codec) throw new InvalidOperationException("PARAM.CODEC_UNSUPPORTED");
        var rows = new List<ParameterValueMapping.Row>();
        foreach (var table in ParameterValueMapping.TablesFor(header.Component))
        {
            var batch = await transaction.QueryAsync(ConfigurationDbSql.TypedParameters.Read(table), [setId, version], (reader) => ConfigurationDbContext.MapToReadTypedValueRow2(reader, table), cancellationToken)
            .ConfigureAwait(false);
            rows.AddRange(batch);
        }
        var value = ParameterValueMapping.Read(header.Component, rows);
        if (value.SetId != setId || value.Version != version || value.SchemaVersion != header.Schema ||
            !string.Equals(ParameterValueCodec.Hash(value), header.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException("PARAM.PROJECTION_CONTENT_MISMATCH");
        return value;
    }
}

}
