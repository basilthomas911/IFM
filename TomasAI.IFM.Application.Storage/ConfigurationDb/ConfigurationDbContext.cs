using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
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
/// <param name="timeProvider">The business clock used for caller-visible timestamps.</param>
public sealed class ConfigurationDbContext(IDbConnectionSettings connectionSettings, IDbContextFactory dbFactory, ILogger<DbProvider> logger, TimeProvider? timeProvider = null) : ObjectDataRepository<ConfigurationDbContext>(connectionSettings[ConfigurationDbConnection], logger), IConfigurationDbContext
{
    /// <summary>Business clock for dated projections; transport and database limits remain physical.</summary>
    internal TimeProvider BusinessClock { get; } = timeProvider ?? TimeProvider.System;

    /// <summary>Gets the configuration connection setting name.</summary>
    public const string ConfigurationDbConnection = "ConfigurationDbConnection";
    /// <summary>Gets the concrete configuration database context.</summary>
    public override ConfigurationDbContext Database => this;
    /// <summary>Gets the configuration database read capability.</summary>
    public IConfigurationDbReadContext DbReader => this;
    /// <summary>Gets the configuration database write capability.</summary>
    public IConfigurationDbWriteContext DbWriter => this;

    internal sealed record BaselineColumnLayout(
        string Name, string TypeSchema, string TypeName, string Nullable, string Default, string Identity, string? IdentityGeneration);

    internal static BaselineColumnLayout MapToBaselineColumn(System.Data.Common.DbDataReader row)
        => new(row.GetString(0), row.GetString(1), row.GetString(2), row.GetString(3), row.GetString(4),
            row.GetString(5), row.IsDBNull(6) ? null : row.GetString(6));

    internal static (long Last, bool Called) MapToBaselineLookupSequence(System.Data.Common.DbDataReader row)
        => (row.GetInt64(0), row.GetBoolean(1));

    internal static (string Request, string Fact) MapToCatalogReceipt(System.Data.Common.DbDataReader row)
        => (row.GetString(0), row.GetString(1));

    /// <inheritdoc/>
    public Task<ConfigurationBaselineDatabaseImage> CaptureConfigurationBaselineAsync(EnlistedEventTransaction transaction,
        CancellationToken cancellationToken = default)
        => this.CaptureBaselineImageAsync(transaction, cancellationToken);

    /// <inheritdoc/>
    public bool MatchesConfigurationBaselineLayout(int schemaVersion, IEnumerable<string> tables)
        => this.MatchesBaselineLayout(schemaVersion, tables);

    /// <inheritdoc/>
    public Task<bool> ClaimConfigurationBaselineAsync(IEnlistedPostgresTransaction transaction, Guid baselineId,
        long sourceEventId, long sourceCut, CancellationToken cancellationToken = default)
        => this.ClaimConfigurationBaselineCoreAsync(transaction, baselineId, sourceEventId, sourceCut, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> MaterializeConfigurationBaselineAsync(IEnlistedPostgresTransaction transaction,
        ConfigurationProjectionBaselineEvent source, CancellationToken cancellationToken = default)
        => this.RestoreBaselineImageAsync(transaction, source, cancellationToken);


    /// <summary>Inserts an immutable Regime Discovery draft.</summary>
    /// <param name = "parameterSet">The validated parameter set.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertRegimeDiscoveryDraftAsync(RegimeDiscoveryParameterSet parameterSet, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        var payload = RegimeDiscoveryParameterPayload.Serialize(parameterSet);
        var preparedParameters1 = new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version, checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft, payload, RegimeDiscoveryParameterPayload.ComputeSha256(payload), description ?? string.Empty, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertDraft)}", ConfigurationDbSql.InsertDraft)
            .SetParameters(preparedParameters1)
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
        var preparedParameters2 = new InsertConfigurationDraft(parameterSet.ParameterSetId, parameterSet.Version, checked((short)parameterSet.SchemaVersion), (short)ConfigurationParameterSetStatus.Draft, payload, MarketConditionParameterPayload.ComputeSha256(payload), description ?? string.Empty, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionDraft)}", ConfigurationDbSql.InsertMarketConditionDraft)
            .SetParameters(preparedParameters2)
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

        var sql = kind.PublishFor();
        var preparedParameters3 = new PublishConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveFromUtc, parameterSetId, version, (short)ConfigurationParameterSetStatus.Draft);
        var affected = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.Publish.{kind}", sql)
            .SetParameters(preparedParameters3)
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
        var sql = kind.RetireFor();
        var preparedParameters4 = new RetireConfiguration((short)ConfigurationParameterSetStatus.Retired, retiredAtUtc, parameterSetId, version, (short)ConfigurationParameterSetStatus.Published);
        var affected = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.Retire.{kind}", sql)
            .SetParameters(preparedParameters4)
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
        var preparedParameters5 = new GetConfiguration(parameterSetId, version);
        var row = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExact)}", ConfigurationDbSql.GetExact)
            .SetParameters(preparedParameters5)
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
        var preparedParameters6 = new GetConfiguration(parameterSetId, version);
        var row = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetExactMarketCondition)}", ConfigurationDbSql.GetExactMarketCondition)
            .SetParameters(preparedParameters6)
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
        var preparedParameters7 = new ResolveConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveAtUtc, (short)targetHorizon);
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffective)}", ConfigurationDbSql.ResolveEffective)
            .SetParameters(preparedParameters7)
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
        var preparedParameters8 = new ResolveMarketConditionConfiguration((short)ConfigurationParameterSetStatus.Published, effectiveAtUtc, fundId, instrumentRoot, (short)targetHorizon);
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.ResolveEffectiveMarketCondition)}", ConfigurationDbSql.ResolveEffectiveMarketCondition)
            .SetParameters(preparedParameters8)
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
        var preparedParameters9 = new GetLookupDefinitions(groupName);
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetLookupDefinitions)}", ConfigurationDbSql.GetLookupDefinitions)
            .SetParameters(preparedParameters9)
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
        var preparedParameters10 = new InsertMarketConditionAssessmentDraft(normalized.ParameterSetId, normalized.Version, normalized.SchemaVersion, normalized.MarketProfileId, normalized.InstrumentRoot, (short)normalized.TargetHorizon, MarketConditionAssessmentHash.Serialize(normalized), MarketConditionAssessmentHash.Parameters(normalized), description ?? string.Empty, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertMarketConditionAssessmentDraft)}", ConfigurationDbSql.InsertMarketConditionAssessmentDraft)
            .SetParameters(preparedParameters10)
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
        var preparedParameters11 = new GetConfiguration(parameterSetId, version);
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetMarketConditionAssessment)}", ConfigurationDbSql.GetMarketConditionAssessment)
            .SetParameters(preparedParameters11)
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
        var preparedParameters12 = new GetEffectiveMarketConditionAssessment(marketProfileId, instrumentRoot, (short)targetHorizon, effectiveAtUtc);
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetEffectiveMarketConditionAssessment)}", ConfigurationDbSql.GetEffectiveMarketConditionAssessment)
            .SetParameters(preparedParameters12)
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
        var preparedParameters13 = new InsertVolatilitySeriesDefinition(definition.Identity.SeriesId, definition.Identity.MethodologyVersion, definition.DataIdentity.Environment, definition.Governance.EffectiveFromUtc.UtcDateTime, definition.Governance.EffectiveUntilUtc?.UtcDateTime, json, hash, definition.Governance.ApprovedConfigurationVersion, definition.Governance.Owner, definition.Governance.ApprovalEvidenceId);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertVolatilitySeriesDefinition)}", ConfigurationDbSql.InsertVolatilitySeriesDefinition)
            .SetParameters(preparedParameters13)
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
        var preparedParameters14 = new GetVolatilitySeriesDefinition(identity.SeriesId, identity.MethodologyVersion);
        return dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetVolatilitySeriesDefinition)}", ConfigurationDbSql.GetVolatilitySeriesDefinition)
            .SetParameters(preparedParameters14)
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
        var preparedParameters15 = new GetEffectiveVolatilitySeriesDefinition(environment, seriesId, effectiveAtUtc.UtcDateTime);
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetEffectiveVolatilitySeriesDefinition)}", ConfigurationDbSql.GetEffectiveVolatilitySeriesDefinition)
            .SetParameters(preparedParameters15)
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
        var preparedParameters16 = new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, checked((short)policy.SchemaVersion), 0, policy.Serialize(), policy.Hash(), description, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertStrategyPositionRiskDraft)}", ConfigurationDbSql.InsertStrategyPositionRiskDraft)
            .SetParameters(preparedParameters16)
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
        var preparedParameters17 = new GetConfiguration(id, version);
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetStrategyPositionRiskVersion)}", ConfigurationDbSql.GetStrategyPositionRiskVersion)
            .SetParameters(preparedParameters17)
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
        /// <param name="policy">The policy supplied for this transaction or query.</param>
    /// <param name="description">The description supplied for this transaction or query.</param>
    /// <param name="createdBy">The createdBy supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
    public async Task InsertRiskManagementDraftAsync(RiskParameterSet policy, string description, string createdBy, CancellationToken token = default)
    {
        var preparedParameters18 = new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0, policy.Serialize(), policy.Hash(), description, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertRiskManagementDraft)}", ConfigurationDbSql.InsertRiskManagementDraft)
            .SetParameters(preparedParameters18)
            .ExecuteCommandAsync(token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<string> InsertPreparedStrategyCatalogDraftAsync(PreparedStrategyCatalogDraft draft, int expectedPreviousVersion,
        string createdBy, CancellationToken cancellationToken = default)
    {
        var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        var hash = await this.InsertStrategyCatalogDraftCoreAsync(connection, transaction, draft, expectedPreviousVersion, createdBy, cancellationToken)
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

    /// <inheritdoc/>
    public async Task PublishPreparedStrategyCatalogAsync(StrategyCatalogPublicationProof proof, string expectedContentHash,
        string publishedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await this.PublishPreparedStrategyCatalogCoreAsync(connection, transaction, proof, expectedContentHash, publishedBy,
            cancellationToken)
                .ConfigureAwait(false);
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
        await this.RetireStrategyCatalogCoreAsync(connection, transaction, key, expectedContentHash, retiredAtUtc, retiredBy, cancellationToken)
            .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
    }



    /// <summary>Prepares a catalog row transition under the write fence without appending an event.</summary>
    /// <param name="enlisted">The transaction that will append the accepted fact.</param>
    /// <param name="request">The typed command already qualified by its owner.</param>
    /// <param name="principal">The accepting user or service.</param>
    /// <param name="stream">The owning catalog stream.</param>
    /// <param name="publication">The exact graph proof for a new publish, or null for other transitions.</param>
    /// <param name="draft">The prepared draft for a save operation.</param>
    /// <param name="token">Cancels the write.</param>
    /// <returns>The persisted predecessor and candidate images, or null for an accepted retry.</returns>
    public async Task<PreparedStrategyCatalogTransition?> PrepareStrategyCatalogTransitionAsync(EnlistedEventTransaction enlisted, CatalogCommandRequest request,
        string principal, string stream, StrategyCatalogPublicationProof? publication = null, PreparedStrategyCatalogDraft? draft = null,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(enlisted);
        var key = request.Operation == CatalogCommandOperation.SaveDraft ? request.Definition?.Key : request.Key;
        if (key is null || request.OperationId == Guid.Empty)
            throw new InvalidOperationException("Catalog operation and version identity are required.");

        var (connection, transaction) = enlisted.GetConnectionAndTransaction();
        await this.Execute(connection, transaction, ConfigurationDbSql.AcquireParameterWriterLock, token)
            .ConfigureAwait(false);
        await this.CatalogWriteLock(connection, transaction, token)
            .ConfigureAwait(false);
        var requestJson = StrategyCatalogJson.Write(request);
        var latest = await enlisted.QueryAsync(ConfigurationDbSql.CatalogSourceHead, (object? [])new CatalogSourceSelection(stream, request.OperationId).Bind(), MapToStrategyCatalogSourceHead, token)
            .ConfigureAwait(false);
        var previous = latest.Count == 0 ? null :
            EventLogMessagePackCodec.Shared.Deserialize(latest[0].Type, latest[0].EventId, latest[0].Payload) as StrategyCatalogSourceEvent
                ?? throw new InvalidDataException("Unexpected source event in a strategy catalog stream.");
        if (previous?.CommandId == request.OperationId)
        {
            if (!string.Equals(previous.RequestJson, requestJson, StringComparison.Ordinal) ||
                !string.Equals(previous.Principal, principal, StringComparison.Ordinal))
                throw new InvalidOperationException("Catalog operation identity was reused with different input.");
            return null;
        }

        var before = await this.ReadCatalog(connection, transaction, key, true, token)
            .ConfigureAwait(false);
        if (previous is not null && !before.MatchesCatalogVersion(previous.ReadAfterVersion()))
            throw new InvalidOperationException("The strategy catalog projection differs from its committed source state.");
        if (await enlisted.ScalarAsync(ConfigurationDbSql.ReadProjectedRevision, [], token)
            .ConfigureAwait(false) is null)
            throw new InvalidOperationException("PARAM.PROJECTION_LAG");

        // Reuse the existing transactional graph validation, then discard its tentative relational writes.
        await enlisted.ExecuteAsync(ConfigurationDbSql.StartAcceptance, [], token)
            .ConfigureAwait(false);
        switch (request.Operation)
        {
            case CatalogCommandOperation.SaveDraft when draft is not null && draft.Definition.Key == key:
                if (before is not null &&
                    string.Equals(before.ContentHash, CanonicalContentHash(draft.Definition), StringComparison.Ordinal) &&
                    string.Equals(before.CreatedBy, principal, StringComparison.Ordinal) &&
                    key.Version == request.ExpectedPreviousVersion + 1)
                    return null;
                await this.InsertStrategyCatalogDraftCoreAsync(connection, transaction, draft, request.ExpectedPreviousVersion, principal, token)
            .ConfigureAwait(false);
                break;
            case CatalogCommandOperation.Publish when request.ExpectedHash is { } publishHash && request.EffectiveUtc is { } effective:
                if (before is { Status: CatalogLifecycleStatus.Published } &&
                    string.Equals(before.ContentHash, publishHash, StringComparison.Ordinal) && before.EffectiveFromUtc == effective &&
                    string.Equals(before.PublishedBy, principal, StringComparison.Ordinal))
                    return null;
                if (publication is null || publication.Root != key || publication.EffectiveFromUtc != effective)
                    throw new InvalidOperationException("The prepared publication does not match the command.");
                await this.PublishPreparedStrategyCatalogCoreAsync(connection, transaction, publication, publishHash, principal, token)
                    .ConfigureAwait(false);
                break;
            case CatalogCommandOperation.Retire when request.ExpectedHash is { } retireHash && request.EffectiveUtc is { } retired:
                if (before is { Status: CatalogLifecycleStatus.Retired } &&
                    string.Equals(before.ContentHash, retireHash, StringComparison.Ordinal) && before.RetiredAtUtc == retired &&
                    string.Equals(before.RetiredBy, principal, StringComparison.Ordinal))
                    return null;
                await this.RetireStrategyCatalogCoreAsync(connection, transaction, key, retireHash, retired, principal, token)
            .ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException("Incomplete or unsupported catalog operation.");
        }
        var after = await this.ReadCatalog(connection, transaction, key, true, token)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The committed catalog version was not materialized.");
        await enlisted.ExecuteAsync(ConfigurationDbSql.RollbackAcceptance, [], token)
            .ConfigureAwait(false);
        return new(latest.Count == 0 ? 0 : latest[0].Version, requestJson, before, after);
    }

    internal static (long Version, long EventId, string Type, byte[] Payload) MapToStrategyCatalogSourceHead(System.Data.Common.DbDataReader reader)
        => (reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2), reader.GetFieldValue<byte[]>(3));

    /// <summary>Materializes an accepted source fact or a historical baseline without re-running command validation.</summary>
        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="enlisted">The enlisted supplied for this transaction or query.</param>
    /// <param name="source">The source supplied for this transaction or query.</param>
    /// <param name="rebuild">The rebuild supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
    public async Task<bool> ProjectStrategyCatalogSourceAsync(EnlistedEventTransaction enlisted, StrategyCatalogSourceEvent source,
        bool rebuild, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(enlisted);
        var (connection, transaction) = enlisted.GetConnectionAndTransaction();
        await this.Execute(connection, transaction, ConfigurationDbSql.AcquireParameterWriterLock, token)
            .ConfigureAwait(false);
        await this.CatalogWriteLock(connection, transaction, token)
            .ConfigureAwait(false);
        if (!rebuild && !await CompleteParameterProjectionAsync(enlisted, source.EventId, token)
            .ConfigureAwait(false))
            return false;

        var accepted = source.ReadAfterVersion();
        var predecessor = source.ReadBeforeVersion();
        var key = accepted.Definition.Key;
        var existing = await this.ReadCatalog(connection, transaction, key, true, token)
            .ConfigureAwait(false);
        if (existing.MatchesCatalogVersion(accepted))
            return false;
        if (existing is null && rebuild && predecessor is not null)
        {
            if (predecessor.Definition.Key != key)
                throw new InvalidDataException("Catalog projection predecessor belongs to a different version.");
            await this.MaterializeCatalogVersionAsync(connection, transaction, predecessor, null, token)
                .ConfigureAwait(false);
            existing = await this.ReadCatalog(connection, transaction, key, true, token)
                .ConfigureAwait(false);
        }
        if (!existing.MatchesCatalogVersion(predecessor))
            throw new InvalidDataException("Strategy catalog projection predecessor differs from the accepted source event.");
        await this.MaterializeCatalogVersionAsync(connection, transaction, accepted, existing, token)
            .ConfigureAwait(false);
        var materialized = await this.ReadCatalog(connection, transaction, key, true, token)
            .ConfigureAwait(false);
        if (!materialized.MatchesCatalogVersion(accepted))
            throw new InvalidDataException("Strategy catalog projection differs from the accepted source event.");
        return true;
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="enlisted">The enlisted supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
    /// <summary>Capture strategy catalog baseline.</summary>
    public async Task<IReadOnlyList<CatalogKey>> ReadLockedStrategyCatalogBaselineKeysAsync(EnlistedEventTransaction enlisted, CancellationToken token = default)
    {
        var (connection, transaction) = enlisted.GetConnectionAndTransaction();
        await this.CatalogWriteLock(connection, transaction, token)
            .ConfigureAwait(false);
        return await enlisted.QueryAsync(ConfigurationDbSql.CatalogVersionsWithoutSource, [], MapToStrategyCatalogKey, token)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<StoredStrategyCatalogDefinition?> GetLockedStrategyCatalogAsync(EnlistedEventTransaction enlisted, CatalogKey key, CancellationToken token = default)
    {
        var (connection, transaction) = enlisted.GetConnectionAndTransaction();
        return this.ReadCatalog(connection, transaction, key, true, token);
    }

    private static CatalogKey MapToStrategyCatalogKey(System.Data.Common.DbDataReader reader) =>
        new((StrategyCatalogKind)reader.GetInt16(0), reader.GetGuid(1), reader.GetInt32(2));

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="transaction">The transaction supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
    /// <summary>Reads strategy catalog source heads from the committed projection.</summary>
    public Task<IReadOnlyList<StrategyCatalogSourceEvent>> GetStrategyCatalogSourceHeadsAsync(
        IEnlistedPostgresTransaction transaction, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return transaction.QueryAsync(ConfigurationDbSql.CatalogSourceHeads, [], MapToGetStrategyCatalogSourceHeadsRow1, token);
    }

    /// <inheritdoc/>
    public async Task<StrategyCatalogQualificationFacts> ReadStrategyCatalogQualificationAsync(CatalogKey root, DateTime asOfUtc,
        bool allowRootDraft, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var facts = await this.ReadStrategyCatalogQualificationCoreAsync(connection, transaction, root, asOfUtc,
            allowRootDraft, lockRows: false, cancellationToken)
                .ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        return facts;
    }

    /// <inheritdoc/>
    public async Task<StrategyCatalogSourceEvent?> ReadCommittedStrategyCatalogOperationAsync(string stream, Guid operationId, CancellationToken token = default)
    {
        await using var connection = await this.OpenCatalogAsync(token)
            .ConfigureAwait(false);
        var parameters = new CatalogSourceSelection(stream, operationId);
        await using var command = this.Command(connection, null, ConfigurationDbSql.CatalogSourceHead, parameters);
        await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        if (!await reader.ReadAsync(token)
            .ConfigureAwait(false)) return null;
        var row = MapToStrategyCatalogSourceHead(reader);
        var source = EventLogMessagePackCodec.Shared.Deserialize(row.Type, row.EventId, row.Payload) as StrategyCatalogSourceEvent
            ?? throw new InvalidDataException("Unexpected source event in a strategy catalog stream.");
        return source.CommandId == operationId ? source : null;
    }

    internal static CatalogPipelinePolicyFact MapToCatalogPipelinePolicy(System.Data.Common.DbDataReader row,
        CatalogPipelineParameterKind kind, Guid id, int version)
        => new(kind, id, version, row.GetString(0), row.GetInt16(1),
            row.IsDBNull(2) ? null : row.GetDateTime(2), row.GetString(3), row.GetInt16(4));

    /// <summary>Inserts an immutable order-composition draft.</summary>
    /// <param name = "policy">The validated policy.</param>
    /// <param name = "description">The description.</param>
    /// <param name = "createdBy">The author.</param>
    /// <param name = "cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the insert operation.</returns>
    public async Task InsertSelectionConstructionDraftAsync(SelectionConstructionPolicy policy, string description, string createdBy, CancellationToken cancellationToken = default)
    {
        var preparedParameters19 = new InsertConfigurationDraft(policy.ParameterSetId, policy.Version, policy.SchemaVersion, 0, policy.Serialize(), policy.Hash(), description, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertOrderCompositionDraft)}", ConfigurationDbSql.InsertOrderCompositionDraft)
            .SetParameters(preparedParameters19)
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
        var preparedParameters20 = new InsertConfigurationDraft(activation.ParameterSetId, activation.Version, activation.SchemaVersion, 0, activation.Serialize(), activation.Hash(), description, BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertStrategyWorkflowDraft)}", ConfigurationDbSql.InsertStrategyWorkflowDraft)
            .SetParameters(preparedParameters20)
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
        /// <param name="id">The id supplied for this transaction or query.</param>
    /// <param name="version">The version supplied for this transaction or query.</param>
    /// <param name="hash">The hash supplied for this transaction or query.</param>
    /// <param name="at">The at supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
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
        var preparedParameters21 = new InsertConfigurationDraft(parameters.ParameterSetId, parameters.Version, parameters.SchemaVersion, 0, TradeSelectionPolicy.Serialize(parameters), TradeSelectionPolicy.Hash(parameters), description ?? "", BusinessClock.GetUtcNow().UtcDateTime, createdBy);
        await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.InsertTradeSelectionDraft)}", ConfigurationDbSql.InsertTradeSelectionDraft)
            .SetParameters(preparedParameters21)
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
        var preparedParameters22 = new GetConfiguration(id, version);
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetTradeSelectionVersion)}", ConfigurationDbSql.GetTradeSelectionVersion)
            .SetParameters(preparedParameters22)
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
        /// <param name="id">The id supplied for this transaction or query.</param>
    /// <param name="version">The version supplied for this transaction or query.</param>
    /// <param name="hash">The hash supplied for this transaction or query.</param>
    /// <param name="at">The at supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
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
        var preparedParameters23 = new GetConfiguration(id, version);
        return await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.GetSelectionPipelinePolicyFor.{kind}", kind.GetSelectionPipelinePolicyFor())
            .SetParameters(preparedParameters23)
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
        /// <param name="fact">The fact supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
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
        var preparedParameters24 = new GetLegacyParameterVersions(setId, version, offset);
        var rows = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetLegacyParameterVersions)}", ConfigurationDbSql.GetLegacyParameterVersions)
            .SetParameters(preparedParameters24)
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
        /// <param name="token">The token supplied for this transaction or query.</param>
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
        /// <param name="componentCode">The componentCode supplied for this transaction or query.</param>
    /// <param name="version">The version supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
    public async Task<ParameterSchemaDefinition?> GetParameterSchemaAsync(string componentCode, int version, CancellationToken token = default)
    {
        var preparedParameters25 = new GetParameterSchema(componentCode, version);
        var stored = await dbFactory.ConfigurationDb.Use($"{nameof(ConfigurationDbSql)}.{nameof(ConfigurationDbSql.GetParameterSchema)}", ConfigurationDbSql.GetParameterSchema)
            .SetParameters(preparedParameters25)
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
        /// <param name="fact">The fact supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
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
        /// <param name="componentCode">The componentCode supplied for this transaction or query.</param>
    /// <param name="setId">The setId supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
    /// <param name="limit">The limit supplied for this transaction or query.</param>
    /// <param name="afterName">The afterName supplied for this transaction or query.</param>
    /// <param name="afterSetId">The afterSetId supplied for this transaction or query.</param>
    /// <param name="afterVersion">The afterVersion supplied for this transaction or query.</param>
    public async Task<ParameterSetVersion[]> GetParameterSetsAsync(string componentCode, Guid? setId = null, CancellationToken token = default, int limit = 100, string afterName = "", Guid? afterSetId = null, int afterVersion = 0)
    {
        await using var connection=await this.OpenCatalogAsync(token)
            .ConfigureAwait(false);
        await using var transaction=await connection.BeginTransactionAsync(token)
            .ConfigureAwait(false);
        var session=new ConfigurationDbSession(connection,transaction);
        var page=await session.QueryAsync(ConfigurationDbSql.VersionPage, [componentCode, setId, afterName, afterSetId, afterVersion, limit], MapToGetParameterSetsRow2, token)
            .ConfigureAwait(false);
        var result=new List<ParameterSetVersion>();
        foreach(var row in page)
        {
            var metadata=await this.ReadCatalogProjectionAsync<ParameterVersionMetadata>(session,row.Id,row.Version,token)
            .ConfigureAwait(false)
                ??throw new InvalidDataException("Missing parameter metadata");
            var values=await GetTypedParameterValueAsync(session,row.Id,checked((int)row.Version),token)
            .ConfigureAwait(false)
                ??throw new InvalidDataException("Missing typed parameter values");
            result.Add(metadata.Restore(values) with{Name=row.Name,Description=row.Description,CatalogRevision=row.Revision});
        }
        await transaction.CommitAsync(token)
            .ConfigureAwait(false);
        return result.ToArray();
    }

    /// <summary>Projects a parameter-startup fact.</summary>
    /// <param name = "fact">The startup fact.</param>
    /// <param name = "token">The cancellation token.</param>
    /// <returns>A task representing the projection operation.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
        /// <param name="fact">The fact supplied for this transaction or query.</param>
    /// <param name="token">The token supplied for this transaction or query.</param>
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
        /// <param name="token">The token supplied for this transaction or query.</param>
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
    public Task InsertTypedParameterValueAsync(IEnlistedPostgresTransaction transaction, ParameterSetValue value, CancellationToken cancellationToken = default)=>transaction.WriteTypedValueAsync(value,cancellationToken);



    /// <summary>Loads concrete parameter values from typed columns and verifies the stored content fingerprint.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="setId">The exact parameter-set identifier.</param>
    /// <param name="version">The immutable version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The concrete value, or null if the version does not exist.</returns>
    public Task<ParameterSetValue?> GetTypedParameterValueAsync(IEnlistedPostgresTransaction transaction, Guid setId, int version, CancellationToken cancellationToken = default)=>transaction.ReadTypedValueAsync(setId,version,cancellationToken);



    /// <summary>Reads the infrastructure fence used to reject stale prepared commands.</summary>
        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    public async Task<long> GetParameterRevisionFenceAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var revision = await session.ScalarAsync(ConfigurationDbSql.ReadRevisionFence, [], cancellationToken)
            .ConfigureAwait(false);
        return revision is long value ? value : throw new InvalidDataException("PARAM.REVISION_FENCE_MISSING");
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Reads projected parameter revision from the committed projection.</summary>
    public async Task<long?> GetProjectedParameterRevisionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        return await session.ScalarAsync(ConfigurationDbSql.ReadProjectedRevision, [], cancellationToken)
            .ConfigureAwait(false) as long?;
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="transaction">The transaction supplied for this transaction or query.</param>
    /// <param name="eventId">The eventId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Mark parameter projection pending.</summary>
    public async Task MarkParameterProjectionPendingAsync(
        IEnlistedPostgresTransaction transaction, long eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        await transaction.ExecuteAsync(ConfigurationDbSql.MarkParameterProjectionPending, [eventId], cancellationToken)
            .ConfigureAwait(false);
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="transaction">The transaction supplied for this transaction or query.</param>
    /// <param name="eventId">The eventId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Complete parameter projection.</summary>
    public async Task<bool> CompleteParameterProjectionAsync(
        IEnlistedPostgresTransaction transaction, long eventId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        return await transaction.ExecuteAsync(ConfigurationDbSql.CompleteParameterProjection, [eventId], cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    /// <summary>Advances the evidence fence atomically with an accepted mutation.</summary>
        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="transaction">The transaction supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    public async Task AdvanceParameterRevisionFenceAsync(IEnlistedPostgresTransaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (await transaction.ExecuteAsync(ConfigurationDbSql.AdvanceRevisionFence, [], cancellationToken)
            .ConfigureAwait(false) != 1)
            throw new InvalidDataException("PARAM.REVISION_FENCE_MISSING");
    }

    /// <summary>Reads a bounded page of active run identities without relying on a registry actor cache.</summary>
        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="limit">The limit supplied for this transaction or query.</param>
    /// <param name="afterCreatedAtUtc">The afterCreatedAtUtc supplied for this transaction or query.</param>
    /// <param name="afterRunId">The afterRunId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    public async Task<Guid[]> GetParameterStartupRunIdsAsync(int limit, DateTime? afterCreatedAtUtc = null, Guid? afterRunId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var ids = await session.QueryAsync(ConfigurationDbSql.StartupPage, [afterCreatedAtUtc?.ToBinary(), afterRunId ?? Guid.Empty, Math.Clamp(limit, 1, 25)], MapToGetParameterStartupRunIdsRow3, cancellationToken)
            .ConfigureAwait(false);
        return ids.ToArray();
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="setId">The setId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Reads parameter set snapshot from the committed projection.</summary>
    public async Task<ParameterSetSnapshot> GetParameterSetSnapshotAsync(Guid setId, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var rows = await session.QueryAsync(ConfigurationDbSql.SetSnapshotVersions, [setId], MapToGetParameterSetSnapshotRow4, cancellationToken)
            .ConfigureAwait(false);
        var versions = new List<ParameterSetVersion>(rows.Count);
        foreach (var row in rows)
        {
            var metadata = await this.ReadCatalogProjectionAsync<ParameterVersionMetadata>(session, setId, row.Version, cancellationToken)
            .ConfigureAwait(false)
                ?? throw new InvalidDataException("Missing parameter metadata");
            var values = await GetTypedParameterValueAsync(session, setId, checked((int)row.Version), cancellationToken)
            .ConfigureAwait(false)
                ?? throw new InvalidDataException("Missing typed parameter values");
            versions.Add(metadata.Restore(values) with { Name = row.Name, Description = row.Description, CatalogRevision = row.Revision });
        }

        var references = versions.ToDictionary(item => item.Reference.Version, item => item.Reference);
        var operations = await session.QueryAsync(ConfigurationDbSql.OwnerOperations, ["set", setId], (reader) => MapToGetParameterSetSnapshotRow5(reader, references), cancellationToken)
            .ConfigureAwait(false);
        var audit = new List<ParameterAuditEntry>();
        foreach (var operation in operations)
        {
            if (await GetParameterAuditProjectionAsync(session, operation.OperationId, cancellationToken)
            .ConfigureAwait(false) is { } entry)
                audit.Add(entry);
        }
        return new(setId, rows.Count == 0 ? 0 : rows[0].Revision, versions.ToArray(), operations.ToArray(), audit.ToArray());
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="identity">The identity supplied for this transaction or query.</param>
    /// <param name="scope">The scope supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Reads parameter assignment snapshot from the committed projection.</summary>
    public async Task<ParameterAssignmentSnapshot> GetParameterAssignmentSnapshotAsync(
        ParameterAssignmentEntityId identity, ParameterAssignmentScope scope, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var operations = await session.QueryAsync(ConfigurationDbSql.OwnerOperations, ["assignment", identity.AssignmentId], MapToGetParameterAssignmentSnapshotRow6, cancellationToken)
            .ConfigureAwait(false);
        if (operations.Count == 0)
            return new(identity, scope, 0, null, []);

        var revision = operations[^1].Revision;
        var assignment = await GetParameterAssignmentProjectionAsync(session, identity.AssignmentId, revision, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidDataException("PARAM.ASSIGNMENT_PROJECTION_MISSING");
        var audit = new List<ParameterAuditEntry>();
        foreach (var operation in operations)
        {
            if (await GetParameterAuditProjectionAsync(session, operation.Id, cancellationToken)
            .ConfigureAwait(false) is { } entry)
                audit.Add(entry);
        }
        return new(identity, scope, revision, assignment, audit.ToArray());
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="assignmentIds">The assignmentIds supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Reads parameter startup inputs from the committed projection.</summary>
    public async Task<ParameterStartupInputsReadModel> GetParameterStartupInputsAsync(
        Guid[] assignmentIds, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var assignments = new List<ParameterAssignmentRevision>(assignmentIds.Length);
        foreach (var id in assignmentIds)
        {
            var revision = Convert.ToInt64(await session.ScalarAsync(ConfigurationDbSql.ReadParameterCatalogRevision, ["assignment", id], cancellationToken)
            .ConfigureAwait(false));
            if (revision > 0)
            {
                var assignment = await GetParameterAssignmentProjectionAsync(session, id, revision, cancellationToken)
            .ConfigureAwait(false)
                    ?? throw new InvalidDataException("PARAM.ASSIGNMENT_PROJECTION_MISSING");
                assignments.Add(assignment);
            }
        }
        var versions = new List<ParameterSetVersion>();
        foreach (var reference in assignments.Where(item => item.Enabled).Select(item => item.Reference).Distinct())
        {
            var metadata = await this.ReadCatalogProjectionAsync<ParameterVersionMetadata>(session, reference.SetId, reference.Version, cancellationToken)
            .ConfigureAwait(false)
                ?? throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
            var values = await GetTypedParameterValueAsync(session, reference.SetId, reference.Version, cancellationToken)
            .ConfigureAwait(false)
                ?? throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
            versions.Add(metadata.Restore(values));
        }
        return new(assignments.ToArray(), versions.ToArray());
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="runId">The runId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Has parameter startup projection.</summary>
    public async Task<bool> HasParameterStartupProjectionAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        return await session.ScalarAsync(ConfigurationDbSql.HasStartup, [runId], cancellationToken)
            .ConfigureAwait(false) is true;
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="runId">The runId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Reads active parameter startup from the committed projection.</summary>
    public async Task<ParameterStartupReadModel> GetActiveParameterStartupAsync(
        Guid runId, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var active = await session.QueryAsync(ConfigurationDbSql.ActiveStartup, [runId], MapToGetActiveParameterStartupRow7, cancellationToken)
            .ConfigureAwait(false);
        if (active.Count == 0)
            return new(null, null);

        var run = await GetParameterStartupProjectionAsync(session, runId, cancellationToken)
            .ConfigureAwait(false);
        var report = await GetParameterStartupReportProjectionAsync(session, runId, cancellationToken)
            .ConfigureAwait(false);
        return new(run, report);
    }

        /// <returns>The persisted projection result or completion of the caller-owned transaction.</returns>
    /// <param name="limit">The limit supplied for this transaction or query.</param>
    /// <param name="afterCreatedAtUtc">The afterCreatedAtUtc supplied for this transaction or query.</param>
    /// <param name="afterRunId">The afterRunId supplied for this transaction or query.</param>
    /// <param name="cancellationToken">The cancellationToken supplied for this transaction or query.</param>
    /// <summary>Reads active parameter startup page from the committed projection.</summary>
    public async Task<ParameterStartupRun[]> GetActiveParameterStartupPageAsync(
        int limit, DateTime? afterCreatedAtUtc, Guid? afterRunId, CancellationToken cancellationToken = default)
    {
        await using var connection = await this.OpenCatalogAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);
        var session = new ConfigurationDbSession(connection, transaction);
        var ids = await session.QueryAsync(ConfigurationDbSql.StartupPage, [afterCreatedAtUtc?.ToBinary(), afterRunId ?? Guid.Empty, Math.Clamp(limit, 1, 25)], MapToGetActiveParameterStartupPageRow8, cancellationToken)
            .ConfigureAwait(false);
        var runs = new List<ParameterStartupRun>(ids.Count);
        foreach (var id in ids)
        {
            var run = await GetParameterStartupProjectionAsync(session, id, cancellationToken)
            .ConfigureAwait(false);
            runs.Add(run ?? throw new InvalidDataException("PARAM.STARTUP_PROJECTION_MISSING"));
        }
        return runs.ToArray();
    }

    /// <summary>Reads one immutable assignment revision and verifies all scalar and child rows.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="assignmentId">The assignment identity.</param>
    /// <param name="revision">The exact accepted revision.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The typed revision, or null when it does not exist.</returns>
    public Task<ParameterAssignmentRevision?> GetParameterAssignmentProjectionAsync(IEnlistedPostgresTransaction transaction, Guid assignmentId, long revision, CancellationToken cancellationToken=default)
        => this.ReadCatalogProjectionAsync<ParameterAssignmentRevision>(transaction,assignmentId,revision,cancellationToken);

    /// <summary>Reads and verifies the typed before/after audit values for an operation.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="operationId">The accepted operation identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The immutable audit entry, or null when absent.</returns>
    public Task<ParameterAuditEntry?> GetParameterAuditProjectionAsync(IEnlistedPostgresTransaction transaction, Guid operationId, CancellationToken cancellationToken=default)
        => this.ReadCatalogProjectionAsync<ParameterAuditEntry>(transaction,operationId,1,cancellationToken);

    /// <summary>Loads a frozen startup projection, resolving each exact version from typed value tables.</summary>
    /// <param name="transaction">The caller-owned PostgreSQL transaction.</param>
    /// <param name="runId">The frozen startup generation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The verified startup generation, or null when absent.</returns>
    public async Task<ParameterStartupRun?> GetParameterStartupProjectionAsync(IEnlistedPostgresTransaction transaction, Guid runId, CancellationToken cancellationToken=default)
    {
        var stored=await this.ReadCatalogProjectionAsync<ParameterStartupProjection>(transaction,runId,1,cancellationToken)
            .ConfigureAwait(false);
        if(stored is null)return null;
        var versions=new List<ParameterSetVersion>();
        foreach(var metadata in stored.Versions)
        {
            var values=await GetTypedParameterValueAsync(transaction,metadata.Reference.SetId,metadata.Reference.Version,cancellationToken)
            .ConfigureAwait(false)
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
        => this.ReadCatalogProjectionAsync<ParameterSignalStartupReport>(transaction,runId,1,cancellationToken);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogSourceEvent MapToGetStrategyCatalogSourceHeadsRow1(global::System.Data.Common.DbDataReader reader) =>             EventLogMessagePackCodec.Shared.Deserialize(reader.GetString(1), reader.GetInt64(0), (byte[])reader.GetValue(2))
                as StrategyCatalogSourceEvent ?? throw new InvalidDataException("Unexpected strategy catalog stream head.");

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static (global::System.Guid Id, long Version, string Name, string Description, long Revision) MapToGetParameterSetsRow2(global::System.Data.Common.DbDataReader reader) => (Id:reader.GetGuid(0),Version:reader.GetInt64(1),Name:reader.GetString(2),Description:reader.GetString(3),Revision:reader.GetInt64(4));

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::System.Guid MapToGetParameterStartupRunIdsRow3(global::System.Data.Common.DbDataReader reader) => reader.GetGuid(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static (long Version, string Name, string Description, long Revision) MapToGetParameterSetSnapshotRow4(global::System.Data.Common.DbDataReader reader) => (Version: reader.GetInt64(0), Name: reader.GetString(1), Description: reader.GetString(2), Revision: reader.GetInt64(3));

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterOperationReceipt MapToGetParameterSetSnapshotRow5(global::System.Data.Common.DbDataReader reader, global::System.Collections.Generic.Dictionary<int, global::TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterVersionRef> references) => new ParameterOperationReceipt(reader.GetGuid(0), reader.GetString(2), reader.GetInt64(1), references[reader.GetInt32(3)]);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static (global::System.Guid Id, long Revision) MapToGetParameterAssignmentSnapshotRow6(global::System.Data.Common.DbDataReader reader) => (Id: reader.GetGuid(0), Revision: reader.GetInt64(1));

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::System.Guid MapToGetActiveParameterStartupRow7(global::System.Data.Common.DbDataReader reader) => reader.GetGuid(0);

    /// <summary>Maps the selected database columns in their query ordinal order.</summary>
    private static global::System.Guid MapToGetActiveParameterStartupPageRow8(global::System.Data.Common.DbDataReader reader) => reader.GetGuid(0);
internal static (string Component, int Schema, string Hash, string Codec) MapToReadTypedValueRow1(global::System.Data.Common.DbDataReader reader) => (Component: reader.GetString(0), Schema: reader.GetInt32(1), Hash: reader.GetString(2), Codec: reader.GetString(3));
internal static global::TomasAI.IFM.Application.Storage.ConfigurationDb.ParameterValueMapping.Row MapToReadTypedValueRow2(global::System.Data.Common.DbDataReader reader, global::TomasAI.IFM.Application.Storage.ConfigurationDb.ParameterValueMapping.Table table) => new ParameterValueMapping.Row(table, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2),
                    Enumerable.Range(0, table.Columns.Count).Select(index => reader.IsDBNull(index + 3) ? null : reader.GetValue(index + 3)).ToArray());
    internal static ParameterValueMapping.Row MapToParameterProjectionRow(System.Data.Common.DbDataReader reader, ParameterValueMapping.Table table) =>
        new(table, reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetInt32(2),
            Enumerable.Range(0, table.Columns.Count).Select(i => reader.IsDBNull(i + 3) ? null : reader.GetValue(i + 3)).ToArray());

}
