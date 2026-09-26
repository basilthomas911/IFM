using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using static TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog.StrategyCatalogValidation;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>
/// Provides PostgreSQL persistence for application configuration data.
/// </summary>
/// <param name="connectionSettings">The named database connection settings.</param>
/// <param name="dbFactory">The database-context factory.</param>
/// <param name="logger">The database-provider logger.</param>
/// <param name="catalogCapabilities">The optional strategy-catalog capability registry.</param>
/// <param name="catalogReferences">The optional strategy-catalog reference validator.</param>
public sealed class ConfigurationDbContext(
    IDbConnectionSettings connectionSettings,
    IDbContextFactory dbFactory,
    ILogger<DbProvider> logger,
    TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.IStrategyCatalogCapabilities? catalogCapabilities = null,
    TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.IStrategyCatalogReferences? catalogReferences = null)
    : ObjectDataRepository<ConfigurationDbContext>(connectionSettings[ConfigurationDbConnection], logger),
      IConfigurationDbContext
{
    /// <summary>Gets the configuration connection setting name.</summary>
    public const string ConfigurationDbConnection = "ConfigurationDbConnection";

    /// <inheritdoc />
    public override ConfigurationDbContext Database => this;

    /// <inheritdoc />
    public IConfigurationDbReadContext DbReader => this;

    /// <inheritdoc />
    public IConfigurationDbWriteContext DbWriter => this;

    /// <inheritdoc />
    public async Task InsertRegimeDiscoveryDraftAsync(
        RegimeDiscoveryParameterSet parameterSet,
        string description,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var payload = RegimeDiscoveryParameterPayload.Serialize(parameterSet);
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertDraft)}",
                ConfigurationDbSql.InsertDraft)
            .SetParameters(new InsertConfigurationDraft(
                parameterSet.ParameterSetId,
                parameterSet.Version,
                checked((short)parameterSet.SchemaVersion),
                (short)ConfigurationParameterSetStatus.Draft,
                payload,
                RegimeDiscoveryParameterPayload.ComputeSha256(payload),
                description ?? string.Empty,
                DateTime.UtcNow,
                createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InsertMarketConditionDraftAsync(
        MarketConditionParameterSet parameterSet,
        string description,
        string createdBy,
        CancellationToken cancellationToken = default)
    {
        var payload = MarketConditionParameterPayload.Serialize(parameterSet);
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionDraft)}",
                ConfigurationDbSql.InsertMarketConditionDraft)
            .SetParameters(new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version,
                checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft,
                payload, MarketConditionParameterPayload.ComputeSha256(payload), description ?? string.Empty,
                DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PublishAsync(
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken = default)
    {
        if (kind == StrategyParameterSetKind.OrderComposition)
        {
            var row = await GetSelectionPipelinePolicyAsync(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.OrderComposition, parameterSetId, version, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Exact construction constraints are missing.");
            TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts.ValidatePipelinePolicy(row);
        }
        if (kind == StrategyParameterSetKind.TradeSelection)
            _ = await GetTradeSelectionVersionAsync(parameterSetId, version, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Exact TradeSelection policy is missing.");
        if (kind == StrategyParameterSetKind.IntrinsicTimeStrategyWorkflow)
        {
            var row = await GetSelectionPipelinePolicyAsync(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow, parameterSetId, version, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing workflow activation.");
            var activation = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection.TradeSelectionActivation.Read(row.PayloadJson);
            if (activation.Hash() != row.PayloadSha256 || activation.ParameterSetId != parameterSetId || activation.Version != version) throw new InvalidOperationException("Workflow activation hash/identity mismatch.");
            var selector = await GetEffectiveTradeSelectionVersionAsync(activation.SelectionPolicyReference.Id, activation.SelectionPolicyReference.Version, activation.SelectionPolicyReference.PayloadSha256, effectiveFromUtc, cancellationToken).ConfigureAwait(false);
            if (selector.ParameterSet.TargetHorizon != activation.TargetHorizon || selector.ParameterSet.InstrumentRoot != activation.InstrumentRoot) throw new InvalidOperationException("Activation selector scope mismatch.");
        }
        var sql = ConfigurationDbSql.PublishFor(kind);
        var affected = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.Publish.{kind}", sql)
            .SetParameters(new PublishConfiguration(
                (short)ConfigurationParameterSetStatus.Published, effectiveFromUtc,
                parameterSetId, version, (short)ConfigurationParameterSetStatus.Draft))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        EnsureSingleLifecycleTransition(affected, kind, parameterSetId, version, "publish", "Draft");
    }

    /// <inheritdoc />
    public async Task RetireAsync(
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        DateTime retiredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var sql = ConfigurationDbSql.RetireFor(kind);
        var affected = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.Retire.{kind}", sql)
            .SetParameters(new RetireConfiguration(
                (short)ConfigurationParameterSetStatus.Retired, retiredAtUtc,
                parameterSetId, version, (short)ConfigurationParameterSetStatus.Published))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        EnsureSingleLifecycleTransition(affected, kind, parameterSetId, version, "retire", "Published");
    }

    /// <inheritdoc />
    public async Task<ResolvedRegimeDiscoveryParameterSet?> GetRegimeDiscoveryAsync(
        Guid parameterSetId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var row = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExact)}", ConfigurationDbSql.GetExact)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(MapToConfigurationParameterSet, cancellationToken).ConfigureAwait(false);
        return row is null ? null : Resolve(row);
    }

    /// <inheritdoc />
    public async Task<ResolvedMarketConditionParameterSet?> GetMarketConditionAsync(
        Guid parameterSetId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var row = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExactMarketCondition)}",
                ConfigurationDbSql.GetExactMarketCondition)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(MapToMarketConditionParameterSet, cancellationToken).ConfigureAwait(false);
        return row is null ? null : ResolveMarketCondition(row);
    }

    /// <inheritdoc />
    public async Task<ResolvedRegimeDiscoveryParameterSet?> GetEffectiveRegimeDiscoveryAsync(
        DateTime effectiveAtUtc,
        TimeFrameType targetHorizon,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffective)}",
                ConfigurationDbSql.ResolveEffective)
            .SetParameters(new ResolveConfiguration(
                (short)ConfigurationParameterSetStatus.Published, effectiveAtUtc, (short)targetHorizon))
            .ExecuteQueryAsync(MapToConfigurationParameterSet, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1 && rows.ElementAt(0).EffectiveFromUtc == rows.ElementAt(1).EffectiveFromUtc)
            throw new InvalidOperationException("Effective Regime Discovery parameter selection is ambiguous.");
        return rows.Count == 0 ? null : Resolve(rows.First());
    }

    /// <inheritdoc />
    public async Task<ResolvedMarketConditionParameterSet?> GetEffectiveMarketConditionAsync(
        DateTime effectiveAtUtc,
        int fundId,
        string instrumentRoot,
        TimeFrameType targetHorizon,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffectiveMarketCondition)}",
                ConfigurationDbSql.ResolveEffectiveMarketCondition)
            .SetParameters(new ResolveMarketConditionConfiguration((short)ConfigurationParameterSetStatus.Published,
                effectiveAtUtc, fundId, instrumentRoot, (short)targetHorizon))
            .ExecuteQueryAsync(MapToMarketConditionParameterSet, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1)
            throw new InvalidOperationException("Effective Market Condition parameter selection is ambiguous.");
        return rows.Count == 0 ? null : ResolveMarketCondition(rows.First());
    }

    static ConfigurationParameterSet MapToConfigurationParameterSet(IObjectDataRecord row) => new(
        StrategyParameterSetKind.RegimeDiscovery,
        row.GetGuid(0), row.GetInt(1), checked((short)row.GetInt(2)),
        (ConfigurationParameterSetStatus)row.GetInt(3),
        row.IsNull(4) ? null : row.GetDateTime(4),
        row.IsNull(5) ? null : row.GetDateTime(5),
        row.GetString(6), row.GetString(7), row.GetString(8), row.GetDateTime(9), row.GetString(10));

    static ConfigurationParameterSet MapToMarketConditionParameterSet(IObjectDataRecord row) => MapToConfigurationParameterSet(row) with
    { Kind = StrategyParameterSetKind.MarketCondition };

    static ResolvedRegimeDiscoveryParameterSet Resolve(ConfigurationParameterSet row)
    {
        var typed = JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(row.PayloadJson)
            ?? throw new InvalidOperationException("Stored Regime Discovery configuration cannot be deserialized.");
        var canonicalPayload = RegimeDiscoveryParameterPayload.Serialize(typed);
        if (!string.Equals(row.PayloadSha256, RegimeDiscoveryParameterPayload.ComputeSha256(canonicalPayload),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored Regime Discovery configuration hash is invalid.");
        var errors = new RegimeDiscoveryParameterSetValidationRules().Execute(typed);
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.ErrorMessage)));
        return new(typed, canonicalPayload, row.PayloadSha256,
            row.EffectiveFromUtc ?? DateTime.MinValue);
    }

    static ResolvedMarketConditionParameterSet ResolveMarketCondition(ConfigurationParameterSet row)
    {
        var typed = JsonSerializer.Deserialize<MarketConditionParameterSet>(row.PayloadJson)
            ?? throw new InvalidOperationException("Stored Market Condition configuration cannot be deserialized.");
        var canonicalPayload = MarketConditionParameterPayload.Serialize(typed);
        var canonicalHash = MarketConditionParameterPayload.ComputeSha256(canonicalPayload);
        if (!string.Equals(row.PayloadSha256, canonicalHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stored Market Condition configuration hash is invalid.");
        if (row.ParameterSetId != typed.ParameterSetId || row.Version != typed.Version ||
            row.SchemaVersion != typed.SchemaVersion)
            throw new InvalidOperationException("Stored Market Condition configuration identity or schema metadata is invalid.");
        var errors = new MarketConditionParameterSetValidationRules().Execute(typed);
        if (errors.Length != 0)
            throw new InvalidOperationException(string.Join("; ", errors.Select(value => value.ErrorMessage)));
        return new(typed, canonicalPayload, canonicalHash,
            row.EffectiveFromUtc ?? DateTime.MinValue);
    }

    static void EnsureSingleLifecycleTransition(
        IReadOnlyCollection<long> affectedRows,
        StrategyParameterSetKind kind,
        Guid parameterSetId,
        int version,
        string operation,
        string requiredState)
    {
        if (affectedRows.Count == 1 && affectedRows.Single() == 1)
            return;
        throw new InvalidOperationException(
            $"Cannot {operation} {kind} parameter set {parameterSetId:D} version {version}; " +
            $"exactly one {requiredState} version must exist.");
    }

    /// <inheritdoc />
    public async Task<LookupDefinitionReadModel[]> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetLookupDefinitions)}",
                ConfigurationDbSql.GetLookupDefinitions)
            .SetParameters(new GetLookupDefinitions(groupName))
            .ExecuteQueryAsync(MapToLookupDefinition, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1024) throw new InvalidOperationException("Lookup group exceeds the supported response size.");
        return rows.ToArray();
    }

    static LookupDefinitionReadModel MapToLookupDefinition(IObjectDataRecord row) => new(
        row.GetInt(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4),
        row.GetInt(5), row.GetBool(6), row.GetDateTime(7), row.GetDateTime(8));

    /// <inheritdoc />
    public async Task InsertMarketConditionAssessmentDraftAsync(MarketConditionAssessmentParameterSet parameters,
        string description, string createdBy, CancellationToken cancellationToken = default)
    {
        var normalized = parameters with { Sources = parameters.Sources };
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionAssessmentDraft)}",
                ConfigurationDbSql.InsertMarketConditionAssessmentDraft)
            .SetParameters(new InsertMarketConditionAssessmentDraft(normalized.ParameterSetId, normalized.Version, normalized.SchemaVersion,
            normalized.MarketProfileId, normalized.InstrumentRoot, (short)normalized.TargetHorizon,
            MarketConditionAssessmentHash.Serialize(normalized), MarketConditionAssessmentHash.Parameters(normalized),
            description ?? string.Empty, DateTime.UtcNow, createdBy)).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResolvedMarketConditionAssessmentParameterSet?> GetMarketConditionAssessmentAsync(Guid parameterSetId,
        int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetMarketConditionAssessment)}",
                ConfigurationDbSql.GetMarketConditionAssessment)
            .SetParameters(new GetConfiguration(parameterSetId, version)).ExecuteSingleAsync(MapToMarketConditionAssessment, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ResolvedMarketConditionAssessmentParameterSet?> GetEffectiveMarketConditionAssessmentAsync(DateTime effectiveAtUtc,
        string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetEffectiveMarketConditionAssessment)}",
                ConfigurationDbSql.GetEffectiveMarketConditionAssessment)
            .SetParameters(new GetEffectiveMarketConditionAssessment(marketProfileId, instrumentRoot, (short)targetHorizon, effectiveAtUtc))
            .ExecuteQueryAsync(MapToMarketConditionAssessment, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1) throw new InvalidOperationException("Effective assessment profile selection is ambiguous.");
        return rows.FirstOrDefault();
    }

    static ResolvedMarketConditionAssessmentParameterSet MapToMarketConditionAssessment(IObjectDataRecord row)
    {
        var p = JsonSerializer.Deserialize<MarketConditionAssessmentParameterSet>(row.GetString(8))
            ?? throw new InvalidOperationException("Invalid stored assessment configuration.");
        p.Validate();
        if (p.ParameterSetId != row.GetGuid(0) || p.Version != row.GetInt(1) || p.SchemaVersion != row.GetInt(2) ||
            p.MarketProfileId != row.GetString(3) || p.InstrumentRoot != row.GetString(4) || (short)p.TargetHorizon != row.GetInt(5) ||
            MarketConditionAssessmentHash.Parameters(p) != row.GetString(9))
            throw new InvalidOperationException("Assessment configuration metadata or payload hash mismatch.");
        return new(p, row.GetString(9), row.IsNull(7) ? null : DateTime.SpecifyKind(row.GetDateTime(7), DateTimeKind.Utc),
            (ConfigurationParameterSetStatus)row.GetInt(6));
    }

    /// <inheritdoc />
    public async Task InsertVolatilitySeriesDefinitionAsync(VolatilitySeriesDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(definition);
        var hash = Sha256(json);
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertVolatilitySeriesDefinition)}",
                ConfigurationDbSql.InsertVolatilitySeriesDefinition)
            .SetParameters(new InsertVolatilitySeriesDefinition(definition.Identity.SeriesId,
            definition.Identity.MethodologyVersion, definition.DataIdentity.Environment,
            definition.Governance.EffectiveFromUtc.UtcDateTime,
            definition.Governance.EffectiveUntilUtc?.UtcDateTime, json, hash,
            definition.Governance.ApprovedConfigurationVersion, definition.Governance.Owner,
            definition.Governance.ApprovalEvidenceId)).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

        var stored = await GetVolatilitySeriesDefinitionAsync(definition.Identity, cancellationToken).ConfigureAwait(false);
        if (stored is null || stored.PayloadSha256 != hash)
            throw new InvalidOperationException("Volatility series identity conflicts with different immutable content.");
    }

    /// <inheritdoc />
    public Task<ResolvedVolatilitySeriesDefinition?> GetVolatilitySeriesDefinitionAsync(
        VolatilitySeriesIdentity identity, CancellationToken cancellationToken = default)
    {
        return dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetVolatilitySeriesDefinition)}",
                ConfigurationDbSql.GetVolatilitySeriesDefinition)
            .SetParameters(new GetVolatilitySeriesDefinition(identity.SeriesId, identity.MethodologyVersion))
            .ExecuteSingleAsync(MapToVolatilitySeriesDefinition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ResolvedVolatilitySeriesDefinition?> GetEffectiveVolatilitySeriesDefinitionAsync(
        string environment, string seriesId, DateTimeOffset effectiveAtUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetEffectiveVolatilitySeriesDefinition)}",
                ConfigurationDbSql.GetEffectiveVolatilitySeriesDefinition)
            .SetParameters(new GetEffectiveVolatilitySeriesDefinition(environment, seriesId, effectiveAtUtc.UtcDateTime))
            .ExecuteQueryAsync(MapToVolatilitySeriesDefinition, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 1) throw new InvalidOperationException("Effective volatility series selection is ambiguous.");
        return rows.FirstOrDefault();
    }

    static ResolvedVolatilitySeriesDefinition MapToVolatilitySeriesDefinition(IObjectDataRecord row)
    {
        var definition = JsonSerializer.Deserialize<VolatilitySeriesDefinition>(row.GetString(5))
            ?? throw new InvalidDataException("Stored volatility series payload is missing.");
        ValidateStoredVolatilityDefinition(definition);
        var hash = Sha256(JsonSerializer.Serialize(definition));
        if (definition.Identity.SeriesId != row.GetString(0) ||
            definition.Identity.MethodologyVersion != row.GetString(1) ||
            definition.DataIdentity.Environment != row.GetString(2) || hash != row.GetString(6))
            throw new InvalidDataException("Stored volatility series identity or digest is invalid.");
        return new(definition, hash);
    }

    static void ValidateStoredVolatilityDefinition(VolatilitySeriesDefinition definition)
    {
        if (definition is null) throw new InvalidDataException("Stored volatility series definition is missing.");
        if (definition.Identity is null || string.IsNullOrWhiteSpace(definition.Identity.SeriesId) ||
            string.IsNullOrWhiteSpace(definition.Identity.MethodologyVersion))
            throw new InvalidDataException("Stored volatility series identity is invalid.");
        if (definition.SchemaVersion != VolatilitySeriesDefinition.CurrentSchemaVersion ||
            string.IsNullOrWhiteSpace(definition.DataIdentity.Environment) ||
            string.IsNullOrWhiteSpace(definition.Governance.ApprovedConfigurationVersion) ||
            string.IsNullOrWhiteSpace(definition.Governance.Owner) ||
            string.IsNullOrWhiteSpace(definition.Governance.ApprovalEvidenceId) ||
            definition.Governance.EffectiveFromUtc.Offset != TimeSpan.Zero ||
            definition.Governance.EffectiveUntilUtc is { } until && until.Offset != TimeSpan.Zero ||
            definition.Governance.EffectiveUntilUtc <= definition.Governance.EffectiveFromUtc ||
            definition.Construction.IntradayCoalescingInterval <= TimeSpan.Zero ||
            definition.Construction.MaximumIntradayCheckpointsPerValueDate <= 0)
            throw new InvalidDataException("Stored volatility series definition is invalid.");
    }

    static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>Creates a validated draft in the existing Risk policy table. Publication and deployment binding remain explicit.</summary>
    public async Task InsertRiskManagementDraftAsync(RiskParameterSet policy, string description, string createdBy, CancellationToken token = default)
    {
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertRiskManagementDraft)}",
                ConfigurationDbSql.InsertRiskManagementDraft)
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0,
                policy.Serialize(), policy.Hash(), description, DateTime.UtcNow, createdBy)).ExecuteCommandAsync(token).ConfigureAwait(false);
    }

    // Each operation owns its connection/transaction. Concurrent calls on a resolved context cannot
    // share the mutable ambient repository transaction. Connections still use the framework provider.
    /// <inheritdoc />
    public async Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition,
        int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken = default)
    {
        var d = Canonicalize(definition);
        var hash = CanonicalContentHash(d);
        var json = JsonSerializer.Serialize(d, JsonOptions);
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CatalogWriteLock(connection, transaction, cancellationToken).ConfigureAwait(false);
        var latest = await Scalar(connection, transaction, ConfigurationDbSql.GetLatestStrategyCatalogVersion,
            cancellationToken, new CatalogIdentity((short)d.Key.Kind, d.Key.Id)).ConfigureAwait(false);
        if (Convert.ToInt32(latest) != expectedPreviousVersion)
            throw new InvalidOperationException("Catalog authoring conflict: expected previous version does not match.");
        var code = await Scalar(connection, transaction, ConfigurationDbSql.GetStrategyCatalogIdentityCode,
            cancellationToken, new CatalogIdentity((short)d.Key.Kind, d.Key.Id)).ConfigureAwait(false);
        if (code is string existing && existing != d.Code) throw new InvalidOperationException("Catalog identity code is immutable.");

        // Exact dependencies must exist even for drafts. They need not be published until publication.
        foreach (var key in Dependencies(d))
            if (await ReadCatalog(connection, transaction, key, true, cancellationToken).ConfigureAwait(false) is null)
                throw new InvalidOperationException($"Missing catalog dependency: {key}.");
        if (d.Key.Kind == StrategyCatalogKind.ParameterSet)
        {
            var schema = await ReadCatalog(connection, transaction, d.Parent!, true, cancellationToken).ConfigureAwait(false);
            ValidateParameters(ReadShape(schema!.Definition.Settings), d.Settings);
        }
        var now = CatalogNow();
        await Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogIdentity,
            cancellationToken, new InsertCatalogIdentity((short)d.Key.Kind, d.Key.Id, d.Code, now, createdBy)).ConfigureAwait(false);
        await Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogVersion,
            cancellationToken, new InsertCatalogVersion(json, hash, now, createdBy)).ConfigureAwait(false);
        foreach (var child in StrategyCatalogSchemaSql.Children)
            await Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogChildren(child), cancellationToken,
                new TextValue(json)).ConfigureAwait(false);
        await Execute(connection, transaction, ConfigurationDbSql.SealStrategyCatalogVersion,
            cancellationToken, new CatalogVersion((short)d.Key.Kind, d.Key.Id, d.Key.Version)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return hash;
    }

    /// <inheritdoc />
    public async Task<StoredStrategyCatalogDefinition?> GetStrategyCatalogAsync(CatalogKey key, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        return await ReadCatalog(connection, null, key, false, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StrategyCatalogSummary>> GetStrategyCatalogsAsync(StrategyCatalogKind kind,
        int limit = 50, string? afterCode = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var command = Command(connection, null, ConfigurationDbSql.GetStrategyCatalogs,
            new ListCatalogs((short)kind, afterCode ?? "", limit));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<StrategyCatalogSummary>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            result.Add(new(new(kind, reader.GetGuid(0), reader.GetInt32(1)), reader.GetString(2), reader.GetString(3),
                (CatalogLifecycleStatus)reader.GetInt16(4), reader.GetString(5)));
        return result;
    }

    /// <inheritdoc />
    public async Task PublishStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime effectiveFromUtc,
        string publishedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CatalogWriteLock(connection, transaction, cancellationToken).ConfigureAwait(false);
        var graph = await LoadCatalogGraph(connection, transaction, key, effectiveFromUtc, true, cancellationToken).ConfigureAwait(false);
        var root = graph[key];
        if (root.Status != CatalogLifecycleStatus.Draft || root.ContentHash != expectedContentHash)
            throw new InvalidOperationException("Publish requires the exact Draft content hash.");
        await ValidateCatalogGraph(connection, transaction, graph, effectiveFromUtc, cancellationToken).ConfigureAwait(false);
        var count = await Execute(connection, transaction, ConfigurationDbSql.PublishStrategyCatalog,
            cancellationToken, new PublishCatalog((short)key.Kind, key.Id, key.Version, effectiveFromUtc, publishedBy,
                expectedContentHash)).ConfigureAwait(false);
        if (count != 1) throw new InvalidOperationException("Catalog publication conflict.");
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RetireStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime retiredAtUtc,
        string retiredBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await CatalogWriteLock(connection, transaction, cancellationToken).ConfigureAwait(false);
        var current = await ReadCatalog(connection, transaction, key, true, cancellationToken).ConfigureAwait(false);
        if (current is null || current.Status != CatalogLifecycleStatus.Published || current.ContentHash != expectedContentHash || retiredAtUtc < current.EffectiveFromUtc)
            throw new InvalidOperationException("Retire requires the exact Published version and a timestamp at or after publication.");
        await Execute(connection, transaction, ConfigurationDbSql.RetireStrategyCatalog,
            cancellationToken, new RetireCatalog((short)key.Kind, key.Id, key.Version, retiredAtUtc, retiredBy)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<StrategyCatalogSnapshot> GetPublishedStrategyDeploymentAsync(CatalogKey deployment, DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenCatalogAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
        var graph = await LoadCatalogGraph(connection, transaction, deployment, asOfUtc, false, cancellationToken).ConfigureAwait(false);
        await ValidateCatalogGraph(connection, transaction, graph, asOfUtc, cancellationToken).ConfigureAwait(false);
        var definitions = graph.Values.OrderBy(x => x.Definition.Key.Kind).ThenBy(x => x.Definition.Key.Id).ThenBy(x => x.Definition.Key.Version).ToArray();
        var hash = Sha(CanonicalJson(JsonSerializer.SerializeToElement(new
        {
            SchemaVersion = 1,
            Deployment = deployment,
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
                await using var command = Command(connection, transaction,
                    ConfigurationDbSql.GetPipelinePolicyForUpdate(parameter.Kind), new GuidVersion(parameter.Id, parameter.Version));
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.GetString(0) != parameter.Hash || reader.GetInt16(1) != 1 || reader.IsDBNull(2) || reader.GetDateTime(2) > at)
                    throw new InvalidOperationException("Pipeline parameter reference is missing, mismatched or not effective and Published.");
                // These kinds have qualified owning schemas. Other existing pipeline kinds retain their own publication path.
                if (parameter.Kind is CatalogPipelineParameterKind.TradeSelection or CatalogPipelineParameterKind.OrderComposition or CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow or CatalogPipelineParameterKind.MarketConditionAssessment or CatalogPipelineParameterKind.RegimeDiscovery or CatalogPipelineParameterKind.RiskManagement)
                    TradeSelectionContracts.ValidatePipelinePolicy(new SelectionPipelinePolicySnapshot
                    {
                        Kind = parameter.Kind,
                        Id = parameter.Id,
                        Version = parameter.Version,
                        PayloadSha256 = reader.GetString(0),
                        PayloadJson = reader.GetString(3),
                        SchemaVersion = reader.GetInt16(4)
                    });
                if (parameter.Kind == CatalogPipelineParameterKind.RiskManagement && d.Key.Kind == StrategyCatalogKind.Deployment)
                {
                    var risk = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskParameterSet.Read(reader.GetString(3));
                    if (risk.TargetHorizon != d.Horizon || d.Products.Any(product => product.Symbol != risk.Root || product.Currency != risk.Currency))
                        throw new InvalidOperationException("Risk policy horizon/product scope does not match the deployment.");
                }

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
        await using var command = Command(connection, transaction, ConfigurationDbSql.GetStrategyCatalog(lockRow),
            new CatalogVersion((short)key.Kind, key.Id, key.Version));
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
        ConfigurationDbSql.CatalogWriteLock, ct, new LongValue(StrategyCatalogSchemaSql.WriterLock));
    static async Task<int> Execute(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, CancellationToken ct, IBindValue? parameters = null)
    {
        await using var command = Command(c, tx, sql, parameters);
        return await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    static async Task<object?> Scalar(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, CancellationToken ct, IBindValue? parameters = null)
    {
        await using var command = Command(c, tx, sql, parameters);
        return await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
    }
    static NpgsqlCommand Command(NpgsqlConnection c, NpgsqlTransaction? tx, string sql, IBindValue? parameters = null)
    {
        var command = new NpgsqlCommand(sql, c, tx);
        if (parameters?.Bind() is NpgsqlParameter[] values)
            command.Parameters.AddRange(values);
        return command;
    }
    internal static DateTime CatalogNow() => new(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);

    /// <inheritdoc />
    public async Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertOrderCompositionDraft)}",
                ConfigurationDbSql.InsertOrderCompositionDraft)
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0, policy.Serialize(), policy.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task InsertTradeSelectionActivationDraftAsync(TradeSelectionActivation activation, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertStrategyWorkflowDraft)}",
                ConfigurationDbSql.InsertStrategyWorkflowDraft)
            .SetParameters(new InsertConfigurationDraft(activation.ParameterSetId, activation.Version, activation.SchemaVersion, 0, activation.Serialize(), activation.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<TradeSelectionActivation> GetEffectiveTradeSelectionActivationAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default)
    {
        var row = await GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow, id, version, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Exact workflow activation is missing.");
        var activation = TradeSelectionActivation.Read(row.PayloadJson);
        if (activation.ParameterSetId != id || activation.Version != version || activation.Hash() != hash || row.PayloadSha256 != hash || row.Status != CatalogLifecycleStatus.Published || row.EffectiveFromUtc is null || row.EffectiveFromUtc > at || row.RetiredAtUtc <= at)
            throw new InvalidOperationException("Exact published workflow activation is invalid or unavailable.");
        var selected = await GetEffectiveTradeSelectionVersionAsync(activation.SelectionPolicyReference.Id, activation.SelectionPolicyReference.Version, activation.SelectionPolicyReference.PayloadSha256, at, cancellationToken).ConfigureAwait(false);
        if (selected.ParameterSet.TargetHorizon != activation.TargetHorizon || selected.ParameterSet.InstrumentRoot != activation.InstrumentRoot) throw new InvalidOperationException("Activation selector scope mismatch.");
        return activation;
    }

    /// <inheritdoc />
    public async Task InsertTradeSelectionDraftAsync(TradeSelectionParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertTradeSelectionDraft)}",
                ConfigurationDbSql.InsertTradeSelectionDraft)
            .SetParameters(new InsertConfigurationDraft(parameters.ParameterSetId, parameters.Version, parameters.SchemaVersion, 0,
                TradeSelectionPolicy.Serialize(parameters), TradeSelectionPolicy.Hash(parameters), description ?? "", DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<ResolvedTradeSelectionParameterSet?> GetTradeSelectionVersionAsync(Guid id, int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetTradeSelectionVersion)}",
                ConfigurationDbSql.GetTradeSelectionVersion)
            .SetParameters(new GetConfiguration(id, version))
            .ExecuteSingleAsync(MapToTradeSelectionVersion, cancellationToken).ConfigureAwait(false);
    }
    /// <inheritdoc />
    public async Task<ResolvedTradeSelectionParameterSet> GetEffectiveTradeSelectionVersionAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default)
    {
        var row = await GetTradeSelectionVersionAsync(id, version, cancellationToken).ConfigureAwait(false);
        if (row is null || row.PayloadSha256 != hash || row.Status != ConfigurationParameterSetStatus.Published || row.EffectiveFromUtc > at || row.EffectiveFromUtc is null || row.RetiredAtUtc <= at)
            throw new InvalidOperationException("TS.CONFIG.MISSING: Exact effective published selector policy is required.");
        return row;
    }
    /// <inheritdoc />
    public async Task<SelectionPipelinePolicySnapshot?> GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetSelectionPipelinePolicyFor)}.{kind}",
                ConfigurationDbSql.GetSelectionPipelinePolicyFor(kind))
            .SetParameters(new GetConfiguration(id, version))
            .ExecuteSingleAsync(MapToSelectionPipelinePolicy, cancellationToken).ConfigureAwait(false);
    }

    static ResolvedTradeSelectionParameterSet MapToTradeSelectionVersion(IObjectDataRecord row)
    {
        var parameterSet = TradeSelectionPolicy.Read(row.GetString(6));
        if (parameterSet.ParameterSetId != row.GetGuid(0) || parameterSet.Version != row.GetInt(1) ||
            parameterSet.SchemaVersion != row.GetInt(2) || TradeSelectionPolicy.Hash(parameterSet) != row.GetString(7))
            throw new InvalidOperationException("TS.CONTRACT.HASH: Stored policy metadata/hash mismatch.");

        return new(parameterSet, row.GetString(7), (ConfigurationParameterSetStatus)row.GetInt(3),
            row.IsNull(4) ? null : DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc),
            row.IsNull(5) ? null : DateTime.SpecifyKind(row.GetDateTime(5), DateTimeKind.Utc));
    }

    static SelectionPipelinePolicySnapshot MapToSelectionPipelinePolicy(IObjectDataRecord row) => new()
    {
        Kind = (CatalogPipelineParameterKind)row.GetInt(0),
        Id = row.GetGuid(1),
        Version = row.GetInt(2),
        SchemaVersion = checked((short)row.GetInt(3)),
        Status = (CatalogLifecycleStatus)row.GetInt(4),
        EffectiveFromUtc = row.IsNull(5) ? null : DateTime.SpecifyKind(row.GetDateTime(5), DateTimeKind.Utc),
        RetiredAtUtc = row.IsNull(6) ? null : DateTime.SpecifyKind(row.GetDateTime(6), DateTimeKind.Utc),
        PayloadJson = row.GetString(7),
        PayloadSha256 = row.GetString(8)
    };

    /// <inheritdoc />
    public async Task ProjectParameterAssignmentAsync(ParameterAssignmentChangedEvent fact, CancellationToken token = default)
    {
        var value = JsonSerializer.Deserialize<ParameterAssignmentRevision>(fact.AssignmentJson) ?? throw new InvalidDataException("Invalid assignment.");
        if (value.AssignmentId != fact.EntityId.AssignmentId || value.Revision != fact.Revision) throw new InvalidDataException("Assignment identity mismatch.");
        await using var connection = await OpenCatalogAsync(token); await using var tx = await connection.BeginTransactionAsync(token);
        await Scalar(connection, tx, ConfigurationDbSql.AcquireNamedAdvisoryLock, token, new TextValue("parameter-assignment:" + fact.EntityId.Format()));
        var receipt = await Scalar(connection, tx, ConfigurationDbSql.GetAssignmentOperationReceipt, token, new GuidValue(fact.CommandId));
        if (receipt is string prior) { if (prior != fact.RequestHash) throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH"); return; }
        var revision = Convert.ToInt64(await Scalar(connection, tx, ConfigurationDbSql.GetAssignmentRevision, token, new GuidValue(value.AssignmentId)) ?? 0L);
        if (revision + 1 != value.Revision) throw new InvalidOperationException("PARAM.PROJECTION_ORDER");
        await Execute(connection, tx, ConfigurationDbSql.UpsertParameterAssignment, token, new UpsertAssignment(value.AssignmentId, value.Revision, fact.AssignmentJson));
        await Execute(connection, tx, ConfigurationDbSql.InsertParameterAssignmentRevision, token, new InsertAssignmentRevision(value.AssignmentId, value.Revision, fact.CommandId, fact.RequestHash, fact.AssignmentJson));
        if (!string.IsNullOrWhiteSpace(fact.AuditJson))
        {
            var audit = JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson) ?? throw new InvalidDataException("Invalid audit entry.");
            if (audit.OperationId != fact.CommandId || audit.Revision != fact.Revision) throw new InvalidDataException("Audit identity mismatch.");
            await Execute(connection, tx, ConfigurationDbSql.InsertParameterSetAudit, token, new InsertParameterAudit(audit.OperationId, audit.EntityId, audit.Revision, fact.AuditJson));
        }
        await tx.CommitAsync(token);
    }

    /// <inheritdoc />
    public async Task<ParameterLegacyVersion[]> GetLegacyParameterVersionsAsync(Guid? setId = null, int version = 0, int offset = 0, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetLegacyParameterVersions)}",
                ConfigurationDbSql.GetLegacyParameterVersions)
            .SetParameters(new GetLegacyParameterVersions(setId, version, offset))
            .ExecuteQueryAsync(MapToLegacyParameterVersion, cancellationToken).ConfigureAwait(false);
        return rows.ToArray();
    }

    static ParameterLegacyVersion MapToLegacyParameterVersion(IObjectDataRecord row) => new(
        new("regime_discovery_parameter_set", row.GetGuid(0), row.GetInt(1), row.GetString(4), "regime-typed-json-v1"),
        checked((short)row.GetInt(2)), row.GetString(3), row.GetString(5), checked((short)row.GetInt(6)));

    /// <inheritdoc />
    public async Task<ParameterComponentSummary[]> GetParameterComponentsAsync(CancellationToken token = default)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetParameterComponents)}",
                ConfigurationDbSql.GetParameterComponents)
            .ExecuteQueryAsync(MapToParameterComponent, token).ConfigureAwait(false);
        return rows.ToArray();
    }

    static ParameterComponentSummary MapToParameterComponent(IObjectDataRecord row) => new(
        row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3),
        JsonSerializer.Deserialize<int[]>(row.GetString(4)) ?? [],
        ParameterSchemaRegistry.Default.Definitions.Any(definition => definition.ComponentCode == row.GetString(2)));
    /// <inheritdoc />
    public async Task<ParameterSchemaDefinition?> GetParameterSchemaAsync(string componentCode, int version, CancellationToken token = default)
    {
        var stored = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetParameterSchema)}",
                ConfigurationDbSql.GetParameterSchema)
            .SetParameters(new GetParameterSchema(componentCode, version))
            .ExecuteSingleAsync(MapToParameterSchema, token).ConfigureAwait(false);
        if (stored is null) return null;
        var registered = ParameterSchemaRegistry.Default.Definitions.SingleOrDefault(x => x.ComponentCode == componentCode && x.Version == version);
        if (registered is null) return stored;
        if (registered.Codec != stored.Codec || registered.SchemaSha256 != stored.SchemaSha256 || !System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(registered.JsonSchema), System.Text.Json.Nodes.JsonNode.Parse(stored.JsonSchema))) throw new InvalidDataException("PARAM.REGISTRY_SCHEMA_MISMATCH");
        return registered;
    }

    static ParameterSchemaDefinition MapToParameterSchema(IObjectDataRecord row) => new(
        row.GetString(0), row.GetInt(1), row.GetString(2), row.GetString(4), row.GetString(3));

    /// <inheritdoc />
    public async Task ProjectParameterSetAsync(IParameterSetFact fact, CancellationToken token = default)
    {
        var value = JsonSerializer.Deserialize<ParameterSetVersion>(fact.VersionJson) ?? throw new InvalidDataException("Invalid version fact.");
        await using var connection = await OpenCatalogAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await Scalar(connection, transaction, ConfigurationDbSql.AcquireNamedAdvisoryLock, token, new TextValue(fact.EntityId.Format()));
        var receipt = await Scalar(connection, transaction, ConfigurationDbSql.GetParameterOperationReceipt, token, new GuidValue(fact.CommandId));
        if (receipt is string prior) { if (prior != fact.RequestHash) throw new InvalidOperationException("PARAM.OPERATION_IDENTITY_MISMATCH"); return; }
        var revision = Convert.ToInt64(await Scalar(connection, transaction, ConfigurationDbSql.GetParameterSetRevision, token, new GuidValue(fact.EntityId.SetId)) ?? 0L);
        if (revision + 1 != fact.Revision) throw new InvalidOperationException("PARAM.PROJECTION_ORDER");
        await Execute(connection, transaction, ConfigurationDbSql.UpsertParameterSet, token, new UpsertParameterSet(fact.EntityId.SetId, value.Reference.ComponentCode, value.Name, value.Description, fact.Revision));
        var hash = await Scalar(connection, transaction, ConfigurationDbSql.GetParameterSetVersionHash, token, new GuidVersion(fact.EntityId.SetId, value.Reference.Version));
        if (hash is string existing && existing != value.Reference.PayloadSha256) throw new InvalidOperationException("PARAM.VERSION_IMMUTABLE");
        await Execute(connection, transaction, ConfigurationDbSql.UpsertParameterSetVersion, token, new UpsertParameterSetVersion(fact.EntityId.SetId, value.Reference.Version, value.Reference.PayloadSha256, (short)value.Status, fact.VersionJson));
        await Execute(connection, transaction, ConfigurationDbSql.InsertParameterOperation,
         token, new InsertParameterOperation(fact.CommandId, fact.EntityId.SetId, fact.Revision, fact.RequestHash, value.Reference.Version));
        if (!string.IsNullOrWhiteSpace(fact.AuditJson))
        {
            var audit = JsonSerializer.Deserialize<ParameterAuditEntry>(fact.AuditJson) ?? throw new InvalidDataException("Invalid audit entry.");
            if (audit.OperationId != fact.CommandId || audit.Revision != fact.Revision) throw new InvalidDataException("Audit identity mismatch.");
            await Execute(connection, transaction, ConfigurationDbSql.InsertParameterSetAudit, token, new InsertParameterAudit(audit.OperationId, audit.EntityId, audit.Revision, fact.AuditJson));
        }
        if (value.Reference.Version == 1 && value.LegacySource is { } source)
        {
            var legacyReference = new LegacyParameterReference(source.Kind, source.SetId, source.Version, value.Reference.SetId, value.Reference.Version, source.PayloadSha256, source.Codec);
            await Execute(connection, transaction, ConfigurationDbSql.InsertLegacyParameterReference, token, legacyReference);
            var same = await Scalar(connection, transaction, ConfigurationDbSql.IsLegacyParameterReferenceEquivalent, token,
                new VerifyLegacyParameterReference(source.Kind, source.SetId, source.Version, value.Reference.SetId,
                    source.PayloadSha256, source.Codec));
            if (same is not true) throw new InvalidDataException("PARAM.LEGACY_MAPPING_CONFLICT");
        }
        await transaction.CommitAsync(token);
    }
    /// <inheritdoc />
    public async Task<ParameterSetVersion[]> GetParameterSetsAsync(string componentCode, Guid? setId = null, CancellationToken token = default, int limit = 100, string afterName = "", Guid? afterSetId = null, int afterVersion = 0)
    {
        var rows = await dbFactory.ConfigurationDb
            .Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetParameterSets)}",
                ConfigurationDbSql.GetParameterSets)
            .SetParameters(new GetParameterSets(componentCode, setId, afterName, afterSetId, afterVersion, limit))
            .ExecuteQueryAsync(MapToParameterSetVersion, token).ConfigureAwait(false);
        return rows.ToArray();
    }

    static ParameterSetVersion MapToParameterSetVersion(IObjectDataRecord row)
    {
        var value = JsonSerializer.Deserialize<ParameterSetVersion>(row.GetString(0)) ?? throw new InvalidDataException("Invalid parameter projection.");
        return value with { Name = row.GetString(1), Description = row.GetString(2), CatalogRevision = row.GetLong(3) };
    }

    /// <inheritdoc />
    public async Task ProjectParameterStartupAsync(ParameterStartupChangedEvent fact, CancellationToken token = default)
    {
        await using var connection = await OpenCatalogAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        if (!string.IsNullOrEmpty(fact.ReportJson))
        {
            var report = new StartupReport(fact.RunId, fact.Revision, fact.ReportJson);
            await Execute(connection, transaction, ConfigurationDbSql.InsertParameterStartupReport, token, report);
            var same = await Scalar(connection, transaction, ConfigurationDbSql.IsParameterStartupReportEquivalent, token, report);
            if (same is not true) throw new InvalidDataException("PARAM.STARTUP_REPORT_IMMUTABLE");
        }
        else if (!fact.Released)
        {
            var run = new StartupRun(fact.RunId, fact.RunJson);
            await Execute(connection, transaction, ConfigurationDbSql.InsertParameterStartupRun, token, run);
            var same = await Scalar(connection, transaction, ConfigurationDbSql.IsParameterStartupRunEquivalent, token, run);
            if (same is not true) throw new InvalidDataException("PARAM.STARTUP_IMMUTABLE");
        }
        else
        {
            var count = await Execute(connection, transaction, ConfigurationDbSql.ReleaseParameterStartupRun, token, new GuidValue(fact.RunId));
            if (count != 1) throw new InvalidOperationException("PARAM.STARTUP_PROJECTION_PENDING");
        }
        await transaction.CommitAsync(token);
    }

    // Serializes lifecycle/assignment decisions across hosts while the event stream remains authoritative.
    /// <inheritdoc />
    public async Task<IAsyncDisposable> AcquireParameterWriteLeaseAsync(CancellationToken token = default)
    {
        var connection = await OpenCatalogAsync(token); NpgsqlTransaction? transaction = null;
        try
        {
            transaction = await connection.BeginTransactionAsync(token);
            await Execute(connection, transaction, ConfigurationDbSql.SetParameterWriterLockTimeout, token);
            await Execute(connection, transaction, ConfigurationDbSql.AcquireParameterWriterLock, token);
            return new ParameterWriteLease(connection, transaction);
        }
        catch { if (transaction is not null) await transaction.DisposeAsync(); await connection.DisposeAsync(); throw; }
    }
    sealed class ParameterWriteLease(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
    {
        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            try { await transaction.DisposeAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }

    // ConfigurationDb consolidation anchor.
}
