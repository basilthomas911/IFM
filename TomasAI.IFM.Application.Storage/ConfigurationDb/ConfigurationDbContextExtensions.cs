using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
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
using MessagePack;
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
    static readonly string[] LegacyBaselineTables = ["parameter_area", "parameter_component", "parameter_schema_version", "intrinsic_time_strategy_workflow_parameter_set", "regime_discovery_parameter_set", "market_condition_parameter_set", "trade_selection_parameter_set", "order_composition_parameter_set", "risk_management_parameter_set", "strategy_position_risk_parameter_set", "market_condition_assessment_parameter_set", "volatility_series_definition", "lookup_definition", "strategy_catalog_identity", "strategy_catalog_version", ..ConfigurationSchemaSql.StrategyCatalog.Children.Select(x => x.Name), "parameter_set", "parameter_set_version", "parameter_operation", "parameter_set_audit", "parameter_assignment", "parameter_assignment_revision", "parameter_legacy_reference", "parameter_startup_run", "parameter_startup_report", "market_data.strategy_option_chain_parameter_version", "market_data.strategy_option_chain_parameter_current"];
    static readonly string[] CurrentBaselineTables=[..LegacyBaselineTables.Where(x=>!x.StartsWith("market_data.strategy_option_chain_parameter_",StringComparison.Ordinal)),"market_data.strategy_option_chain_policy_version","market_data.strategy_option_chain_policy_current","parameter_value_version",..ParameterValueMapping.Tables.Select(x=>x.Name),
        "parameter_projection_record","parameter_catalog_operation","parameter_catalog_startup_state","parameter_catalog_migration",..ParameterProjectionMapping.Tables.Select(x=>x.Name)];

    extension(ConfigurationDbContext context)
    {
        /// <summary>Computes the canonical stored projection hash for replay and read verification.</summary>
        internal string HashCatalogProjection<T>(T value)
        {
            var bytes = MessagePackSerializer.Serialize(value, MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None));
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        /// <summary>Writes a relational projection and verifies any immutable duplicate.</summary>
        internal async Task WriteCatalogProjectionAsync<T>(IEnlistedPostgresTransaction tx, Guid id, long version, T value,
            bool replace, CancellationToken token) where T : class
        {
            var root = ParameterProjectionMapping.Roots[typeof(T)];
            var hash = context.HashCatalogProjection(value);
            var previous = await tx.ScalarAsync(ConfigurationDbSql.TypedCatalog.Header, [root.Name, id, version], token)
                .ConfigureAwait(false);
            if (previous is string existing)
            {
                if (existing == hash)
                {
                    await context.ReadCatalogProjectionAsync<T>(tx, id, version, token)
                        .ConfigureAwait(false);
                    return;
                }
                if (!replace)
                    throw new InvalidDataException("PARAM.IMMUTABLE_CONTENT_CONFLICT");
                foreach (var table in ParameterValueMapping.Descendants(root).Reverse())
                    await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.Delete(table), [id, version], token)
                        .ConfigureAwait(false);
            }
            await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.PutHeader, [root.Name, id, version, hash], token)
                .ConfigureAwait(false);
            foreach (var row in ParameterProjectionMapping.Write(value))
                await tx.ExecuteAsync(ConfigurationDbSql.TypedParameters.Insert(row.Table), [id, version, row.Path, row.ParentPath, row.Ordinal, .. row.Values], token)
                    .ConfigureAwait(false);
        }

        /// <summary>Reads a relational projection and verifies the stored content hash.</summary>
        internal async Task<T?> ReadCatalogProjectionAsync<T>(IEnlistedPostgresTransaction tx, Guid id, long version, CancellationToken token) where T : class
        {
            var root = ParameterProjectionMapping.Roots[typeof(T)];
            var hash = await tx.ScalarAsync(ConfigurationDbSql.TypedCatalog.Header, [root.Name, id, version], token)
                .ConfigureAwait(false);
            if (hash is null) return null;
            var rows = new List<ParameterValueMapping.Row>();
            foreach (var table in ParameterValueMapping.Descendants(root))
            {
                var batch = await tx.QueryAsync(ConfigurationDbSql.TypedParameters.Read(table), [id, version], reader => ConfigurationDbContext.MapToParameterProjectionRow(reader, table), token)
                    .ConfigureAwait(false);
                rows.AddRange(batch);
            }
            var result = ParameterProjectionMapping.Read<T>(rows);
            if (context.HashCatalogProjection(result) != (string)hash)
                throw new InvalidDataException("PARAM.PROJECTION_CONTENT_MISMATCH");
            return result;
        }

        /// <summary>Persists an accepted parameter set projection and its operation receipt.</summary>
        internal async Task ProjectCatalogSetAsync(IEnlistedPostgresTransaction tx, IParameterSetFact fact, CancellationToken token)
        {
            var value = fact.ReadVersion();
            var audit = fact.ReadAudit();
            var factHash = context.HashCatalogProjection((fact.EventName, fact.Revision, value, audit));
            if (await tx.IsDuplicateCatalogProjectionAsync("set", fact.EntityId.SetId, fact.Revision, fact.CommandId, fact.RequestHash, factHash, token)
                .ConfigureAwait(false)) return;
            var parameters = value.Values();
            if (parameters.SetId != fact.EntityId.SetId || parameters.Version != value.Reference.Version || parameters.SchemaVersion != value.SchemaVersion || parameters.ComponentCode != value.Reference.ComponentCode)
                throw new InvalidDataException("PARAM.REFERENCE_INVALID");
            if (value.Parameters is not null && value.ReferenceCodec == ParameterValueCodec.Codec && ParameterValueCodec.Hash(parameters) != value.Reference.PayloadSha256)
                throw new InvalidDataException("PARAM.HASH_MISMATCH");
            var old = await context.ReadCatalogProjectionAsync<ParameterVersionMetadata>(tx, parameters.SetId, parameters.Version, token)
                .ConfigureAwait(false);
            if (old is not null)
            {
                if (old.Reference != value.Reference || old.SchemaVersion != value.SchemaVersion || old.CreatedAtUtc != value.CreatedAtUtc || old.CreatedBy != value.CreatedBy || old.LegacySource != value.LegacySource)
                    throw new InvalidDataException("PARAM.VERSION_IMMUTABLE");
                if (old.Status != value.Status && !((old.Status == ParameterVersionStatus.Draft && value.Status == ParameterVersionStatus.Published) || (old.Status == ParameterVersionStatus.Published && value.Status == ParameterVersionStatus.Retired)))
                    throw new InvalidDataException("PARAM.LIFECYCLE_INVALID");
            }
            await context.InsertTypedParameterValueAsync(tx, parameters, token)
                .ConfigureAwait(false);
            await tx.ExecuteAsync(ConfigurationDbSql.UpsertParameterSet, [parameters.SetId, parameters.ComponentCode, value.Name, value.Description, fact.Revision], token)
                .ConfigureAwait(false);
            await context.WriteCatalogProjectionAsync(tx, parameters.SetId, parameters.Version, ParameterVersionMetadata.From(value), true, token)
                .ConfigureAwait(false);
            if (audit is not null)
            {
                if (audit.OperationId != fact.CommandId || audit.Revision != fact.Revision)
                    throw new InvalidDataException("Audit identity mismatch");
                await context.WriteCatalogProjectionAsync(tx, audit.OperationId, 1, audit, false, token)
                    .ConfigureAwait(false);
            }
            if (value.Reference.Version == 1 && value.LegacySource is { } source)
            {
                var legacy = new LegacyParameterReference(source.Kind, source.SetId, source.Version, value.Reference.SetId, value.Reference.Version, source.PayloadSha256, source.Codec);
                await tx.ExecuteAsync(ConfigurationDbSql.InsertLegacyParameterReference, context.BindProjection(legacy), token)
                    .ConfigureAwait(false);
                var same = await tx.ScalarAsync(ConfigurationDbSql.IsLegacyParameterReferenceEquivalent, [source.Kind, source.SetId, source.Version, value.Reference.SetId, source.PayloadSha256, source.Codec], token)
                    .ConfigureAwait(false);
                if (same is not true)
                    throw new InvalidDataException("PARAM.LEGACY_MAPPING_CONFLICT");
            }
            await tx.RecordCatalogProjectionAsync("set", parameters.SetId, fact.Revision, fact.CommandId, fact.RequestHash, parameters.Version, value.CreatedAtUtc, factHash, token)
                .ConfigureAwait(false);
        }

        /// <summary>Persists an accepted assignment projection and its operation receipt.</summary>
        internal async Task ProjectCatalogAssignmentAsync(IEnlistedPostgresTransaction tx, ParameterAssignmentChangedEvent fact, CancellationToken token)
        {
            var value = fact.ReadAssignment();
            var audit = fact.ReadAudit();
            var factHash = context.HashCatalogProjection((value, audit));
            if (value.AssignmentId != fact.EntityId.AssignmentId || value.Revision != fact.Revision)
                throw new InvalidDataException("PARAM.REFERENCE_INVALID");
            if (await tx.IsDuplicateCatalogProjectionAsync("assignment", value.AssignmentId, fact.Revision, fact.CommandId, fact.RequestHash, factHash, token)
                .ConfigureAwait(false)) return;
            await context.WriteCatalogProjectionAsync(tx, value.AssignmentId, value.Revision, value, false, token)
                .ConfigureAwait(false);
            if (audit is not null)
                await context.WriteCatalogProjectionAsync(tx, audit.OperationId, 1, audit, false, token)
                    .ConfigureAwait(false);
            await tx.RecordCatalogProjectionAsync("assignment", value.AssignmentId, fact.Revision, fact.CommandId, fact.RequestHash, value.Reference.Version, value.CreatedAtUtc, factHash, token)
                .ConfigureAwait(false);
        }

        /// <summary>Persists an accepted startup projection and its operation receipt.</summary>
        internal async Task ProjectCatalogStartupAsync(IEnlistedPostgresTransaction tx, ParameterStartupChangedEvent fact, CancellationToken token)
        {
            var run = fact.ReadRun();
            var report = fact.ReadReport();
            var factHash = context.HashCatalogProjection((fact.RunId, fact.Released, run, report));
            if (fact is ParameterStartupCreatedEvent checkpoint)
                factHash = context.HashCatalogProjection((fact.RunId, fact.Released, run, report, checkpoint.ImportedFromRegistry, checkpoint.ReleasedAtCreation));
            var historicalRegistry = fact.EntityId == ParameterStartupEntityId.Registry;
            var kind = historicalRegistry ? "startup" : "startup-run";
            var owner = historicalRegistry ? Guid.Empty : fact.RunId;
            if (!historicalRegistry && fact.EntityId.Id != fact.RunId)
                throw new InvalidDataException("PARAM.STARTUP_OWNER_MISMATCH");
            if (await tx.IsDuplicateCatalogProjectionAsync(kind, owner, fact.Revision, fact.CommandId, factHash, factHash, token)
                .ConfigureAwait(false)) return;
            if (run is not null)
            {
                if (run.RunId != fact.RunId || run.Plan.StartupRunId != fact.RunId)
                    throw new InvalidDataException("PARAM.STARTUP_INVALID");
                foreach (var version in run.Versions)
                {
                    var values = version.Values();
                    if (values.SetId != version.Reference.SetId || values.Version != version.Reference.Version || values.ComponentCode != version.Reference.ComponentCode ||
                        values.SchemaVersion != version.SchemaVersion || version.ReferenceCodec == ParameterValueCodec.Codec && ParameterValueCodec.Hash(values) != version.Reference.PayloadSha256)
                        throw new InvalidDataException("PARAM.STARTUP_VERSION_INVALID");
                    await context.InsertTypedParameterValueAsync(tx, values, token)
                        .ConfigureAwait(false);
                }
                await context.WriteCatalogProjectionAsync(tx, fact.RunId, 1, ParameterStartupProjection.From(run), false, token)
                    .ConfigureAwait(false);
                await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.StartupState, [fact.RunId, fact.Revision, fact is not ParameterStartupCreatedEvent { ReleasedAtCreation: true }], token)
                    .ConfigureAwait(false);
            }
            if (report is not null)
            {
                if (report.RunId != fact.RunId)
                    throw new InvalidDataException("PARAM.STARTUP_REPORT_INVALID");
                await context.WriteCatalogProjectionAsync(tx, fact.RunId, 1, report, false, token)
                    .ConfigureAwait(false);
            }
            else if (run is null && fact.Released)
                await tx.ExecuteAsync(ConfigurationDbSql.TypedCatalog.StartupState, [fact.RunId, fact.Revision, false], token)
                    .ConfigureAwait(false);
            else if (run is null)
                throw new InvalidDataException("PARAM.STARTUP_INVALID");
            await tx.RecordCatalogProjectionAsync(kind, owner, fact.Revision, fact.CommandId, factHash, 0, fact.ReceivedOn, factHash, token)
                .ConfigureAwait(false);
        }


        /// <summary>Locks the configured tables and reads one consistent image and sequence state.</summary>
        internal async Task<ConfigurationBaselineDatabaseImage> CaptureBaselineImageAsync(EnlistedEventTransaction db, CancellationToken token)
        {
            await context.LockBaselineTablesAsync(db, CurrentBaselineTables, token)
                .ConfigureAwait(false);
            var cut = await db.ReadCommittedEventCutAsync(token)
                .ConfigureAwait(false);
            var images = new List<ConfigurationBaselineTable>(CurrentBaselineTables.Length);
            foreach (var table in CurrentBaselineTables)
            {
                var columns = await context.ReadBaselineColumnsAsync(db, table, token)
                    .ConfigureAwait(false);
                var rows = await context.ReadBaselineRowsAsync(db, table, token)
                    .ConfigureAwait(false);
                images.Add(new(table, JsonSerializer.Serialize(columns), rows, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rows)))));
            }
            var sequence = (await db.QueryAsync(ConfigurationDbSql.BaselineLookupSequence, [], ConfigurationDbContext.MapToBaselineLookupSequence, token)
                .ConfigureAwait(false)).Single();
            return new(cut, images.AsReadOnly(), sequence.Last, sequence.Called);
        }

        /// <summary>Rebuilds exact baseline rows into empty configuration tables, allowing identical registry seeds.
        /// Catalog drafts are populated, sealed and restored to their accepted lifecycle with all guards enabled.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "S3267", Justification = "Each row verification awaits a query on the same transaction; a synchronous Where predicate cannot perform this operation.")]
        internal async Task<bool> RestoreBaselineImageAsync(IEnlistedPostgresTransaction db, ConfigurationProjectionBaselineEvent source, CancellationToken token)
        {
            // A checked-in table allowlist is required before using any SQL identifier from an image.
            if (!context.MatchesBaselineLayout(source.SchemaVersion, source.Tables.Select(table => table.Name)))
                throw new InvalidDataException("Configuration baseline layout is not a supported physical table set.");

            await context.LockBaselineTablesAsync(db, context.GetBaselineTableNames(source.SchemaVersion), token)
                .ConfigureAwait(false);
            if (!await context.ClaimConfigurationBaselineCoreAsync(db, source.Id, source.EventId, source.SourceCut, token)
                .ConfigureAwait(false))
                return false;
            var catalogInserted = false;
            foreach (var image in source.Tables)
            {
                var columns = await context.ReadBaselineColumnsAsync(db, image.Name, token)
                    .ConfigureAwait(false);
                if (!string.Equals(JsonSerializer.Serialize(columns), image.ColumnsJson, StringComparison.Ordinal))
                    throw new InvalidDataException("Configuration baseline columns differ from target: " + image.Name);
                var current = await context.ReadBaselineRowsAsync(db, image.Name, token)
                    .ConfigureAwait(false);
                if (string.Equals(current, image.RowsJson, StringComparison.Ordinal))
                    continue;
                if (!string.Equals(current, "[]", StringComparison.Ordinal) && image.Name is not ("parameter_area" or "parameter_component" or "parameter_schema_version"))
                    throw new InvalidOperationException("Configuration baseline requires an empty target table: " + image.Name);
                var rows = image.RowsJson;
                if (string.Equals(image.Name, "strategy_catalog_version", StringComparison.Ordinal))
                {
                    rows = (string)(await db.ScalarAsync(ConfigurationDbSql.PrepareBaselineCatalogDrafts, [rows], token)
                        .ConfigureAwait(false))!;
                    catalogInserted = true;
                }

                var names = string.Join(",", columns.Select(column => context.QuoteBaselineColumn(column.Name)));
                await db.ExecuteAsync(ConfigurationDbSql.InsertBaselineRows(context.QualifyBaselineTable(image.Name), names), [rows], token)
                    .ConfigureAwait(false);
            }

            if (catalogInserted)
                await context.RestoreBaselineCatalogLifecycleAsync(db, source.Tables.Single(x => string.Equals(x.Name, "strategy_catalog_version", StringComparison.Ordinal)).RowsJson, token)
                    .ConfigureAwait(false);
            foreach (var image in source.Tables)
                if (!string.Equals(await context.ReadBaselineRowsAsync(db, image.Name, token)
                    .ConfigureAwait(false), image.RowsJson, StringComparison.Ordinal))
                    throw new InvalidDataException("Configuration baseline row mismatch: " + image.Name);
            // Explicit identity values also restore the next generated lookup id.
            await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineLookupSequence, [source.LookupSequenceLastValue, source.LookupSequenceIsCalled], token)
                .ConfigureAwait(false);
            return true;
        }

        /// <summary>Restores catalog sealing, publication and retirement through the existing guards.</summary>
        async Task RestoreBaselineCatalogLifecycleAsync(IEnlistedPostgresTransaction db, string rows, CancellationToken token)
        {
            await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineCatalogLifecycle1, [rows], token)
                .ConfigureAwait(false);
            await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineCatalogLifecycle2, [rows], token)
                .ConfigureAwait(false);
            await db.ExecuteAsync(ConfigurationDbSql.RestoreBaselineCatalogLifecycle3, [rows], token)
                .ConfigureAwait(false);
        }

        /// <summary>Records a baseline application once and checks replay identity.</summary>
        internal async Task<bool> ClaimConfigurationBaselineCoreAsync(IEnlistedPostgresTransaction db, Guid baselineId, long sourceEventId, long sourceCut, CancellationToken token)
        {
            var existing = await db.ScalarAsync(ConfigurationDbSql.GetBaselineApplication, [baselineId], token)
                .ConfigureAwait(false);
            if (existing is long id)
            {
                if (id != sourceEventId)
                    throw new InvalidDataException("Configuration baseline identity conflict.");
                return false;
            }

            await db.ExecuteAsync(ConfigurationDbSql.InsertBaselineApplication, [baselineId, sourceEventId, sourceCut], token)
                .ConfigureAwait(false);
            return true;
        }

        /// <summary>Locks the allowlisted baseline tables and the application receipt table.</summary>
        Task<int> LockBaselineTablesAsync(IEnlistedPostgresTransaction db, IReadOnlyList<string> tables, CancellationToken token)
            => db.ExecuteAsync(ConfigurationDbSql.LockBaselineTables(string.Join(",", tables.Select(context.QualifyBaselineTable))), [], token);
        /// <summary>Reads an allowlisted table as an ordered JSON image.</summary>
        async Task<string> ReadBaselineRowsAsync(IEnlistedPostgresTransaction db, string table, CancellationToken token)
            => (string)(await db.ScalarAsync(ConfigurationDbSql.ReadBaselineRows(context.QualifyBaselineTable(table)), [], token)
                .ConfigureAwait(false))!;
        /// <summary>Reads the actual column layout of an allowlisted table.</summary>
        async Task<ConfigurationDbContext.BaselineColumnLayout[]> ReadBaselineColumnsAsync(IEnlistedPostgresTransaction db, string table, CancellationToken token)
        {
            var qualified = context.QualifyBaselineTable(table).Split('.');
            return (await db.QueryAsync(ConfigurationDbSql.ReadBaselineColumns, [qualified[0], qualified[1]], ConfigurationDbContext.MapToBaselineColumn, token)
                .ConfigureAwait(false)).ToArray();
        }

        /// <summary>Tests the table layout against the supported physical baseline catalog.</summary>
        internal bool MatchesBaselineLayout(int schemaVersion, IEnumerable<string> tables)
            => tables.SequenceEqual(context.GetBaselineTableNames(schemaVersion), StringComparer.Ordinal);

        /// <summary>Gets the physical tables used by a supported baseline layout.</summary>
        IReadOnlyList<string> GetBaselineTableNames(int schemaVersion)
            => schemaVersion == 1 ? LegacyBaselineTables : CurrentBaselineTables;

        /// <summary>Qualifies a baseline table from the checked-in layout.</summary>
        string QualifyBaselineTable(string name) => name.Contains('.', StringComparison.Ordinal) ? name : "reference_configuration." + name;

        /// <summary>Quotes a column obtained from the database layout.</summary>
        string QuoteBaselineColumn(string name) => "\"" + name.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";


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

        /// <summary>Extracts provider-neutral positional values for an enlisted projection statement.</summary>
        internal object?[] BindProjection(IBindValue? values) => values?.Bind() is object?[] parameters ? parameters : [];

        /// <summary>Writes a prepared draft after fencing its revision and exact schema proof.</summary>
        internal async Task<string> InsertStrategyCatalogDraftCoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
            PreparedStrategyCatalogDraft draft, int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken)
        {
            var d = draft.Definition;
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
            // Exact dependencies are immutable. Verify the caller's schema proof while the write fence is held.
            foreach (var key in Dependencies(d))
            {
                var dependency = await context.ReadCatalog(connection, transaction, key, true, cancellationToken)
                    .ConfigureAwait(false) ?? throw new InvalidOperationException($"Missing catalog dependency: {key}.");
                if (d.Key.Kind == StrategyCatalogKind.ParameterSet && key == d.Parent
                    && !string.Equals(dependency.ContentHash, draft.ParameterSchemaContentHash, StringComparison.Ordinal))
                    throw new InvalidOperationException("The qualified parameter schema changed before the draft was saved.");
            }

            await context.MaterializeCatalogDraftAsync(connection, transaction, d, hash, json, context.BusinessClock.GetUtcNow().UtcDateTime.CatalogNow(), createdBy, cancellationToken)
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
    extension(NpgsqlTransaction transaction)
    {
        /// <summary>Creates a provider command bound to the caller-owned configuration transaction.</summary>
        internal NpgsqlCommand CreateConfigurationSessionCommand(NpgsqlConnection connection, string sql, object?[] values)
        {
            var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 0 };
            PostgresValueBinding.AddParameters(command, values);
            return command;
        }
    }

    extension(IEnlistedPostgresTransaction transaction)
    {
        /// <summary>Fences a projection revision and validates an existing operation receipt.</summary>
        internal async Task<bool> IsDuplicateCatalogProjectionAsync(string kind, Guid entity, long revision, Guid operation, string request, string factHash, CancellationToken token)
        {
            await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.Lock, [kind + ":" + entity.ToString("D")], token)
                .ConfigureAwait(false);
            var receipts = await transaction.QueryAsync(ConfigurationDbSql.TypedCatalog.Receipt, [operation], ConfigurationDbContext.MapToCatalogReceipt, token)
                .ConfigureAwait(false);
            if (receipts.Count != 0)
            {
                if (receipts[0].Request != request || receipts[0].Fact != factHash) throw new InvalidDataException("PARAM.OPERATION_IDENTITY_MISMATCH");
                return true;
            }
            var prior = Convert.ToInt64(await transaction.ScalarAsync(ConfigurationDbSql.TypedCatalog.Revision, [kind, entity], token)
                .ConfigureAwait(false));
            if (prior + 1 != revision) throw new InvalidDataException("PARAM.PROJECTION_ORDER");
            return false;
        }
        /// <summary>Records the accepted projection revision and fact hash.</summary>
        internal Task<int> RecordCatalogProjectionAsync(string kind, Guid entity, long revision, Guid operation, string request, int version, DateTime at, string factHash, CancellationToken token) =>
            transaction.ExecuteAsync(ConfigurationDbSql.TypedCatalog.InsertReceipt, [operation, entity, revision, request, kind, version, at.Ticks, factHash], token);

        /// <summary>Stores an immutable parameter value and verifies any duplicate rows.</summary>
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
                await transaction.ReadTypedValueAsync(value.SetId, value.Version, cancellationToken)
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

        /// <summary>Reads a typed parameter value and verifies its stored identity and content.</summary>
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

/// <summary>Maps the closed parameter contract to typed relational rows. No encoded document is persisted.</summary>
internal static class ParameterValueMapping
{
    internal sealed record Column(string Name, Type Type, PropertyInfo? Property, bool Presence = false);
    internal sealed record Child(PropertyInfo Property, Table Table, bool Collection);
    internal sealed class Table(string name, Type type, Table? parent, bool scalar)
    {
        public string Name { get; } = name;
        public Type Type { get; } = type;
        public Table? Parent { get; } = parent;
        public bool Scalar { get; } = scalar;
        public List<Column> Columns { get; } = [];
        public List<Child> Children { get; } = [];
    }
    internal sealed record Row(Table Table, string Path, string? ParentPath, int Ordinal, object?[] Values);
    static readonly Dictionary<string, Table> Roots = BuildRoots();
    internal static IEnumerable<Table> Tables => Roots.Values.SelectMany(Descendants);
    internal static IEnumerable<Table> TablesFor(string component) => Descendants(Root(component));
    internal static Table Root(string component) => Roots.TryGetValue(component, out var table)
        ? table : throw new ArgumentException("Unknown parameter component", nameof(component));

    static Dictionary<string, Table> BuildRoots()
    {
        var variants = typeof(ParameterSetValue).GetCustomAttributes<UnionAttribute>();
        return variants.Select(x => x.SubType).ToDictionary(type =>
        {
            var value = Activator.CreateInstance(type.GetProperty("Value")!.PropertyType)!;
            return ((ParameterSetValue)Activator.CreateInstance(type, value)!).ComponentCode;
        }, type => Build(type.GetProperty("Value")!.PropertyType, null, "parameter_" + Snake(type.Name.Replace("ParameterValue", "", StringComparison.Ordinal))));
    }
    internal static Table Build(Type type, Table? parent, string name)
    {
        var table = new Table(Identifier(name), type, parent, IsScalar(type));
        if (table.Scalar) { table.Columns.Add(new("value", type, null)); return table; }
        foreach (var property in type.GetProperties().Where(x => x.GetCustomAttribute<KeyAttribute>() is not null)
                     .OrderBy(x => x.GetCustomAttribute<KeyAttribute>()!.IntKey))
        {
            if (IsScalar(property.PropertyType)) table.Columns.Add(new(Snake(property.Name), property.PropertyType, property));
            else
            {
                var element = Element(property.PropertyType);
                table.Columns.Add(new("has_" + Snake(property.Name), typeof(bool), property, true));
                table.Children.Add(new(property, Build(element ?? property.PropertyType, table, name + "_" + Snake(property.Name)), element is not null));
            }
        }
        return table;
    }
    internal static IEnumerable<Table> Descendants(Table table)
    {
        yield return table;
        foreach (var child in table.Children.SelectMany(x => Descendants(x.Table))) yield return child;
    }
    static Type? Element(Type type) => type.IsArray ? type.GetElementType() :
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>) ? type.GetGenericArguments()[0] : null;
    static bool IsPresent(object? value) => value is not null &&
        !(value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(ImmutableArray<>) &&
          (bool)value.GetType().GetProperty("IsDefault")!.GetValue(value)!);
    static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsEnum || type.IsPrimitive || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(Guid) || type == typeof(DateOnly) || type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) || type == typeof(TimeSpan);
    }
    static string Snake(string name) => Regex.Replace(name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    static string Identifier(string name) => name.Length <= 63 ? name : name[..50] + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..12].ToLowerInvariant();
    internal static string SqlType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsEnum || type == typeof(int) || type == typeof(short) || type == typeof(byte) || type == typeof(ushort)) return "integer";
        if (type == typeof(long) || type == typeof(uint)) return "bigint";
        if (type == typeof(decimal) || type == typeof(ulong)) return "numeric";
        if (type == typeof(double)) return "double precision";
        if (type == typeof(float)) return "real";
        if (type == typeof(bool)) return "boolean";
        if (type == typeof(Guid)) return "uuid";
        if (type == typeof(DateOnly)) return "date";
        if (type == typeof(DateTime)) return "bigint";
        if (type == typeof(DateTimeOffset)) return "text";
        if (type == typeof(TimeSpan)) return "interval";
        if (type == typeof(string)) return "text";
        throw new NotSupportedException(type.FullName);
    }
    internal static object? DatabaseValue(object? value, Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (value is null) return null;
        if (type.IsEnum || type == typeof(short) || type == typeof(byte) || type == typeof(ushort)) return Convert.ToInt32(value);
        if (type == typeof(uint)) return Convert.ToInt64(value);
        if (type == typeof(ulong)) return Convert.ToDecimal(value);
        if (value is DateTime date) return date.ToBinary();
        if (value is DateTimeOffset offset) return offset.ToString("O",System.Globalization.CultureInfo.InvariantCulture);
        return value;
    }
    internal static IReadOnlyList<Row> Write(ParameterSetValue value)
    {
        var rows = new List<Row>();
        var instance = value.GetType().GetProperty("Value")!.GetValue(value)!;
        Walk(Root(value.ComponentCode), instance, "$", null, 0, rows);
        return rows;
    }
    internal static void Walk(Table table, object value, string path, string? parent, int ordinal, List<Row> rows)
    {
        rows.Add(new(table, path, parent, ordinal, table.Columns.Select(column => column.Presence
            ? (object)IsPresent(column.Property!.GetValue(value))
            : DatabaseValue(column.Property is null ? value : column.Property.GetValue(value), column.Type)).ToArray()));
        foreach (var child in table.Children)
        {
            var nested = child.Property.GetValue(value);
            if (!IsPresent(nested)) continue;
            var childPath = path + "/" + child.Property.Name;
            if (child.Collection)
            {
                var index = 0;
                foreach (var item in (IEnumerable)nested)
                {
                    if (item is null) throw new ArgumentException("Null collection item at " + childPath);
                    Walk(child.Table, item, childPath + "/" + index, path, index++, rows);
                }
            }
            else Walk(child.Table, nested, childPath, path, 0, rows);
        }
    }
    internal static ParameterSetValue Read(string component, IReadOnlyList<Row> rows)
    {
        var root = Root(component);
        var value = Materialize(root, rows.Single(x => x.Table == root && x.Path == "$"), rows);
        var variant = typeof(ParameterSetValue).GetCustomAttributes<UnionAttribute>().Select(x => x.SubType)
            .Single(x => x.GetProperty("Value")!.PropertyType == root.Type);
        return (ParameterSetValue)Activator.CreateInstance(variant, value)!;
    }
    internal static object Materialize(Table table, Row row, IReadOnlyList<Row> rows)
    {
        if (table.Scalar) return ConvertValue(row.Values[0], table.Type)!;
        var fields = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < table.Columns.Count; i++)
            if (!table.Columns[i].Presence) fields[table.Columns[i].Property!.Name] = ConvertValue(row.Values[i], table.Columns[i].Type);
        foreach (var child in table.Children)
        {
            var present = (bool)row.Values[table.Columns.FindIndex(x => x.Presence && x.Property == child.Property)]!;
            var children = rows.Where(x => x.Table == child.Table && x.ParentPath == row.Path).OrderBy(x => x.Ordinal).ToArray();
            if (!present)
            {
                if (children.Length != 0) throw new InvalidOperationException("Rows exist for absent child " + child.Property.Name);
                fields[child.Property.Name] = null; continue;
            }
            if (!child.Collection) fields[child.Property.Name] = Materialize(child.Table, children.Single(), rows);
            else
            {
                var array = Array.CreateInstance(child.Table.Type, children.Length);
                for (var i = 0; i < children.Length; i++)
                {
                    if (children[i].Ordinal != i) throw new InvalidOperationException("Noncontiguous parameter child ordinals");
                    array.SetValue(Materialize(child.Table, children[i], rows), i);
                }
                fields[child.Property.Name] = child.Property.PropertyType.IsArray ? array :
                    typeof(ParameterValueMapping).GetMethod(nameof(ToImmutable), BindingFlags.Static | BindingFlags.NonPublic)!
                        .MakeGenericMethod(child.Table.Type).Invoke(null, [array]);
            }
        }
        var constructor = table.Type.GetConstructors().OrderByDescending(x => x.GetParameters().Length)
            .First(x => x.GetParameters().All(p => fields.ContainsKey(p.Name!)));
        var instance = constructor.Invoke(constructor.GetParameters().Select(x => fields[x.Name!]).ToArray());
        foreach (var property in table.Type.GetProperties().Where(x => x.SetMethod is not null && fields.ContainsKey(x.Name)))
            property.SetValue(instance, fields[property.Name]);
        return instance;
    }
    static ImmutableArray<T> ToImmutable<T>(T[] values) => ImmutableArray.CreateRange(values);
    static object? ConvertValue(object? value, Type type)
    {
        if (value is null or DBNull) return null;
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsInstanceOfType(value)) return value;
        if (type.IsEnum) return Enum.ToObject(type, value);
        if (type == typeof(DateTime) && value is long ticks) return DateTime.FromBinary(ticks);
        if (type == typeof(DateTimeOffset) && value is string text) return DateTimeOffset.ParseExact(text,"O",System.Globalization.CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Relational version metadata. Concrete values live in the parameter value tables.</summary>
[MessagePackObject(AllowPrivate=true)]
internal sealed record ParameterVersionMetadata(
    [property:Key(0)] ParameterVersionRef Reference,[property:Key(1)] string Name,[property:Key(2)] string Description,
    [property:Key(3)] int SchemaVersion,[property:Key(4)] ParameterVersionStatus Status,
    [property:Key(5)] DateTime CreatedAtUtc,[property:Key(6)] string CreatedBy,
    [property:Key(7)] DateTime? PublishedAtUtc,[property:Key(8)] DateTime? RetiredAtUtc,
    [property:Key(9)] long CatalogRevision,[property:Key(10)] ParameterLegacyReference? LegacySource,
    [property:Key(11)] string ReferenceCodec)
{
    internal static ParameterVersionMetadata From(ParameterSetVersion value)=>new(value.Reference,value.Name,value.Description,value.SchemaVersion,
        value.Status,value.CreatedAtUtc,value.CreatedBy,value.PublishedAtUtc,value.RetiredAtUtc,value.CatalogRevision,value.LegacySource,
        value.Parameters is null ? "sorted-json-v1" : value.ReferenceCodec);
    internal ParameterSetVersion Restore(ParameterSetValue values)=>new(Reference,Name,Description,SchemaVersion,Status,string.Empty,CreatedAtUtc,
        CreatedBy,PublishedAtUtc,RetiredAtUtc,CatalogRevision,LegacySource){Parameters=values,ReferenceCodec=ReferenceCodec};
}
[MessagePackObject(AllowPrivate=true)]
internal sealed record ParameterStartupProjection(
    [property:Key(0)] Guid RunId,[property:Key(1)] ParameterAssignmentRevision[] Scopes,
    [property:Key(2)] ParameterVersionMetadata[] Versions,[property:Key(3)] ParameterSignalStartupPlan Plan,
    [property:Key(4)] DateTime CreatedAtUtc,[property:Key(5)] string CreatedBy)
{
    internal static ParameterStartupProjection From(ParameterStartupRun run)=>new(run.RunId,run.Scopes,run.Versions.Select(ParameterVersionMetadata.From).ToArray(),run.Plan,run.CreatedAtUtc,run.CreatedBy);
}

internal static class ParameterProjectionMapping
{
    internal static readonly IReadOnlyDictionary<Type,ParameterValueMapping.Table> Roots=new Dictionary<Type,ParameterValueMapping.Table>
    {
        [typeof(ParameterVersionMetadata)]=ParameterValueMapping.Build(typeof(ParameterVersionMetadata),null,"parameter_catalog_version"),
        [typeof(ParameterAuditEntry)]=ParameterValueMapping.Build(typeof(ParameterAuditEntry),null,"parameter_catalog_audit"),
        [typeof(ParameterAssignmentRevision)]=ParameterValueMapping.Build(typeof(ParameterAssignmentRevision),null,"parameter_catalog_assignment"),
        [typeof(ParameterStartupProjection)]=ParameterValueMapping.Build(typeof(ParameterStartupProjection),null,"parameter_catalog_startup"),
        [typeof(ParameterSignalStartupReport)]=ParameterValueMapping.Build(typeof(ParameterSignalStartupReport),null,"parameter_catalog_startup_report")
    };
    internal static IEnumerable<ParameterValueMapping.Table> Tables=>Roots.Values.SelectMany(ParameterValueMapping.Descendants);
    internal static IReadOnlyList<ParameterValueMapping.Row> Write<T>(T value) where T:notnull
    {
        var rows=new List<ParameterValueMapping.Row>();ParameterValueMapping.Walk(Roots[typeof(T)],value,"$",null,0,rows);return rows;
    }
    internal static T Read<T>(IReadOnlyList<ParameterValueMapping.Row> rows)=>
        (T)ParameterValueMapping.Materialize(Roots[typeof(T)],rows.Single(x=>x.Table==Roots[typeof(T)]&&x.Path=="$"),rows);
}

/// <summary>Owns and releases the PostgreSQL parameter writer lock transaction and connection.</summary>
internal sealed class ParameterWriteLease(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
{
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await (transaction.DisposeAsync())
            .ConfigureAwait(false);
        }
        finally
        {
            await (connection.DisposeAsync())
            .ConfigureAwait(false);
        }
    }
}

/// <summary>Executes bound statements on a caller-owned configuration connection and transaction.</summary>
internal sealed class ConfigurationDbSession(NpgsqlConnection connection, NpgsqlTransaction transaction) : IEnlistedPostgresTransaction
{
    readonly PostgresQueryTimeoutScope timeouts = new(connection, transaction);

    /// <inheritdoc/>
    public async Task<int> ExecuteAsync(string sql, object?[] parameters, CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout, token)
            .ConfigureAwait(false);
        await using var command = transaction.CreateConfigurationSessionCommand(connection, sql, parameters);
        return await command.ExecuteNonQueryAsync(token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<object?> ScalarAsync(string sql, object?[] parameters, CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout, token)
            .ConfigureAwait(false);
        await using var command = transaction.CreateConfigurationSessionCommand(connection, sql, parameters);
        var value = await command.ExecuteScalarAsync(token)
            .ConfigureAwait(false);
        return value is DBNull ? null : value;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object?[] parameters,
        Func<System.Data.Common.DbDataReader, T> map, CancellationToken token, PostgresQueryTimeout? timeout = null)
    {
        await timeouts.ApplyAsync(timeout,token)
            .ConfigureAwait(false);
        await using var command = transaction.CreateConfigurationSessionCommand(connection, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        var values = new List<T>();
        while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
            values.Add(map(reader));
        return values;
    }
}
