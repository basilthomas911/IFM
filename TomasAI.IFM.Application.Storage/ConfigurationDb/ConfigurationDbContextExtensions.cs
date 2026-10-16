using System.Text.Json;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Framework.Storage;
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
        internal async Task<Dictionary<CatalogKey, StoredStrategyCatalogDefinition>> LoadCatalogGraph(NpgsqlConnection connection, NpgsqlTransaction transaction, CatalogKey root, DateTime at, bool allowRootDraft, CancellationToken ct)
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
                var row = await context.ReadCatalog(connection, transaction, key, true, ct)
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

        /// <summary>Checks stored dependencies, capabilities and effective policy bindings before publication or deployment resolution.</summary>
        /// <param name = "connection">The PostgreSQL connection.</param>
        /// <param name = "transaction">The caller-owned transaction.</param>
        /// <param name = "graph">The exact stored catalog dependency graph.</param>
        /// <param name = "at">The UTC effective timestamp.</param>
        /// <param name = "ct">The cancellation token.</param>
        /// <returns>A task representing the persistence operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
        internal async Task ValidateCatalogGraph(NpgsqlConnection connection, NpgsqlTransaction transaction, Dictionary<CatalogKey, StoredStrategyCatalogDefinition> graph, DateTime at, CancellationToken ct)
        {
            foreach (var stored in graph.Values)
            {
                ct.ThrowIfCancellationRequested();
                var d = stored.Definition;
                ValidateForPublication(d, graph);
                foreach (var requirement in d.Capabilities)
                    ValidateCapability(requirement, d);
                if (d.Key.Kind == StrategyCatalogKind.ParameterSet)
                    foreach (var validator in graph[d.Parent!].Definition.Capabilities.Where(c => string.Equals(c.Role, "validator", StringComparison.Ordinal)))
                        ValidateCapability(validator, d);
                if (d.Products.Length > 0 || d.LegacyFamilies.Length > 0)
                {
                    if (context.CatalogReferences is null)
                        throw new InvalidOperationException("Catalog external reference validator is not registered.");
                    foreach (var product in d.Products)
                        await context.CatalogReferences.ValidateProductAsync(product, ct)
            .ConfigureAwait(false);
                    foreach (var family in d.LegacyFamilies)
                        await context.CatalogReferences.ValidateLegacyFamilyAsync(family, Freeze(d), ct)
            .ConfigureAwait(false);
                }

                foreach (var parameter in d.PipelineParameters)
                {
                    var command = context.Command(connection, transaction, ConfigurationDbSql.GetPipelinePolicyForUpdate(parameter.Kind), new GuidVersion(parameter.Id, parameter.Version));
                    await using var commandDisposal = command.ConfigureAwait(false);
                    var reader = await command.ExecuteReaderAsync(ct)
            .ConfigureAwait(false);
                    await using var readerDisposal = reader.ConfigureAwait(false);
                    if (!await reader.ReadAsync(ct)
            .ConfigureAwait(false) || !string.Equals(reader.GetString(0), parameter.Hash, StringComparison.Ordinal) || reader.GetInt16(1) != 1 || (await reader.IsDBNullAsync(2, ct)
            .ConfigureAwait(false)) || reader.GetDateTime(2) > at)
                        throw new InvalidOperationException("Pipeline parameter reference is missing, mismatched or not effective and Published.");
                    // These kinds have qualified owning schemas. Other existing pipeline kinds retain their own publication path.
                    if (parameter.Kind is CatalogPipelineParameterKind.TradeSelection or CatalogPipelineParameterKind.OrderComposition or CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow or CatalogPipelineParameterKind.MarketConditionAssessment or CatalogPipelineParameterKind.RegimeDiscovery or CatalogPipelineParameterKind.RiskManagement)
                        TradeSelectionContracts.ValidatePipelinePolicy(new SelectionPipelinePolicySnapshot { Kind = parameter.Kind, Id = parameter.Id, Version = parameter.Version, PayloadSha256 = reader.GetString(0), PayloadJson = reader.GetString(3), SchemaVersion = reader.GetInt16(4) });
                    if (parameter.Kind == CatalogPipelineParameterKind.RiskManagement && d.Key.Kind == StrategyCatalogKind.Deployment)
                    {
                        var risk = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskParameterSet.Read(reader.GetString(3));
                        if (risk.TargetHorizon != d.Horizon || d.Products.Any(product => !string.Equals(product.Symbol, risk.Root, StringComparison.Ordinal) || !string.Equals(product.Currency, risk.Currency, StringComparison.Ordinal)))
                            throw new InvalidOperationException("Risk policy horizon/product scope does not match the deployment.");
                    }
                }
            }

            void ValidateCapability(CatalogCapability capability, StrategyCatalogDefinition owner)
            {
                if (context.CatalogCapabilities is null)
                    throw new InvalidOperationException($"Catalog capability registry is not registered: {capability.Code}.");
                // A registry is trusted server code, but receives a defensive copy to preserve the frozen write/hash.
                var copy = graph.ToDictionary(x => x.Key, x => x.Value with { Definition = Freeze(x.Value.Definition) });
                context.CatalogCapabilities.Validate(capability, Freeze(owner), copy);
            }
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
            if (parameters?.Bind()is NpgsqlParameter[] values)
                command.Parameters.AddRange(values);
            return command;
        }

        internal object?[] BindProjection(IBindValue? values) => values?.Bind() is NpgsqlParameter[] parameters ? parameters : [];
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
}
