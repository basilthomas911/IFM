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

        /// <summary>Applies an accepted assignment fact with operation deduplication and revision-order checks.</summary>
        /// <param name = "fact">The fact.</param>
        /// <param name = "scalar">The scalar-query executor on the current transaction.</param>
        /// <param name = "execute">The command executor on the current transaction.</param>
        /// <param name = "token">The cancellation token.</param>
        /// <returns>A task representing the persistence operation.</returns>
        internal async Task ProjectParameterAssignmentCoreAsync(ParameterAssignmentChangedEvent fact, Func<string, CancellationToken, IBindValue?, Task<object?>> scalar, Func<string, CancellationToken, IBindValue?, Task<int>> execute, CancellationToken token)
        {
            var value = JsonSerializer.Deserialize<ParameterAssignmentRevision>(fact.AssignmentJson) ?? throw new InvalidDataException("Invalid assignment.");
            if (value.AssignmentId != fact.EntityId.AssignmentId || value.Revision != fact.Revision)
                throw new InvalidDataException("Assignment identity mismatch.");
            await (scalar(ConfigurationDbSql.AcquireNamedAdvisoryLock, token, new TextValue("parameter-assignment:" + fact.EntityId.Format())))
            .ConfigureAwait(false);
            var receipt = await (scalar(ConfigurationDbSql.GetAssignmentOperationReceipt, token, new GuidValue(fact.CommandId)))
            .ConfigureAwait(false);
            if (receipt is string prior)
            {
                if (!string.Equals(prior, fact.RequestHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
                return;
            }

            var revision = Convert.ToInt64(await (scalar(ConfigurationDbSql.GetAssignmentRevision, token, new GuidValue(value.AssignmentId)))
            .ConfigureAwait(false) ?? 0L, System.Globalization.CultureInfo.InvariantCulture);
            if (revision + 1 != value.Revision)
                throw new InvalidOperationException("PARAM.PROJECTION_ORDER");
            await (execute(ConfigurationDbSql.UpsertParameterAssignment, token, new UpsertAssignment(value.AssignmentId, value.Revision, fact.AssignmentJson)))
            .ConfigureAwait(false);
            await (execute(ConfigurationDbSql.InsertParameterAssignmentRevision, token, new InsertAssignmentRevision(value.AssignmentId, value.Revision, fact.CommandId, fact.RequestHash, fact.AssignmentJson)))
            .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fact.AuditJson))
            {
                var audit = JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson) ?? throw new InvalidDataException("Invalid audit entry.");
                if (audit.OperationId != fact.CommandId || audit.Revision != fact.Revision)
                    throw new InvalidDataException("Audit identity mismatch.");
                await (execute(ConfigurationDbSql.InsertParameterSetAudit, token, new InsertParameterAudit(audit.OperationId, audit.EntityId, audit.Revision, fact.AuditJson)))
            .ConfigureAwait(false);
            }
        }

        /// <summary>Applies an accepted parameter-set fact with operation deduplication and revision-order checks.</summary>
        /// <param name = "fact">The fact.</param>
        /// <param name = "scalar">The scalar-query executor on the current transaction.</param>
        /// <param name = "execute">The command executor on the current transaction.</param>
        /// <param name = "token">The cancellation token.</param>
        /// <returns>A task representing the persistence operation.</returns>
        internal async Task ProjectParameterSetCoreAsync(IParameterSetFact fact, Func<string, CancellationToken, IBindValue?, Task<object?>> scalar, Func<string, CancellationToken, IBindValue?, Task<int>> execute, CancellationToken token)
        {
            var value = JsonSerializer.Deserialize<ParameterSetVersion>(fact.VersionJson) ?? throw new InvalidDataException("Invalid version fact.");
            await (scalar(ConfigurationDbSql.AcquireNamedAdvisoryLock, token, new TextValue(fact.EntityId.Format())))
            .ConfigureAwait(false);
            var receipt = await (scalar(ConfigurationDbSql.GetParameterOperationReceipt, token, new GuidValue(fact.CommandId)))
            .ConfigureAwait(false);
            if (receipt is string prior)
            {
                if (!string.Equals(prior, fact.RequestHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH");
                return;
            }

            var revision = Convert.ToInt64(await (scalar(ConfigurationDbSql.GetParameterSetRevision, token, new GuidValue(fact.EntityId.SetId)))
            .ConfigureAwait(false) ?? 0L, System.Globalization.CultureInfo.InvariantCulture);
            if (revision + 1 != fact.Revision)
                throw new InvalidOperationException("PARAM.PROJECTION_ORDER");
            await (execute(ConfigurationDbSql.UpsertParameterSet, token, new UpsertParameterSet(fact.EntityId.SetId, value.Reference.ComponentCode, value.Name, value.Description, fact.Revision)))
            .ConfigureAwait(false);
            var hash = await (scalar(ConfigurationDbSql.GetParameterSetVersionHash, token, new GuidVersion(fact.EntityId.SetId, value.Reference.Version)))
            .ConfigureAwait(false);
            if (hash is string existing && !string.Equals(existing, value.Reference.PayloadSha256, StringComparison.Ordinal))
                throw new InvalidOperationException("PARAM.VERSION_IMMUTABLE");
            await (execute(ConfigurationDbSql.UpsertParameterSetVersion, token, new UpsertParameterSetVersion(fact.EntityId.SetId, value.Reference.Version, value.Reference.PayloadSha256, (short)value.Status, fact.VersionJson)))
            .ConfigureAwait(false);
            await (execute(ConfigurationDbSql.InsertParameterOperation, token, new InsertParameterOperation(fact.CommandId, fact.EntityId.SetId, fact.Revision, fact.RequestHash, value.Reference.Version, fact.ReceivedOn == default ? value.CreatedAtUtc : fact.ReceivedOn)))
            .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(fact.AuditJson))
            {
                var audit = JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson) ?? throw new InvalidDataException("Invalid audit entry.");
                if (audit.OperationId != fact.CommandId || audit.Revision != fact.Revision)
                    throw new InvalidDataException("Audit identity mismatch.");
                await (execute(ConfigurationDbSql.InsertParameterSetAudit, token, new InsertParameterAudit(audit.OperationId, audit.EntityId, audit.Revision, fact.AuditJson)))
            .ConfigureAwait(false);
            }

            if (value.Reference.Version == 1 && value.LegacySource is { } source)
            {
                var legacyReference = new LegacyParameterReference(source.Kind, source.SetId, source.Version, value.Reference.SetId, value.Reference.Version, source.PayloadSha256, source.Codec);
                await (execute(ConfigurationDbSql.InsertLegacyParameterReference, token, legacyReference))
            .ConfigureAwait(false);
                var same = await (scalar(ConfigurationDbSql.IsLegacyParameterReferenceEquivalent, token, new VerifyLegacyParameterReference(source.Kind, source.SetId, source.Version, value.Reference.SetId, source.PayloadSha256, source.Codec)))
            .ConfigureAwait(false);
                if (same is not true)
                    throw new InvalidDataException("PARAM.LEGACY_MAPPING_CONFLICT");
            }
        }

        /// <summary>Applies an accepted startup fact and verifies its projection completion.</summary>
        /// <param name = "fact">The fact.</param>
        /// <param name = "scalar">The scalar-query executor on the current transaction.</param>
        /// <param name = "execute">The command executor on the current transaction.</param>
        /// <param name = "token">The cancellation token.</param>
        /// <returns>A task representing the persistence operation.</returns>
        internal async Task ProjectParameterStartupCoreAsync(ParameterStartupChangedEvent fact, Func<string, CancellationToken, IBindValue?, Task<object?>> scalar, Func<string, CancellationToken, IBindValue?, Task<int>> execute, CancellationToken token)
        {
            if (!string.IsNullOrEmpty(fact.ReportJson))
            {
                var report = new StartupReport(fact.RunId, fact.Revision, fact.ReportJson);
                await (execute(ConfigurationDbSql.InsertParameterStartupReport, token, report))
            .ConfigureAwait(false);
                var same = await (scalar(ConfigurationDbSql.IsParameterStartupReportEquivalent, token, report))
            .ConfigureAwait(false);
                if (same is not true)
                    throw new InvalidDataException("PARAM.STARTUP_REPORT_IMMUTABLE");
            }
            else if (!fact.Released)
            {
                var run = new StartupRun(fact.RunId, fact.RunJson);
                await (execute(ConfigurationDbSql.InsertParameterStartupRun, token, run))
            .ConfigureAwait(false);
                var same = await (scalar(ConfigurationDbSql.IsParameterStartupRunEquivalent, token, run))
            .ConfigureAwait(false);
                if (same is not true)
                    throw new InvalidDataException("PARAM.STARTUP_IMMUTABLE");
            }
            else
            {
                var count = await (execute(ConfigurationDbSql.ReleaseParameterStartupRun, token, new GuidValue(fact.RunId)))
            .ConfigureAwait(false);
                if (count != 1)
                    throw new InvalidOperationException("PARAM.STARTUP_PROJECTION_PENDING");
            }
        }

        /// <summary>Converts typed configuration bindings into parameters for the enlisted PostgreSQL transaction.</summary>
        /// <param name = "values">The typed parameter bindings.</param>
        /// <returns>The bind projection result.</returns>
        internal object? [] BindProjection(IBindValue? values) => values?.Bind()is NpgsqlParameter[] parameters ? parameters : [];
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
