using System.Globalization;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaSql;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
/// <summary>Creates every ConfigurationDb PostgreSQL schema object, including tables, indexes, constraints, functions, triggers and seed definitions.</summary>
public sealed class ConfigurationSchemaDb : SchemaDbContext<ConfigurationSchemaDb>
{
    /// <summary>Initializes the complete schema catalog with lookup defaults.</summary>
    /// <param name = "connectionSettings">The named PostgreSQL connection settings.</param>
    /// <param name = "logger">The schema-provider logger.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6672", Justification = "SchemaDbContext shares the storage provider logger category.")]
    public ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : this(connectionSettings, logger, true)
    {
    }

    /// <summary>Initializes the complete schema catalog without lookup default rows for isolated rebuilds or offline migration.</summary>
    /// <param name = "connectionSettings">The named PostgreSQL connection settings.</param>
    /// <param name = "logger">The schema-provider logger.</param>
    /// <returns>The schema executor with lookup default insertion disabled.</returns>
    public static ConfigurationSchemaDb WithoutLookupDefaults(IDbConnectionSettings connectionSettings, ILogger logger)
    {
        return new(connectionSettings, logger as ILogger<DbProvider> ?? new ProviderCategoryLogger(logger), false);
    }

    // Keep provider logging details inside the Application implementation.
    sealed class ProviderCategoryLogger(ILogger inner) : ILogger<DbProvider>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);
        public bool IsEnabled(LogLevel level) => inner.IsEnabled(level);
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => inner.Log(level, id, state, exception, formatter);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6672", Justification = "SchemaDbContext shares the storage provider logger category.")]
    private ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger, bool seedLookupDefaults) : base((connectionSettings ?? throw new ArgumentNullException(nameof(connectionSettings)))[ConfigurationDbContext.ConfigurationDbConnection], logger)
    {
        definitions = GetDefinitions(seedLookupDefaults);
    }

    readonly SchemaObjectDefinition[] definitions;
    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => definitions;

    /// <summary>Returns the ConfigurationDb schema objects in dependency order.</summary>
    internal static SchemaObjectDefinition[] GetDefinitions(bool seedLookupDefaults = true)
        =>
        [
            new("reference_configuration", CreateReferenceConfiguration, DropReferenceConfiguration),
            new("intrinsic_time_strategy_workflow_parameter_set", CreateIntrinsicTimeStrategyWorkflowParameterSet, DropIntrinsicTimeStrategyWorkflowParameterSet),
            new("regime_discovery_parameter_set", CreateRegimeDiscoveryParameterSet, DropRegimeDiscoveryParameterSet),
            new("market_condition_parameter_set", CreateMarketConditionParameterSet, DropMarketConditionParameterSet),
            new("trade_selection_parameter_set", CreateTradeSelectionParameterSet, DropTradeSelectionParameterSet),
            new("order_composition_parameter_set", CreateOrderCompositionParameterSet, DropOrderCompositionParameterSet),
            new("risk_management_parameter_set", CreateRiskManagementParameterSet, DropRiskManagementParameterSet),
            new("strategy_position_risk_parameter_set", CreateStrategyPositionRiskParameterSet, DropStrategyPositionRiskParameterSet),
            new("trade_selection_parameter_set_lifecycle_guard", CreateTradeSelectionParameterSetLifecycleGuard, DropTradeSelectionParameterSetLifecycleGuard),
            new("intrinsic_time_strategy_workflow_parameter_set_lifecycle_guard", CreateIntrinsicTimeStrategyWorkflowParameterSetLifecycleGuard, DropIntrinsicTimeStrategyWorkflowParameterSetLifecycleGuard),
            new("order_composition_parameter_set_lifecycle_guard", CreateOrderCompositionParameterSetLifecycleGuard, DropOrderCompositionParameterSetLifecycleGuard),
            new("strategy_position_risk_parameter_set_lifecycle_guard", CreateStrategyPositionRiskParameterSetLifecycleGuard, DropStrategyPositionRiskParameterSetLifecycleGuard),
            new("ix_market_condition_parameter_set_effective", CreateIxMarketConditionParameterSetEffective, DropIxMarketConditionParameterSetEffective),
            new("market_condition_parameter_set_lifecycle_constraints", CreateMarketConditionParameterSetLifecycleConstraints, DropMarketConditionParameterSetLifecycleConstraints),
            new("market_condition_parameter_set_lifecycle_guard", CreateMarketConditionParameterSetLifecycleGuard, DropMarketConditionParameterSetLifecycleGuard),
            new("market_condition_assessment_parameter_set", CreateMarketConditionAssessmentParameterSet, DropMarketConditionAssessmentParameterSet),
            new("volatility_series_definition", CreateVolatilitySeriesDefinition, DropVolatilitySeriesDefinition),
            new("lookup_definition", seedLookupDefaults ? JoinSql(CreateLookupDefinition, SeedLookupDefaults) : CreateLookupDefinition, DropLookupDefinition),
            new("strategy_catalog", CreateStrategyCatalog, DropStrategyCatalog),
            new("parameter_registry", JoinSql(CreateParameterRegistry, RegisteredParameterSchemaSeeds()), DropParameterRegistry),
            new("parameter_sets", CreateParameterSets, DropParameterSets),
            new("parameter_value_version", CreateParameterValueVersion, DropParameterValueVersion),
            new("parameter_regime_discovery", CreateParameterRegimeDiscovery, DropParameterRegimeDiscovery),
            new("parameter_regime_discovery_horizon", CreateParameterRegimeDiscoveryHorizon, DropParameterRegimeDiscoveryHorizon),
            new("parameter_regime_discovery_horizon_time_frames", CreateParameterRegimeDiscoveryHorizonTimeFrames, DropParameterRegimeDiscoveryHorizonTimeFrames),
            new("parameter_regime_discovery_trend", CreateParameterRegimeDiscoveryTrend, DropParameterRegimeDiscoveryTrend),
            new("parameter_regime_discovery_volatility", CreateParameterRegimeDiscoveryVolatility, DropParameterRegimeDiscoveryVolatility),
            new("parameter_regime_discovery_market_structure", CreateParameterRegimeDiscoveryMarketStructure, DropParameterRegimeDiscoveryMarketStructure),
            new("parameter_regime_discovery_fusion", CreateParameterRegimeDiscoveryFusion, DropParameterRegimeDiscoveryFusion),
            new("parameter_regime_discovery_freshness", CreateParameterRegimeDiscoveryFreshness, DropParameterRegimeDiscoveryFreshness),
            new("parameter_regime_discovery_data_quality", CreateParameterRegimeDiscoveryDataQuality, DropParameterRegimeDiscoveryDataQuality),
            new("parameter_regime_discovery_data_quality_supported__450c80e543a1", CreateParameterRegimeDiscoveryDataQualitySupported450c80e543a1, DropParameterRegimeDiscoveryDataQualitySupported450c80e543a1),
            new("parameter_regime_discovery_data_quality_approved_c_65f3d75001b7", CreateParameterRegimeDiscoveryDataQualityApprovedC65f3d75001b7, DropParameterRegimeDiscoveryDataQualityApprovedC65f3d75001b7),
            new("parameter_regime_discovery_signal_requirements", CreateParameterRegimeDiscoverySignalRequirements, DropParameterRegimeDiscoverySignalRequirements),
            new("parameter_regime_discovery_signal_metrics", CreateParameterRegimeDiscoverySignalMetrics, DropParameterRegimeDiscoverySignalMetrics),
            new("parameter_regime_discovery_observation_metrics", CreateParameterRegimeDiscoveryObservationMetrics, DropParameterRegimeDiscoveryObservationMetrics),
            new("parameter_futures_iti_signal", CreateParameterFuturesItiSignal, DropParameterFuturesItiSignal),
            new("parameter_futures_iti_signal_default_trading_days", CreateParameterFuturesItiSignalDefaultTradingDays, DropParameterFuturesItiSignalDefaultTradingDays),
            new("parameter_option_volatility_series", CreateParameterOptionVolatilitySeries, DropParameterOptionVolatilitySeries),
            new("parameter_option_volatility_series_eligible_product_families", CreateParameterOptionVolatilitySeriesEligibleProductFamilies, DropParameterOptionVolatilitySeriesEligibleProductFamilies),
            new("parameter_option_volatility_series_pricers", CreateParameterOptionVolatilitySeriesPricers, DropParameterOptionVolatilitySeriesPricers),
            new("parameter_option_volatility_series_pricers_pricer_versions", CreateParameterOptionVolatilitySeriesPricersPricerVersions, DropParameterOptionVolatilitySeriesPricersPricerVersions),
            new("parameter_option_volatility_consumer_rules", CreateParameterOptionVolatilityConsumerRules, DropParameterOptionVolatilityConsumerRules),
            new("parameter_option_volatility_consumer_rules_numerical_rules", CreateParameterOptionVolatilityConsumerRulesNumericalRules, DropParameterOptionVolatilityConsumerRulesNumericalRules),
            new("parameter_option_volatility_retention", CreateParameterOptionVolatilityRetention, DropParameterOptionVolatilityRetention),
            new("parameter_option_volatility_retention_historical_sources", CreateParameterOptionVolatilityRetentionHistoricalSources, DropParameterOptionVolatilityRetentionHistoricalSources),
            new("parameter_option_volatility_retention_isolated_environments", CreateParameterOptionVolatilityRetentionIsolatedEnvironments, DropParameterOptionVolatilityRetentionIsolatedEnvironments),
            new("parameter_iron_condor_market_selection", CreateParameterIronCondorMarketSelection, DropParameterIronCondorMarketSelection),
            new("parameter_iron_condor_market_selection_symbols", CreateParameterIronCondorMarketSelectionSymbols, DropParameterIronCondorMarketSelectionSymbols),
            new("parameter_vertical_spread_market_selection", CreateParameterVerticalSpreadMarketSelection, DropParameterVerticalSpreadMarketSelection),
            new("parameter_vertical_spread_market_selection_symbols", CreateParameterVerticalSpreadMarketSelectionSymbols, DropParameterVerticalSpreadMarketSelectionSymbols),
            new("parameter_strategy_option_chain", CreateParameterStrategyOptionChain, DropParameterStrategyOptionChain),
            new("parameter_strategy_option_chain_bias_rows", CreateParameterStrategyOptionChainBiasRows, DropParameterStrategyOptionChainBiasRows),
            new("parameter_strategy_option_chain_bias_rows_put_delta", CreateParameterStrategyOptionChainBiasRowsPutDelta, DropParameterStrategyOptionChainBiasRowsPutDelta),
            new("parameter_strategy_option_chain_bias_rows_call_delta", CreateParameterStrategyOptionChainBiasRowsCallDelta, DropParameterStrategyOptionChainBiasRowsCallDelta),
            new("parameter_strategy_option_chain_bias_rows_put_wing_widths", CreateParameterStrategyOptionChainBiasRowsPutWingWidths, DropParameterStrategyOptionChainBiasRowsPutWingWidths),
            new("parameter_strategy_option_chain_bias_rows_call_wing_widths", CreateParameterStrategyOptionChainBiasRowsCallWingWidths, DropParameterStrategyOptionChainBiasRowsCallWingWidths),
            new("parameter_revision_fence", CreateParameterRevisionFence, DropParameterRevisionFence),
            new("parameter_projection_pending", CreateParameterProjectionPending, DropParameterProjectionPending),
            new("parameter_projection_record", CreateParameterProjectionRecord, DropParameterProjectionRecord),
            new("parameter_catalog_version", CreateParameterCatalogVersion, DropParameterCatalogVersion),
            new("parameter_catalog_version_reference", CreateParameterCatalogVersionReference, DropParameterCatalogVersionReference),
            new("parameter_catalog_version_legacy_source", CreateParameterCatalogVersionLegacySource, DropParameterCatalogVersionLegacySource),
            new("parameter_catalog_audit", CreateParameterCatalogAudit, DropParameterCatalogAudit),
            new("parameter_catalog_audit_before", CreateParameterCatalogAuditBefore, DropParameterCatalogAuditBefore),
            new("parameter_catalog_audit_before_reference", CreateParameterCatalogAuditBeforeReference, DropParameterCatalogAuditBeforeReference),
            new("parameter_catalog_audit_before_assignment", CreateParameterCatalogAuditBeforeAssignment, DropParameterCatalogAuditBeforeAssignment),
            new("parameter_catalog_audit_before_assignment_scope", CreateParameterCatalogAuditBeforeAssignmentScope, DropParameterCatalogAuditBeforeAssignmentScope),
            new("parameter_catalog_audit_before_assignment_reference", CreateParameterCatalogAuditBeforeAssignmentReference, DropParameterCatalogAuditBeforeAssignmentReference),
            new("parameter_catalog_audit_after", CreateParameterCatalogAuditAfter, DropParameterCatalogAuditAfter),
            new("parameter_catalog_audit_after_reference", CreateParameterCatalogAuditAfterReference, DropParameterCatalogAuditAfterReference),
            new("parameter_catalog_audit_after_assignment", CreateParameterCatalogAuditAfterAssignment, DropParameterCatalogAuditAfterAssignment),
            new("parameter_catalog_audit_after_assignment_scope", CreateParameterCatalogAuditAfterAssignmentScope, DropParameterCatalogAuditAfterAssignmentScope),
            new("parameter_catalog_audit_after_assignment_reference", CreateParameterCatalogAuditAfterAssignmentReference, DropParameterCatalogAuditAfterAssignmentReference),
            new("parameter_catalog_assignment", CreateParameterCatalogAssignment, DropParameterCatalogAssignment),
            new("parameter_catalog_assignment_scope", CreateParameterCatalogAssignmentScope, DropParameterCatalogAssignmentScope),
            new("parameter_catalog_assignment_reference", CreateParameterCatalogAssignmentReference, DropParameterCatalogAssignmentReference),
            new("parameter_catalog_startup", CreateParameterCatalogStartup, DropParameterCatalogStartup),
            new("parameter_catalog_startup_scopes", CreateParameterCatalogStartupScopes, DropParameterCatalogStartupScopes),
            new("parameter_catalog_startup_scopes_scope", CreateParameterCatalogStartupScopesScope, DropParameterCatalogStartupScopesScope),
            new("parameter_catalog_startup_scopes_reference", CreateParameterCatalogStartupScopesReference, DropParameterCatalogStartupScopesReference),
            new("parameter_catalog_startup_versions", CreateParameterCatalogStartupVersions, DropParameterCatalogStartupVersions),
            new("parameter_catalog_startup_versions_reference", CreateParameterCatalogStartupVersionsReference, DropParameterCatalogStartupVersionsReference),
            new("parameter_catalog_startup_versions_legacy_source", CreateParameterCatalogStartupVersionsLegacySource, DropParameterCatalogStartupVersionsLegacySource),
            new("parameter_catalog_startup_plan", CreateParameterCatalogStartupPlan, DropParameterCatalogStartupPlan),
            new("parameter_catalog_startup_plan_steps", CreateParameterCatalogStartupPlanSteps, DropParameterCatalogStartupPlanSteps),
            new("parameter_catalog_startup_plan_steps_key", CreateParameterCatalogStartupPlanStepsKey, DropParameterCatalogStartupPlanStepsKey),
            new("parameter_catalog_startup_plan_steps_consumers", CreateParameterCatalogStartupPlanStepsConsumers, DropParameterCatalogStartupPlanStepsConsumers),
            new("parameter_catalog_startup_plan_steps_consumers_key", CreateParameterCatalogStartupPlanStepsConsumersKey, DropParameterCatalogStartupPlanStepsConsumersKey),
            new("parameter_catalog_startup_plan_issues", CreateParameterCatalogStartupPlanIssues, DropParameterCatalogStartupPlanIssues),
            new("parameter_catalog_startup_report", CreateParameterCatalogStartupReport, DropParameterCatalogStartupReport),
            new("parameter_catalog_startup_report_outcomes", CreateParameterCatalogStartupReportOutcomes, DropParameterCatalogStartupReportOutcomes),
            new("parameter_catalog_startup_report_outcomes_key", CreateParameterCatalogStartupReportOutcomesKey, DropParameterCatalogStartupReportOutcomesKey),
            new("configuration_baseline_application", CreateConfigurationBaselineApplication, DropConfigurationBaselineApplication),
        ];

    static string RegisteredParameterSchemaSeeds()
        => string.Join("\n", ParameterSchemaRegistry.Default.Definitions.Select(schema =>
            string.Format(CultureInfo.InvariantCulture, SeedRegisteredParameterSchema,
                EscapeSqlLiteral(schema.ComponentCode), schema.Version, EscapeSqlLiteral(schema.Codec),
                EscapeSqlLiteral(schema.SchemaSha256), EscapeSqlLiteral(schema.JsonSchema))));

    static string EscapeSqlLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    static string JoinSql(string first, string second) => first + "\n" + second;
}
