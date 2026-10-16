using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Text.Json;
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
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using static TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical.StrategyCatalogValidation;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
/// <summary>
/// Provides PostgreSQL persistence for application configuration data.
/// </summary>
/// <param name = "connectionSettings">The named database connection settings.</param>
/// <param name = "dbFactory">The database-context factory.</param>
/// <param name = "logger">The database-provider logger.</param>
/// <param name = "catalogCapabilities">The optional strategy-catalog capability registry.</param>
/// <param name = "catalogReferences">The optional strategy-catalog reference validator.</param>
public sealed class ConfigurationDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger, IStrategyCatalogCapabilities? catalogCapabilities = null, IStrategyCatalogReferences? catalogReferences = null) : ObjectDataRepository<ConfigurationDbContext>(connectionSettings[ConfigurationDbConnection], logger), IConfigurationDbContext
{
    internal IStrategyCatalogCapabilities? CatalogCapabilities { get; } = catalogCapabilities;
    internal IStrategyCatalogReferences? CatalogReferences { get; } = catalogReferences;

    /// <summary>Gets the configuration connection setting name.</summary>
    public const string ConfigurationDbConnection = "ConfigurationDbConnection";
    /// <summary>Gets the concrete configuration database context.</summary>
    public override ConfigurationDbContext Database => this;
    /// <summary>Gets the configuration database read capability.</summary>
    public IConfigurationDbReadContext DbReader => this;
    /// <summary>Gets the configuration database write capability.</summary>
    public IConfigurationDbWriteContext DbWriter => this;

    /// <summary>Inserts an immutable Regime Discovery draft.</summary>
    /// <param name = "parameterSet">The validated parameter set.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertRegimeDiscoveryDraftAsync(RegimeDiscoveryParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        var payload = RegimeDiscoveryParameterPayload.Serialize(parameterSet);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertDraft)}", ConfigurationDbSql.InsertDraft)
            .SetParameters(new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version, checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft, payload, RegimeDiscoveryParameterPayload.ComputeSha256(payload), description ?? string.Empty, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Materializes an accepted Regime Discovery draft with its original creation timestamp.</summary>
    /// <param name = "transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name = "parameterSet">The parameter set.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The created by.</param>
    /// <param name = "createdAtUtc">The created at utc.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public async Task InsertRegimeDiscoveryDraftAsync(IEnlistedPostgresTransaction transaction, RegimeDiscoveryParameterSet parameterSet, string description, string createdBy, DateTime createdAtUtc, CancellationToken cancellationToken = default)
    {
        var payload = RegimeDiscoveryParameterPayload.Serialize(parameterSet);
        var parameters = new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version, checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft, payload, RegimeDiscoveryParameterPayload.ComputeSha256(payload), description ?? string.Empty, createdAtUtc, createdBy);
        await transaction.ExecuteAsync(ConfigurationDbSql.InsertDraft, this.BindProjection(parameters), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Materializes the accepted publication of a Regime Discovery draft.</summary>
    /// <param name = "transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name = "parameterSetId">The parameter set id.</param>
    /// <param name = "version">The version.</param>
    /// <param name = "effectiveFromUtc">The effective from utc.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public async Task PublishRegimeDiscoveryAsync(IEnlistedPostgresTransaction transaction, Guid parameterSetId, int version, DateTime effectiveFromUtc, CancellationToken cancellationToken = default)
    {
        var parameters = new PublishConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveFromUtc, parameterSetId, version, (short)ConfigurationParameterSetStatus.Draft);
        var count = await transaction.ExecuteAsync(ConfigurationDbSql.Publish, this.BindProjection(parameters), cancellationToken)
            .ConfigureAwait(false);
        this.EnsureSingleLifecycleTransition([count], StrategyParameterSetKind.RegimeDiscovery, parameterSetId, version, "publish", "Draft");
    }

    /// <summary>Materializes the accepted retirement of a Regime Discovery version.</summary>
    /// <param name = "transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name = "parameterSetId">The parameter set id.</param>
    /// <param name = "version">The version.</param>
    /// <param name = "retiredAtUtc">The retired at utc.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public async Task RetireRegimeDiscoveryAsync(IEnlistedPostgresTransaction transaction, Guid parameterSetId, int version, DateTime retiredAtUtc, CancellationToken cancellationToken = default)
    {
        var parameters = new RetireConfiguration((short)ConfigurationParameterSetStatus.Retired, retiredAtUtc, parameterSetId, version, (short)ConfigurationParameterSetStatus.Published);
        var count = await transaction.ExecuteAsync(ConfigurationDbSql.Retire, this.BindProjection(parameters), cancellationToken)
            .ConfigureAwait(false);
        this.EnsureSingleLifecycleTransition([count], StrategyParameterSetKind.RegimeDiscovery, parameterSetId, version, "retire", "Published");
    }

    /// <summary>Inserts an immutable Market Condition draft.</summary>
    /// <param name = "parameterSet">The validated parameter set.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertMarketConditionDraftAsync(MarketConditionParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        var payload = MarketConditionParameterPayload.Serialize(parameterSet);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionDraft)}", ConfigurationDbSql.InsertMarketConditionDraft)
            .SetParameters(new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version, checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft, payload, MarketConditionParameterPayload.ComputeSha256(payload), description ?? string.Empty, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Publishes an immutable parameter-set version.</summary>
    /// <param name = "kind">The parameter-set kind.</param>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version.</param>
    /// <param name = "effectiveFromUtc">The UTC effective timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    public async Task PublishAsync(StrategyParameterSetKind kind, Guid parameterSetId, int version, DateTime effectiveFromUtc, CancellationToken cancellationToken = default)
    {
        if (kind == StrategyParameterSetKind.OrderComposition)
        {
            var row = await GetSelectionPipelinePolicyAsync(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.OrderComposition, parameterSetId, version, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Exact construction constraints are missing.");
            TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts.ValidatePipelinePolicy(row);
        }

        if (kind == StrategyParameterSetKind.TradeSelection)
            _ = await GetTradeSelectionVersionAsync(parameterSetId, version, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Exact TradeSelection policy is missing.");
        if (kind == StrategyParameterSetKind.IntrinsicTimeStrategyWorkflow)
        {
            var row = await GetSelectionPipelinePolicyAsync(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow, parameterSetId, version, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Missing workflow activation.");
            var activation = TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection.TradeSelectionActivation.Read(row.PayloadJson);
            if (!string.Equals(activation.Hash(), row.PayloadSha256, StringComparison.Ordinal) || activation.ParameterSetId != parameterSetId || activation.Version != version)
                throw new InvalidOperationException("Workflow activation hash/identity mismatch.");
            var selector = await GetEffectiveTradeSelectionVersionAsync(activation.SelectionPolicyReference.Id, activation.SelectionPolicyReference.Version, activation.SelectionPolicyReference.PayloadSha256, effectiveFromUtc, cancellationToken)
            .ConfigureAwait(false);
            if (selector.ParameterSet.TargetHorizon != activation.TargetHorizon || !string.Equals(selector.ParameterSet.InstrumentRoot, activation.InstrumentRoot, StringComparison.Ordinal))
                throw new InvalidOperationException("Activation selector scope mismatch.");
        }

        var sql = ConfigurationDbSql.PublishFor(kind);
        var affected = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.Publish.{kind}", sql)
            .SetParameters(new PublishConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveFromUtc, parameterSetId, version, (short)ConfigurationParameterSetStatus.Draft))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        this.EnsureSingleLifecycleTransition(affected, kind, parameterSetId, version, "publish", "Draft");
    }

    /// <summary>Retires a published parameter-set version.</summary>
    /// <param name = "kind">The parameter-set kind.</param>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version.</param>
    /// <param name = "retiredAtUtc">The UTC retirement timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the retirement operation.</returns>
    public async Task RetireAsync(StrategyParameterSetKind kind, Guid parameterSetId, int version, DateTime retiredAtUtc, CancellationToken cancellationToken = default)
    {
        var sql = ConfigurationDbSql.RetireFor(kind);
        var affected = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.Retire.{kind}", sql)
            .SetParameters(new RetireConfiguration((short)ConfigurationParameterSetStatus.Retired, retiredAtUtc, parameterSetId, version, (short)ConfigurationParameterSetStatus.Published))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        this.EnsureSingleLifecycleTransition(affected, kind, parameterSetId, version, "retire", "Published");
    }

    /// <summary>Gets one exact immutable Regime Discovery version.</summary>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    public async Task<ResolvedRegimeDiscoveryParameterSet?> GetRegimeDiscoveryAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default)
    {
        var row = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExact)}", ConfigurationDbSql.GetExact)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(MapToConfigurationParameterSet, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : this.Resolve(row);
    }

    /// <summary>Gets one exact immutable Market Condition version.</summary>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    public async Task<ResolvedMarketConditionParameterSet?> GetMarketConditionAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default)
    {
        var row = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExactMarketCondition)}", ConfigurationDbSql.GetExactMarketCondition)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(MapToMarketConditionParameterSet, cancellationToken)
            .ConfigureAwait(false);
        return row is null ? null : this.ResolveMarketCondition(row);
    }

    /// <summary>Gets the effective published Regime Discovery version.</summary>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "targetHorizon">The target horizon.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective version, or <see langword="null"/> when none exists.</returns>
    public async Task<ResolvedRegimeDiscoveryParameterSet?> GetEffectiveRegimeDiscoveryAsync(DateTime effectiveAtUtc, TimeFrameType targetHorizon, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffective)}", ConfigurationDbSql.ResolveEffective)
            .SetParameters(new ResolveConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveAtUtc, (short)targetHorizon))
            .ExecuteQueryAsync(MapToConfigurationParameterSet, cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count > 1 && rows.ElementAt(0).EffectiveFromUtc == rows.ElementAt(1).EffectiveFromUtc)
            throw new InvalidOperationException("Effective Regime Discovery parameter selection is ambiguous.");
        return rows.Count == 0 ? null : this.Resolve(rows.First());
    }

    /// <summary>Gets the effective published Market Condition version.</summary>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "fundId">The fund identifier.</param>
    /// <param name = "instrumentRoot">The instrument root.</param>
    /// <param name = "targetHorizon">The target horizon.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective version, or <see langword="null"/> when none exists.</returns>
    public async Task<ResolvedMarketConditionParameterSet?> GetEffectiveMarketConditionAsync(DateTime effectiveAtUtc, int fundId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffectiveMarketCondition)}", ConfigurationDbSql.ResolveEffectiveMarketCondition)
            .SetParameters(new ResolveMarketConditionConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveAtUtc, fundId, instrumentRoot, (short)targetHorizon))
            .ExecuteQueryAsync(MapToMarketConditionParameterSet, cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count > 1)
            throw new InvalidOperationException("Effective Market Condition parameter selection is ambiguous.");
        return rows.Count == 0 ? null : this.ResolveMarketCondition(rows.First());
    }

    static ConfigurationParameterSet MapToConfigurationParameterSet(IObjectDataRecord row) => new(StrategyParameterSetKind.RegimeDiscovery, row.GetGuid(0), row.GetInt(1), checked((short)row.GetInt(2)), (ConfigurationParameterSetStatus)row.GetInt(3), row.IsNull(4) ? null : row.GetDateTime(4), row.IsNull(5) ? null : row.GetDateTime(5), row.GetString(6), row.GetString(7), row.GetString(8), row.GetDateTime(9), row.GetString(10));
    static ConfigurationParameterSet MapToMarketConditionParameterSet(IObjectDataRecord row) => MapToConfigurationParameterSet(row)with
    {
        Kind = StrategyParameterSetKind.MarketCondition
    };
    /// <summary>Gets lookup definitions for a group.</summary>
    /// <param name = "groupName">The lookup group name.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The ordered lookup definitions.</returns>
    public async Task<LookupDefinitionReadModel[]> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetLookupDefinitions)}", ConfigurationDbSql.GetLookupDefinitions)
            .SetParameters(new GetLookupDefinitions(groupName))
            .ExecuteQueryAsync(MapToLookupDefinition, cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count > 1024)
            throw new InvalidOperationException("Lookup group exceeds the supported response size.");
        return rows.ToArray();
    }

    static LookupDefinitionReadModel MapToLookupDefinition(IObjectDataRecord row) => new(row.GetInt(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4), row.GetInt(5), row.GetBool(6), row.GetDateTime(7), row.GetDateTime(8));
    /// <summary>Inserts an immutable Market Condition Assessment draft.</summary>
    /// <param name = "parameters">The validated parameter set.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertMarketConditionAssessmentDraftAsync(MarketConditionAssessmentParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        var normalized = parameters with
        {
            Sources = parameters.Sources
        };
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionAssessmentDraft)}", ConfigurationDbSql.InsertMarketConditionAssessmentDraft)
            .SetParameters(new InsertMarketConditionAssessmentDraft(normalized.ParameterSetId, normalized.Version, normalized.SchemaVersion, normalized.MarketProfileId, normalized.InstrumentRoot, (short)normalized.TargetHorizon, MarketConditionAssessmentHash.Serialize(normalized), MarketConditionAssessmentHash.Parameters(normalized), description ?? string.Empty, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets one exact Market Condition Assessment parameter set.</summary>
    /// <param name = "parameterSetId">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved parameter set, or <see langword="null"/> when it does not exist.</returns>
    public async Task<ResolvedMarketConditionAssessmentParameterSet?> GetMarketConditionAssessmentAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetMarketConditionAssessment)}", ConfigurationDbSql.GetMarketConditionAssessment)
            .SetParameters(new GetConfiguration(parameterSetId, version))
            .ExecuteSingleAsync(MapToMarketConditionAssessment, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets the effective Market Condition Assessment parameter set.</summary>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "marketProfileId">The market-profile identifier.</param>
    /// <param name = "instrumentRoot">The instrument root.</param>
    /// <param name = "targetHorizon">The target horizon.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective parameter set, or <see langword="null"/> when none exists.</returns>
    public async Task<ResolvedMarketConditionAssessmentParameterSet?> GetEffectiveMarketConditionAssessmentAsync(DateTime effectiveAtUtc, string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetEffectiveMarketConditionAssessment)}", ConfigurationDbSql.GetEffectiveMarketConditionAssessment)
            .SetParameters(new GetEffectiveMarketConditionAssessment(marketProfileId, instrumentRoot, (short)targetHorizon, effectiveAtUtc))
            .ExecuteQueryAsync(MapToMarketConditionAssessment, cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count > 1)
            throw new InvalidOperationException("Effective assessment profile selection is ambiguous.");
        return rows.FirstOrDefault();
    }

    static ResolvedMarketConditionAssessmentParameterSet MapToMarketConditionAssessment(IObjectDataRecord row)
    {
        var p = JsonSerializer.Deserialize<MarketConditionAssessmentParameterSet>(row.GetString(8)) ?? throw new InvalidOperationException("Invalid stored assessment configuration.");
        p.Validate();
        if (p.ParameterSetId != row.GetGuid(0) || p.Version != row.GetInt(1) || p.SchemaVersion != row.GetInt(2) || !string.Equals(p.MarketProfileId, row.GetString(3), StringComparison.Ordinal) || !string.Equals(p.InstrumentRoot, row.GetString(4), StringComparison.Ordinal) || (short)p.TargetHorizon != row.GetInt(5) || !string.Equals(MarketConditionAssessmentHash.Parameters(p), row.GetString(9), StringComparison.Ordinal))
            throw new InvalidOperationException("Assessment configuration metadata or payload hash mismatch.");
        return new(p, row.GetString(9), row.IsNull(7) ? null : DateTime.SpecifyKind(row.GetDateTime(7), DateTimeKind.Utc), (ConfigurationParameterSetStatus)row.GetInt(6));
    }

    /// <summary>Inserts an immutable volatility-series definition.</summary>
    /// <param name = "definition">The validated definition.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertVolatilitySeriesDefinitionAsync(VolatilitySeriesDefinition definition, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(definition);
        var hash = json.Sha256();
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertVolatilitySeriesDefinition)}", ConfigurationDbSql.InsertVolatilitySeriesDefinition)
            .SetParameters(new InsertVolatilitySeriesDefinition(definition.Identity.SeriesId, definition.Identity.MethodologyVersion, definition.DataIdentity.Environment, definition.Governance.EffectiveFromUtc.UtcDateTime, definition.Governance.EffectiveUntilUtc?.UtcDateTime, json, hash, definition.Governance.ApprovedConfigurationVersion, definition.Governance.Owner, definition.Governance.ApprovalEvidenceId))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
        var stored = await GetVolatilitySeriesDefinitionAsync(definition.Identity, cancellationToken)
            .ConfigureAwait(false);
        if (stored is null || !string.Equals(stored.PayloadSha256, hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Volatility series identity conflicts with different immutable content.");
    }

    /// <summary>Gets one exact volatility-series definition.</summary>
    /// <param name = "identity">The definition identity.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved definition, or <see langword="null"/> when it does not exist.</returns>
    public Task<ResolvedVolatilitySeriesDefinition?> GetVolatilitySeriesDefinitionAsync(VolatilitySeriesIdentity identity, CancellationToken cancellationToken = default)
    {
        return dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetVolatilitySeriesDefinition)}", ConfigurationDbSql.GetVolatilitySeriesDefinition)
            .SetParameters(new GetVolatilitySeriesDefinition(identity.SeriesId, identity.MethodologyVersion))
            .ExecuteSingleAsync(MapToVolatilitySeriesDefinition, cancellationToken);
    }

    /// <summary>Gets the effective volatility-series definition.</summary>
    /// <param name = "environment">The market-data environment.</param>
    /// <param name = "seriesId">The series identifier.</param>
    /// <param name = "effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective definition, or <see langword="null"/> when none exists.</returns>
    public async Task<ResolvedVolatilitySeriesDefinition?> GetEffectiveVolatilitySeriesDefinitionAsync(string environment, string seriesId, DateTimeOffset effectiveAtUtc, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetEffectiveVolatilitySeriesDefinition)}", ConfigurationDbSql.GetEffectiveVolatilitySeriesDefinition)
            .SetParameters(new GetEffectiveVolatilitySeriesDefinition(environment, seriesId, effectiveAtUtc.UtcDateTime))
            .ExecuteQueryAsync(MapToVolatilitySeriesDefinition, cancellationToken)
            .ConfigureAwait(false);
        if (rows.Count > 1)
            throw new InvalidOperationException("Effective volatility series selection is ambiguous.");
        return rows.FirstOrDefault();
    }

    static ResolvedVolatilitySeriesDefinition MapToVolatilitySeriesDefinition(IObjectDataRecord row)
    {
        var definition = JsonSerializer.Deserialize<VolatilitySeriesDefinition>(row.GetString(5)) ?? throw new InvalidDataException("Stored volatility series payload is missing.");
        definition.ValidateStoredVolatilityDefinition();
        var hash = JsonSerializer.Serialize(definition).Sha256();
        if (!string.Equals(definition.Identity.SeriesId, row.GetString(0), StringComparison.Ordinal) || !string.Equals(definition.Identity.MethodologyVersion, row.GetString(1), StringComparison.Ordinal) || !string.Equals(definition.DataIdentity.Environment, row.GetString(2), StringComparison.Ordinal) || !string.Equals(hash, row.GetString(6), StringComparison.Ordinal))
            throw new InvalidDataException("Stored volatility series identity or digest is invalid.");
        return new(definition, hash);
    }

    /// <summary>Creates a validated immutable strategy position-risk draft; activation remains explicit.</summary>
    /// <param name = "policy">The policy.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The created by.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public async Task InsertStrategyPositionRiskDraftAsync(TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.StrategyRiskParameterSet policy, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertStrategyPositionRiskDraft)}", ConfigurationDbSql.InsertStrategyPositionRiskDraft)
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, checked((short)policy.SchemaVersion), 0, policy.Serialize(), policy.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets one exact strategy position-risk version with validated payload identity and hash.</summary>
    /// <param name = "id">The id.</param>
    /// <param name = "version">The version.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The get strategy position risk version result.</returns>
    public async Task<TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.ResolvedStrategyRiskParameterSet?> GetStrategyPositionRiskVersionAsync(Guid id, int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetStrategyPositionRiskVersion)}", ConfigurationDbSql.GetStrategyPositionRiskVersion)
            .SetParameters(new GetConfiguration(id, version))
            .ExecuteSingleAsync(MapToStrategyPositionRiskVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Inserts an immutable Risk Management draft.</summary>
    /// <param name = "policy">The validated policy.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task InsertRiskManagementDraftAsync(RiskParameterSet policy, string description, string createdBy, CancellationToken token = default)
    {
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertRiskManagementDraft)}", ConfigurationDbSql.InsertRiskManagementDraft)
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0, policy.Serialize(), policy.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
    }

    /// <summary>Inserts an immutable strategy-catalog draft and its normalized children.</summary>
    /// <param name = "definition">The validated definition.</param>
    /// <param name = "expectedPreviousVersion">The expected previous version.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The canonical content digest.</returns>
     // Each operation owns its connection/transaction. Concurrent calls on a resolved context cannot
    // share the mutable ambient repository transaction. Connections still use the framework provider.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3267", Justification = "Retain sequential awaited database operations and explicit loops without allocating LINQ iterators on persistence paths.")]
    public async Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition, int expectedPreviousVersion, string createdBy, CancellationToken cancellationToken = default)
    {
        var d = Canonicalize(definition);
        var hash = CanonicalContentHash(d);
        var json = JsonSerializer.Serialize(d, JsonOptions);
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await this.CatalogWriteLock(connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        var latest = await this.Scalar(connection, transaction, ConfigurationDbSql.GetLatestStrategyCatalogVersion, cancellationToken, new CatalogIdentity((short)d.Key.Kind, d.Key.Id))
            .ConfigureAwait(false);
        if (Convert.ToInt32(latest, System.Globalization.CultureInfo.InvariantCulture) != expectedPreviousVersion)
            throw new InvalidOperationException("Catalog authoring conflict: expected previous version does not match.");
        var code = await this.Scalar(connection, transaction, ConfigurationDbSql.GetStrategyCatalogIdentityCode, cancellationToken, new CatalogIdentity((short)d.Key.Kind, d.Key.Id))
            .ConfigureAwait(false);
        if (code is string existing && !string.Equals(existing, d.Code, StringComparison.Ordinal))
            throw new InvalidOperationException("Catalog identity code is immutable.");
        // Exact dependencies must exist even for drafts. They need not be published until publication.
        foreach (var key in Dependencies(d))
            if (await this.ReadCatalog(connection, transaction, key, true, cancellationToken)
            .ConfigureAwait(false)is null)
                throw new InvalidOperationException($"Missing catalog dependency: {key}.");
        if (d.Key.Kind == StrategyCatalogKind.ParameterSet)
        {
            var schema = await this.ReadCatalog(connection, transaction, d.Parent!, true, cancellationToken)
            .ConfigureAwait(false);
            ValidateParameters(ReadShape(schema!.Definition.Settings), d.Settings);
        }

        var now = DateTime.UtcNow.CatalogNow();
        await this.Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogIdentity, cancellationToken, new InsertCatalogIdentity((short)d.Key.Kind, d.Key.Id, d.Code, now, createdBy))
            .ConfigureAwait(false);
        await this.Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogVersion, cancellationToken, new InsertCatalogVersion(json, hash, now, createdBy))
            .ConfigureAwait(false);
        foreach (var child in ConfigurationSchemaSql.StrategyCatalog.Children)
            await this.Execute(connection, transaction, ConfigurationDbSql.InsertStrategyCatalogChildren(child), cancellationToken, new TextValue(json))
            .ConfigureAwait(false);
        await this.Execute(connection, transaction, ConfigurationDbSql.SealStrategyCatalogVersion, cancellationToken, new CatalogVersion((short)d.Key.Kind, d.Key.Id, d.Key.Version))
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        return hash;
    }

    /// <summary>Gets one exact strategy-catalog definition.</summary>
    /// <param name = "key">The catalog key.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The stored definition, or <see langword="null"/> when it does not exist.</returns>
    public async Task<StoredStrategyCatalogDefinition?> GetStrategyCatalogAsync(CatalogKey key, CancellationToken cancellationToken = default)
    {
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        return await this.ReadCatalog(connection, null, key, false, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets the latest strategy-catalog versions ordered by stable code.</summary>
    /// <param name = "kind">The catalog kind.</param>
    /// <param name = "limit">The maximum result count.</param>
    /// <param name = "afterCode">The exclusive stable-code cursor.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The strategy-catalog summaries.</returns>
    public async Task<IReadOnlyList<StrategyCatalogSummary>> GetStrategyCatalogsAsync(StrategyCatalogKind kind, int limit = 50, string? afterCode = null, CancellationToken cancellationToken = default)
    {
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var command = this.Command(connection, null, ConfigurationDbSql.GetStrategyCatalogs, new ListCatalogs((short)kind, afterCode ?? "", limit));
        await using var commandDisposal = command.ConfigureAwait(false);
        var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var result = new List<StrategyCatalogSummary>();
        while (await reader.ReadAsync(cancellationToken)
            .ConfigureAwait(false))
            result.Add(MapToStrategyCatalogSummary(new AdoNetDataRecord().SetReader(reader), kind));
        return result;
    }

    /// <summary>Publishes a strategy-catalog draft.</summary>
    /// <param name = "key">The catalog key.</param>
    /// <param name = "expectedContentHash">The expected content digest.</param>
    /// <param name = "effectiveFromUtc">The UTC effective timestamp.</param>
    /// <param name = "publishedBy">The publisher.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    public async Task PublishStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime effectiveFromUtc, string publishedBy, CancellationToken cancellationToken = default)
    {
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await this.CatalogWriteLock(connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        var graph = await this.LoadCatalogGraph(connection, transaction, key, effectiveFromUtc, true, cancellationToken)
            .ConfigureAwait(false);
        var root = graph[key];
        if (root.Status != CatalogLifecycleStatus.Draft || !string.Equals(root.ContentHash, expectedContentHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Publish requires the exact Draft content hash.");
        await this.ValidateCatalogGraph(connection, transaction, graph, effectiveFromUtc, cancellationToken)
            .ConfigureAwait(false);
        var count = await this.Execute(connection, transaction, ConfigurationDbSql.PublishStrategyCatalog, cancellationToken, new PublishCatalog((short)key.Kind, key.Id, key.Version, effectiveFromUtc, publishedBy, expectedContentHash))
            .ConfigureAwait(false);
        if (count != 1)
            throw new InvalidOperationException("Catalog publication conflict.");
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Retires a published strategy-catalog version.</summary>
    /// <param name = "key">The catalog key.</param>
    /// <param name = "expectedContentHash">The expected content digest.</param>
    /// <param name = "retiredAtUtc">The UTC retirement timestamp.</param>
    /// <param name = "retiredBy">The retiring user.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the retirement operation.</returns>
    public async Task RetireStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime retiredAtUtc, string retiredBy, CancellationToken cancellationToken = default)
    {
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await this.CatalogWriteLock(connection, transaction, cancellationToken)
            .ConfigureAwait(false);
        var current = await this.ReadCatalog(connection, transaction, key, true, cancellationToken)
            .ConfigureAwait(false);
        if (current is null || current.Status != CatalogLifecycleStatus.Published || !string.Equals(current.ContentHash, expectedContentHash, StringComparison.Ordinal) || retiredAtUtc < current.EffectiveFromUtc)
            throw new InvalidOperationException("Retire requires the exact Published version and a timestamp at or after publication.");
        await this.Execute(connection, transaction, ConfigurationDbSql.RetireStrategyCatalog, cancellationToken, new RetireCatalog((short)key.Kind, key.Id, key.Version, retiredAtUtc, retiredBy))
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets a validated published strategy deployment snapshot.</summary>
    /// <param name = "deployment">The deployment catalog key.</param>
    /// <param name = "asOfUtc">The UTC selection timestamp.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The validated deployment snapshot.</returns>
    public async Task<StrategyCatalogSnapshot> GetPublishedStrategyDeploymentAsync(CatalogKey deployment, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        var graph = await this.LoadCatalogGraph(connection, transaction, deployment, asOfUtc, false, cancellationToken)
            .ConfigureAwait(false);
        await this.ValidateCatalogGraph(connection, transaction, graph, asOfUtc, cancellationToken)
            .ConfigureAwait(false);
        var definitions = graph.Values.OrderBy(x => x.Definition.Key.Kind).ThenBy(x => x.Definition.Key.Id).ThenBy(x => x.Definition.Key.Version).ToArray();
        var hash = Sha(CanonicalJson(JsonSerializer.SerializeToElement(new { SchemaVersion = 1, Deployment = deployment, Definitions = definitions.Select(x => new { x.Definition.Key, x.ContentHash }).ToArray() }, JsonOptions)));
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(deployment, asOfUtc, definitions, hash);
    }

    /// <summary>Inserts an immutable order-composition draft.</summary>
    /// <param name = "policy">The validated policy.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertOrderCompositionDraft)}", ConfigurationDbSql.InsertOrderCompositionDraft)
            .SetParameters(new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0, policy.Serialize(), policy.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Inserts an immutable Trade Selection activation draft.</summary>
    /// <param name = "activation">The validated activation.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertTradeSelectionActivationDraftAsync(TradeSelectionActivation activation, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertStrategyWorkflowDraft)}", ConfigurationDbSql.InsertStrategyWorkflowDraft)
            .SetParameters(new InsertConfigurationDraft(activation.ParameterSetId, activation.Version, activation.SchemaVersion, 0, activation.Serialize(), activation.Hash(), description, DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets an effective Trade Selection activation.</summary>
    /// <param name = "id">The activation identifier.</param>
    /// <param name = "version">The activation version.</param>
    /// <param name = "hash">The expected payload digest.</param>
    /// <param name = "at">The at.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective activation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<TradeSelectionActivation> GetEffectiveTradeSelectionActivationAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default)
    {
        var row = await GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow, id, version, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("Exact workflow activation is missing.");
        var activation = TradeSelectionActivation.Read(row.PayloadJson);
        if (activation.ParameterSetId != id || activation.Version != version || !string.Equals(activation.Hash(), hash, StringComparison.Ordinal) || !string.Equals(row.PayloadSha256, hash, StringComparison.Ordinal) || row.Status != CatalogLifecycleStatus.Published || row.EffectiveFromUtc is null || row.EffectiveFromUtc > at || row.RetiredAtUtc <= at)
            throw new InvalidOperationException("Exact published workflow activation is invalid or unavailable.");
        var selected = await GetEffectiveTradeSelectionVersionAsync(activation.SelectionPolicyReference.Id, activation.SelectionPolicyReference.Version, activation.SelectionPolicyReference.PayloadSha256, at, cancellationToken)
            .ConfigureAwait(false);
        if (selected.ParameterSet.TargetHorizon != activation.TargetHorizon || !string.Equals(selected.ParameterSet.InstrumentRoot, activation.InstrumentRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Activation selector scope mismatch.");
        return activation;
    }

    /// <summary>Inserts an immutable Trade Selection draft.</summary>
    /// <param name = "parameters">The validated parameter set.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertTradeSelectionDraftAsync(TradeSelectionParameterSet parameters, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertTradeSelectionDraft)}", ConfigurationDbSql.InsertTradeSelectionDraft)
            .SetParameters(new InsertConfigurationDraft(parameters.ParameterSetId, parameters.Version, parameters.SchemaVersion, 0, TradeSelectionPolicy.Serialize(parameters), TradeSelectionPolicy.Hash(parameters), description ?? "", DateTime.UtcNow, createdBy))
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets one exact Trade Selection parameter-set version.</summary>
    /// <param name = "id">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    public async Task<ResolvedTradeSelectionParameterSet?> GetTradeSelectionVersionAsync(Guid id, int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetTradeSelectionVersion)}", ConfigurationDbSql.GetTradeSelectionVersion)
            .SetParameters(new GetConfiguration(id, version))
            .ExecuteSingleAsync(MapToTradeSelectionVersion, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets an effective Trade Selection parameter-set version.</summary>
    /// <param name = "id">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "hash">The expected payload digest.</param>
    /// <param name = "at">The at.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The effective parameter-set version.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<ResolvedTradeSelectionParameterSet> GetEffectiveTradeSelectionVersionAsync(Guid id, int version, string hash, DateTime at, CancellationToken cancellationToken = default)
    {
        var row = await GetTradeSelectionVersionAsync(id, version, cancellationToken)
            .ConfigureAwait(false);
        if (row is null || !string.Equals(row.PayloadSha256, hash, StringComparison.Ordinal) || row.Status != ConfigurationParameterSetStatus.Published || row.EffectiveFromUtc > at || row.EffectiveFromUtc is null || row.RetiredAtUtc <= at)
            throw new InvalidOperationException("TS.CONFIG.MISSING: Exact effective published selector policy is required.");
        return row;
    }

    /// <summary>Gets one exact pipeline policy snapshot.</summary>
    /// <param name = "kind">The pipeline parameter kind.</param>
    /// <param name = "id">The parameter-set identifier.</param>
    /// <param name = "version">The version number.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The policy snapshot, or <see langword="null"/> when it does not exist.</returns>
    public async Task<SelectionPipelinePolicySnapshot?> GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, CancellationToken cancellationToken = default)
    {
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetSelectionPipelinePolicyFor)}.{kind}", ConfigurationDbSql.GetSelectionPipelinePolicyFor(kind))
            .SetParameters(new GetConfiguration(id, version))
            .ExecuteSingleAsync(MapToSelectionPipelinePolicy, cancellationToken)
            .ConfigureAwait(false);
    }

    static ResolvedTradeSelectionParameterSet MapToTradeSelectionVersion(IObjectDataRecord row)
    {
        var parameterSet = TradeSelectionPolicy.Read(row.GetString(6));
        if (parameterSet.ParameterSetId != row.GetGuid(0) || parameterSet.Version != row.GetInt(1) || parameterSet.SchemaVersion != row.GetInt(2) || !string.Equals(TradeSelectionPolicy.Hash(parameterSet), row.GetString(7), StringComparison.Ordinal))
            throw new InvalidOperationException("TS.CONTRACT.HASH: Stored policy metadata/hash mismatch.");
        return new(parameterSet, row.GetString(7), (ConfigurationParameterSetStatus)row.GetInt(3), row.IsNull(4) ? null : DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc), row.IsNull(5) ? null : DateTime.SpecifyKind(row.GetDateTime(5), DateTimeKind.Utc));
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
    /// <summary>Projects a parameter-assignment fact.</summary>
    /// <param name = "fact">The assignment fact.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task ProjectParameterAssignmentAsync(ParameterAssignmentChangedEvent fact, CancellationToken token = default)
    {
        var connection = await this.OpenCatalogAsync(token)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await this.ProjectCatalogAssignmentAsync(new ConfigurationDbSession(connection,transaction),fact,token)
            .ConfigureAwait(false);
        await transaction.CommitAsync(token)
            .ConfigureAwait(false);
    }

    /// <summary>Materializes an accepted parameter assignment fact on the caller-owned source-event transaction.</summary>
    /// <param name = "transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name = "fact">The fact.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public Task ProjectParameterAssignmentAsync(IEnlistedPostgresTransaction transaction, ParameterAssignmentChangedEvent fact, CancellationToken cancellationToken = default)
    {
        return this.ProjectCatalogAssignmentAsync(transaction,fact,cancellationToken);
    }

    /// <summary>Gets legacy parameter versions.</summary>
    /// <param name = "setId">The optional legacy set identifier.</param>
    /// <param name = "version">The optional exact version, or zero for all versions.</param>
    /// <param name = "offset">The result offset.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>The legacy parameter versions.</returns>
    public async Task<ParameterLegacyVersion[]> GetLegacyParameterVersionsAsync(Guid? setId = null, int version = 0, int offset = 0, CancellationToken cancellationToken = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetLegacyParameterVersions)}", ConfigurationDbSql.GetLegacyParameterVersions)
            .SetParameters(new GetLegacyParameterVersions(setId, version, offset))
            .ExecuteQueryAsync(MapToLegacyParameterVersion, cancellationToken)
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    static ParameterLegacyVersion MapToLegacyParameterVersion(IObjectDataRecord row) => new(new("regime_discovery_parameter_set", row.GetGuid(0), row.GetInt(1), row.GetString(4), "regime-typed-json-v1"), checked((short)row.GetInt(2)), row.GetString(3), row.GetString(5), checked((short)row.GetInt(6)));
    /// <summary>Gets registered parameter components.</summary>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>The registered parameter components.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<ParameterComponentSummary[]> GetParameterComponentsAsync(CancellationToken token = default)
    {
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetParameterComponents)}", ConfigurationDbSql.GetParameterComponents)
            .ExecuteQueryAsync(MapToParameterComponent, token)
            .ConfigureAwait(false);
        return rows.ToArray();
    }

    static ParameterComponentSummary MapToParameterComponent(IObjectDataRecord row) => new(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), JsonSerializer.Deserialize<int[]>(row.GetString(4)) ?? [], ParameterSchemaRegistry.Default.Definitions.Any(definition => string.Equals(definition.ComponentCode, row.GetString(2), StringComparison.Ordinal)));
    /// <summary>Gets a parameter schema definition.</summary>
    /// <param name = "componentCode">The component code.</param>
    /// <param name = "version">The schema version.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>The schema definition, or <see langword="null"/> when it does not exist.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<ParameterSchemaDefinition?> GetParameterSchemaAsync(string componentCode, int version, CancellationToken token = default)
    {
        var stored = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetParameterSchema)}", ConfigurationDbSql.GetParameterSchema)
            .SetParameters(new GetParameterSchema(componentCode, version))
            .ExecuteSingleAsync(MapToParameterSchema, token)
            .ConfigureAwait(false);
        if (stored is null)
            return null;
        var registered = ParameterSchemaRegistry.Default.Definitions.SingleOrDefault(x => string.Equals(x.ComponentCode, componentCode, StringComparison.Ordinal) && x.Version == version);
        if (registered is null)
            return stored;
        if (!string.Equals(registered.Codec, stored.Codec, StringComparison.Ordinal) || !string.Equals(registered.SchemaSha256, stored.SchemaSha256, StringComparison.Ordinal) || !System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(registered.JsonSchema), System.Text.Json.Nodes.JsonNode.Parse(stored.JsonSchema)))
            throw new InvalidDataException("PARAM.REGISTRY_SCHEMA_MISMATCH");
        return registered;
    }

    static ParameterSchemaDefinition MapToParameterSchema(IObjectDataRecord row) => new(row.GetString(0), row.GetInt(1), row.GetString(2), row.GetString(4), row.GetString(3));
    /// <summary>Projects a parameter-set fact.</summary>
    /// <param name = "fact">The parameter-set fact.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task ProjectParameterSetAsync(IParameterSetFact fact, CancellationToken token = default)
    {
        var connection = await this.OpenCatalogAsync(token)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await this.ProjectCatalogSetAsync(new ConfigurationDbSession(connection,transaction),fact,token)
            .ConfigureAwait(false);
        await transaction.CommitAsync(token)
            .ConfigureAwait(false);
    }

    /// <summary>Materializes an accepted parameter set fact on the caller-owned source-event transaction.</summary>
    /// <param name = "transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name = "fact">The fact.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public Task ProjectParameterSetAsync(IEnlistedPostgresTransaction transaction, IParameterSetFact fact, CancellationToken cancellationToken = default)
    {
        return this.ProjectCatalogSetAsync(transaction,fact,cancellationToken);
    }

    /// <summary>Gets parameter-set versions using stable cursor pagination.</summary>
    /// <param name = "componentCode">The component code.</param>
    /// <param name = "setId">The optional parameter-set identifier.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <param name = "limit">The maximum result count.</param>
    /// <param name = "afterName">The exclusive name cursor.</param>
    /// <param name = "afterSetId">The exclusive set-identifier cursor.</param>
    /// <param name = "afterVersion">The exclusive version cursor.</param>
    /// <returns>The parameter-set versions.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<ParameterSetVersion[]> GetParameterSetsAsync(string componentCode, Guid? setId = null, CancellationToken token = default, int limit = 100, string afterName = "", Guid? afterSetId = null, int afterVersion = 0)
    {
        await using var connection=await this.OpenCatalogAsync(token).ConfigureAwait(false);
        await using var transaction=await connection.BeginTransactionAsync(token).ConfigureAwait(false);
        var session=new ConfigurationDbSession(connection,transaction);
        var page=await session.QueryAsync(ConfigurationDbSql.TypedCatalog.VersionPage,[componentCode,setId,afterName,afterSetId,afterVersion,limit],
            reader=>(Id:reader.GetGuid(0),Version:reader.GetInt64(1),Name:reader.GetString(2),Description:reader.GetString(3),Revision:reader.GetInt64(4)),token).ConfigureAwait(false);
        var result=new List<ParameterSetVersion>();
        foreach(var row in page)
        {
            var metadata=await this.ReadCatalogAsync<ParameterVersionMetadata>(session,row.Id,row.Version,token).ConfigureAwait(false)
                ??throw new InvalidDataException("Missing parameter metadata");
            var values=await GetTypedParameterValueAsync(session,row.Id,checked((int)row.Version),token).ConfigureAwait(false)
                ??throw new InvalidDataException("Missing typed parameter values");
            result.Add(metadata.Restore(values) with{Name=row.Name,Description=row.Description,CatalogRevision=row.Revision});
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return result.ToArray();
    }

    /// <summary>Projects a parameter-startup fact.</summary>
    /// <param name = "fact">The startup fact.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task ProjectParameterStartupAsync(ParameterStartupChangedEvent fact, CancellationToken token = default)
    {
        var connection = await this.OpenCatalogAsync(token)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(token)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        await this.ProjectCatalogStartupAsync(new ConfigurationDbSession(connection,transaction),fact,token)
            .ConfigureAwait(false);
        await transaction.CommitAsync(token)
            .ConfigureAwait(false);
    }

    /// <summary>Materializes an accepted parameter startup fact on the caller-owned source-event transaction.</summary>
    /// <param name = "transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name = "fact">The fact.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the persistence operation.</returns>
    public Task ProjectParameterStartupAsync(IEnlistedPostgresTransaction transaction, ParameterStartupChangedEvent fact, CancellationToken cancellationToken = default)
    {
        return this.ProjectCatalogStartupAsync(transaction,fact,cancellationToken);
    }

    /// <summary>Acquires the cross-host parameter write lease.</summary>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>The asynchronous lease handle.</returns>
     // Serializes lifecycle/assignment decisions across hosts while the event stream remains authoritative.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<IAsyncDisposable> AcquireParameterWriteLeaseAsync(CancellationToken token = default)
    {
        var connection = await (this.OpenCatalogAsync(token))
            .ConfigureAwait(false);
        NpgsqlTransaction? transaction = null;
        try
        {
            transaction = await (connection.BeginTransactionAsync(token))
            .ConfigureAwait(false);
            await (this.Execute(connection, transaction, ConfigurationDbSql.SetParameterWriterLockTimeout, token))
            .ConfigureAwait(false);
            await (this.Execute(connection, transaction, ConfigurationDbSql.AcquireParameterWriterLock, token))
            .ConfigureAwait(false);
            return new ParameterWriteLease(connection, transaction);
        }
        catch
        {
            if (transaction is not null)
                await (transaction.DisposeAsync())
            .ConfigureAwait(false);
            await (connection.DisposeAsync())
            .ConfigureAwait(false);
            throw;
        }
    }

    internal static TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.ResolvedStrategyRiskParameterSet MapToStrategyPositionRiskVersion(IObjectDataRecord row)
    {
        var policy = TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.StrategyRiskParameterSet.Read(row.GetString(6));
        if (policy.ParameterSetId != row.GetGuid(0) || policy.Version != row.GetInt(1) || policy.SchemaVersion != row.GetInt(2) || !string.Equals(policy.Hash(), row.GetString(7), StringComparison.Ordinal))
            throw new InvalidDataException("StrategyRiskParameterSet.IDENTITY_OR_HASH_MISMATCH");
        return new TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk.ResolvedStrategyRiskParameterSet(policy, row.GetString(7), (ConfigurationParameterSetStatus)row.GetInt(3), row.IsNull(4) ? null : DateTime.SpecifyKind(row.GetDateTime(4), DateTimeKind.Utc), row.IsNull(5) ? null : DateTime.SpecifyKind(row.GetDateTime(5), DateTimeKind.Utc));
    }

    internal static StoredStrategyCatalogDefinition MapToStrategyCatalog(IObjectDataRecord row) => new(Freeze(JsonSerializer.Deserialize<StrategyCatalogDefinition>(row.GetString(0), JsonOptions)!), row.GetString(1), (CatalogLifecycleStatus)row.GetInt(2), row.GetDateTime(3), row.GetString(4), row.IsNull(5) ? null : row.GetDateTime(5), row.IsNull(6) ? null : row.GetString(6), row.IsNull(7) ? null : row.GetDateTime(7), row.IsNull(8) ? null : row.GetString(8));
    internal static StrategyCatalogSummary MapToStrategyCatalogSummary(IObjectDataRecord row, StrategyCatalogKind kind) => new(new(kind, row.GetGuid(0), row.GetInt(1)), row.GetString(2), row.GetString(3), (CatalogLifecycleStatus)row.GetInt(4), row.GetString(5));

    /// <summary>Persists an immutable typed value. An identical replay is a no-op; conflicting content fails.</summary>
    /// <remarks>The caller owns the transaction that also appends the source event. No transaction is committed here.</remarks>
    /// <param name="transaction">The source event's PostgreSQL transaction.</param>
    /// <param name="value">The concrete immutable version to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after all typed rows are inserted or the identical replay is verified.</returns>
    public Task InsertTypedParameterValueAsync(IEnlistedPostgresTransaction transaction, ParameterSetValue value, CancellationToken cancellationToken = default)=>WriteTypedValueAsync(transaction,value,cancellationToken);

    internal static async Task WriteTypedValueAsync(IEnlistedPostgresTransaction transaction, ParameterSetValue value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(value);
        if (value.SetId == Guid.Empty || value.Version < 1) throw new ArgumentException("Parameter identity and version are required", nameof(value));
        // Freeze mutable child arrays before hashing or binding them.
        value = ParameterValueCodec.Snapshot(value);
        var rows = ParameterValueMapping.Write(value);
        var hash = ParameterValueCodec.Hash(value);
        await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.Lock, [value.SetId.ToString("D")], cancellationToken).ConfigureAwait(false);
        var existing = await transaction.ScalarAsync(ConfigurationDbSql.TypedParameters.Hash,
            [value.SetId, value.Version], cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals((string)existing, hash, StringComparison.Ordinal)) throw new InvalidOperationException("PARAM.IMMUTABLE_CONTENT_CONFLICT");
            // Verify rows as well: a header alone is not proof that the projection is complete.
            await ReadTypedValueAsync(transaction, value.SetId, value.Version, cancellationToken).ConfigureAwait(false);
            return;
        }
        await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.InsertHeader,
            [value.SetId, value.Version, value.ComponentCode, value.SchemaVersion, hash, ParameterValueCodec.Codec], cancellationToken).ConfigureAwait(false);
        foreach (var row in rows)
            await transaction.ExecuteAsync(ConfigurationDbSql.TypedParameters.Insert(row.Table),
                [value.SetId, value.Version, row.Path, row.ParentPath, row.Ordinal, ..row.Values], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Loads concrete parameter values from typed columns and verifies the stored content fingerprint.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="setId">The exact parameter-set identifier.</param>
    /// <param name="version">The immutable version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The concrete value, or null if the version does not exist.</returns>
    public Task<ParameterSetValue?> GetTypedParameterValueAsync(IEnlistedPostgresTransaction transaction, Guid setId, int version, CancellationToken cancellationToken = default)=>ReadTypedValueAsync(transaction,setId,version,cancellationToken);

    internal static async Task<ParameterSetValue?> ReadTypedValueAsync(IEnlistedPostgresTransaction transaction, Guid setId, int version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var headers = await transaction.QueryAsync(ConfigurationDbSql.TypedParameters.ReadHeader, [setId, version],
            reader => (Component: reader.GetString(0), Schema: reader.GetInt32(1), Hash: reader.GetString(2), Codec: reader.GetString(3)), cancellationToken).ConfigureAwait(false);
        if (headers.Count == 0) return null;
        var header = headers.Single();
        if (header.Codec != ParameterValueCodec.Codec) throw new InvalidOperationException("PARAM.CODEC_UNSUPPORTED");
        var rows = new List<ParameterValueMapping.Row>();
        foreach (var table in ParameterValueMapping.TablesFor(header.Component))
        {
            var batch = await transaction.QueryAsync(ConfigurationDbSql.TypedParameters.Read(table), [setId, version],
                reader => new ParameterValueMapping.Row(table, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2),
                    Enumerable.Range(0, table.Columns.Count).Select(index => reader.IsDBNull(index + 3) ? null : reader.GetValue(index + 3)).ToArray()), cancellationToken).ConfigureAwait(false);
            rows.AddRange(batch);
        }
        var value = ParameterValueMapping.Read(header.Component, rows);
        if (value.SetId != setId || value.Version != version || value.SchemaVersion != header.Schema ||
            !string.Equals(ParameterValueCodec.Hash(value), header.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException("PARAM.PROJECTION_CONTENT_MISMATCH");
        return value;
    }

    /// <summary>Reads one immutable assignment revision and verifies all scalar and child rows.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="assignmentId">The assignment identity.</param>
    /// <param name="revision">The exact accepted revision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed revision, or null when it does not exist.</returns>
    public Task<ParameterAssignmentRevision?> GetParameterAssignmentProjectionAsync(IEnlistedPostgresTransaction transaction, Guid assignmentId, long revision, CancellationToken cancellationToken=default)
        => this.ReadCatalogAsync<ParameterAssignmentRevision>(transaction,assignmentId,revision,cancellationToken);

    /// <summary>Reads and verifies the typed before/after audit values for an operation.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="operationId">The accepted operation identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable audit entry, or null when absent.</returns>
    public Task<ParameterAuditEntry?> GetParameterAuditProjectionAsync(IEnlistedPostgresTransaction transaction, Guid operationId, CancellationToken cancellationToken=default)
        => this.ReadCatalogAsync<ParameterAuditEntry>(transaction,operationId,1,cancellationToken);

    /// <summary>Loads a frozen startup projection, resolving each exact version from typed value tables.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="runId">The frozen startup generation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verified startup generation, or null when absent.</returns>
    public async Task<ParameterStartupRun?> GetParameterStartupProjectionAsync(IEnlistedPostgresTransaction transaction, Guid runId, CancellationToken cancellationToken=default)
    {
        var stored=await this.ReadCatalogAsync<ParameterStartupProjection>(transaction,runId,1,cancellationToken).ConfigureAwait(false);
        if(stored is null)return null;
        var versions=new List<ParameterSetVersion>();
        foreach(var metadata in stored.Versions)
        {
            var values=await GetTypedParameterValueAsync(transaction,metadata.Reference.SetId,metadata.Reference.Version,cancellationToken).ConfigureAwait(false)
                ??throw new InvalidDataException("PARAM.STARTUP_VALUES_MISSING");
            versions.Add(metadata.Restore(values));
        }
        return new(stored.RunId,stored.Scopes,versions.ToArray(),stored.Plan,stored.CreatedAtUtc,stored.CreatedBy);
    }

    /// <summary>Reads and verifies a startup preparation report and its ordered outcomes.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="runId">The startup generation identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed report, or null when absent.</returns>
    public Task<ParameterSignalStartupReport?> GetParameterStartupReportProjectionAsync(IEnlistedPostgresTransaction transaction, Guid runId, CancellationToken cancellationToken=default)
        => this.ReadCatalogAsync<ParameterSignalStartupReport>(transaction,runId,1,cancellationToken);
}
