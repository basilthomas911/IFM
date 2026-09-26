using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>
/// Defines asynchronous Configuration database queries.
/// </summary>
public interface IConfigurationDbReadContext
{
    /// <summary>Gets one exact immutable Regime Discovery version.</summary>
    /// <param name="parameterSetId">The parameter-set identifier.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedRegimeDiscoveryParameterSet?> GetRegimeDiscoveryAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);

    /// <summary>Gets one exact immutable Market Condition version.</summary>
    /// <param name="parameterSetId">The parameter-set identifier.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedMarketConditionParameterSet?> GetMarketConditionAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective published Regime Discovery version.</summary>
    /// <param name="effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name="targetHorizon">The target horizon.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective version, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedRegimeDiscoveryParameterSet?> GetEffectiveRegimeDiscoveryAsync(DateTime effectiveAtUtc, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective published Market Condition version.</summary>
    /// <param name="effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="instrumentRoot">The instrument root.</param>
    /// <param name="targetHorizon">The target horizon.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective version, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedMarketConditionParameterSet?> GetEffectiveMarketConditionAsync(DateTime effectiveAtUtc, int fundId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);

    /// <summary>Gets lookup definitions for a group.</summary>
    /// <param name="groupName">The lookup group name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ordered lookup definitions.</returns>
    Task<LookupDefinitionReadModel[]> GetLookupDefinitionsAsync(string groupName, CancellationToken cancellationToken = default);

    /// <summary>Gets one exact strategy-catalog definition.</summary>
    /// <param name="key">The catalog key.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stored definition, or <see langword="null"/> when it does not exist.</returns>
    Task<StoredStrategyCatalogDefinition?> GetStrategyCatalogAsync(CatalogKey key, CancellationToken cancellationToken = default);

    /// <summary>Gets the latest strategy-catalog versions ordered by stable code.</summary>
    /// <param name="kind">The catalog kind.</param>
    /// <param name="limit">The maximum result count.</param>
    /// <param name="afterCode">The exclusive stable-code cursor.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The strategy-catalog summaries.</returns>
    Task<IReadOnlyList<StrategyCatalogSummary>> GetStrategyCatalogsAsync(StrategyCatalogKind kind, int limit = 50, string? afterCode = null, CancellationToken cancellationToken = default);

    /// <summary>Gets a validated published strategy deployment snapshot.</summary>
    /// <param name="deployment">The deployment catalog key.</param>
    /// <param name="asOfUtc">The UTC selection timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validated deployment snapshot.</returns>
    Task<StrategyCatalogSnapshot> GetPublishedStrategyDeploymentAsync(CatalogKey deployment, DateTime asOfUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets one exact volatility-series definition.</summary>
    /// <param name="identity">The definition identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved definition, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedVolatilitySeriesDefinition?> GetVolatilitySeriesDefinitionAsync(VolatilitySeriesIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective volatility-series definition.</summary>
    /// <param name="environment">The market-data environment.</param>
    /// <param name="seriesId">The series identifier.</param>
    /// <param name="effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective definition, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedVolatilitySeriesDefinition?> GetEffectiveVolatilitySeriesDefinitionAsync(string environment, string seriesId, DateTimeOffset effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets an effective Trade Selection activation.</summary>
    /// <param name="id">The activation identifier.</param>
    /// <param name="version">The activation version.</param>
    /// <param name="hash">The expected payload digest.</param>
    /// <param name="effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective activation.</returns>
    Task<TradeSelectionActivation> GetEffectiveTradeSelectionActivationAsync(Guid id, int version, string hash, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets one exact Trade Selection parameter-set version.</summary>
    /// <param name="id">The parameter-set identifier.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved version, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedTradeSelectionParameterSet?> GetTradeSelectionVersionAsync(Guid id, int version, CancellationToken cancellationToken = default);

    /// <summary>Gets an effective Trade Selection parameter-set version.</summary>
    /// <param name="id">The parameter-set identifier.</param>
    /// <param name="version">The version number.</param>
    /// <param name="hash">The expected payload digest.</param>
    /// <param name="effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective parameter-set version.</returns>
    Task<ResolvedTradeSelectionParameterSet> GetEffectiveTradeSelectionVersionAsync(Guid id, int version, string hash, DateTime effectiveAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Gets one exact pipeline policy snapshot.</summary>
    /// <param name="kind">The pipeline parameter kind.</param>
    /// <param name="id">The parameter-set identifier.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policy snapshot, or <see langword="null"/> when it does not exist.</returns>
    Task<SelectionPipelinePolicySnapshot?> GetSelectionPipelinePolicyAsync(CatalogPipelineParameterKind kind, Guid id, int version, CancellationToken cancellationToken = default);

    /// <summary>Gets one exact Market Condition Assessment parameter set.</summary>
    /// <param name="parameterSetId">The parameter-set identifier.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resolved parameter set, or <see langword="null"/> when it does not exist.</returns>
    Task<ResolvedMarketConditionAssessmentParameterSet?> GetMarketConditionAssessmentAsync(Guid parameterSetId, int version, CancellationToken cancellationToken = default);

    /// <summary>Gets the effective Market Condition Assessment parameter set.</summary>
    /// <param name="effectiveAtUtc">The UTC selection timestamp.</param>
    /// <param name="marketProfileId">The market-profile identifier.</param>
    /// <param name="instrumentRoot">The instrument root.</param>
    /// <param name="targetHorizon">The target horizon.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The effective parameter set, or <see langword="null"/> when none exists.</returns>
    Task<ResolvedMarketConditionAssessmentParameterSet?> GetEffectiveMarketConditionAssessmentAsync(DateTime effectiveAtUtc, string marketProfileId, string instrumentRoot, TimeFrameType targetHorizon, CancellationToken cancellationToken = default);

    /// <summary>Gets legacy parameter versions.</summary>
    /// <param name="setId">The optional legacy set identifier.</param>
    /// <param name="version">The optional exact version, or zero for all versions.</param>
    /// <param name="offset">The result offset.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The legacy parameter versions.</returns>
    Task<ParameterLegacyVersion[]> GetLegacyParameterVersionsAsync(Guid? setId = null, int version = 0, int offset = 0, CancellationToken cancellationToken = default);

    /// <summary>Gets registered parameter components.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The registered parameter components.</returns>
    Task<ParameterComponentSummary[]> GetParameterComponentsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a parameter schema definition.</summary>
    /// <param name="componentCode">The component code.</param>
    /// <param name="version">The schema version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The schema definition, or <see langword="null"/> when it does not exist.</returns>
    Task<ParameterSchemaDefinition?> GetParameterSchemaAsync(string componentCode, int version, CancellationToken cancellationToken = default);

    /// <summary>Gets parameter-set versions using stable cursor pagination.</summary>
    /// <param name="componentCode">The component code.</param>
    /// <param name="setId">The optional parameter-set identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="limit">The maximum result count.</param>
    /// <param name="afterName">The exclusive name cursor.</param>
    /// <param name="afterSetId">The exclusive set-identifier cursor.</param>
    /// <param name="afterVersion">The exclusive version cursor.</param>
    /// <returns>The parameter-set versions.</returns>
    Task<ParameterSetVersion[]> GetParameterSetsAsync(string componentCode, Guid? setId = null, CancellationToken cancellationToken = default, int limit = 100, string afterName = "", Guid? afterSetId = null, int afterVersion = 0);
}
