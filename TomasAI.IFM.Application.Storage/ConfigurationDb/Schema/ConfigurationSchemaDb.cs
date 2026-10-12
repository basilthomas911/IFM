using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

/// <summary>Creates the PostgreSQL strategy-configuration schema and versioned strategy parameter tables.</summary>
public sealed class ConfigurationSchemaDb : SchemaDbContext<ConfigurationSchemaDb>
{
    /// <summary>Creates the normal configuration schema with its lookup defaults.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6672", Justification = "SchemaDbContext shares the storage provider logger category.")]
    public ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
        : this(connectionSettings ?? throw new ArgumentNullException(nameof(connectionSettings)), logger, true) { }

    /// <summary>Creates structure only for an isolated source-event rebuild or offline migration.</summary>
    public static ConfigurationSchemaDb WithoutLookupDefaults(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(connectionSettings);
        return new(connectionSettings, logger, false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S6672", Justification = "SchemaDbContext shares the storage provider logger category.")]
    private ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger, bool seedLookupDefaults)
        : base(connectionSettings[ConfigurationDbContext.ConfigurationDbConnection], logger)
    {
        definitions = seedLookupDefaults ? Objects : Objects.Select(definition =>
            string.Equals(definition.Name, "lookup_definition", StringComparison.Ordinal)
                ? definition with { CreateStatement = LookupDefinitionSchemaSql.CreateStructure } : definition).ToArray();
    }
    static readonly string[] Tables =
    [
        "intrinsic_time_strategy_workflow_parameter_set",
        "regime_discovery_parameter_set",
        "market_condition_parameter_set",
        "trade_selection_parameter_set",
        "order_composition_parameter_set",
        "risk_management_parameter_set",
        "strategy_position_risk_parameter_set"
    ];

    static readonly SchemaObjectDefinition[] Objects =
        new[] { new SchemaObjectDefinition("reference_configuration", ConfigurationSchemaSql.CreateSchema,
            "DROP SCHEMA IF EXISTS reference_configuration CASCADE;") }
        .Concat(Tables.Select(table => new SchemaObjectDefinition(
            table,
            ConfigurationSchemaSql.CreateTable(table),
            $"DROP TABLE IF EXISTS reference_configuration.{table};")))
        .Concat(new[] { "trade_selection_parameter_set", "intrinsic_time_strategy_workflow_parameter_set", "order_composition_parameter_set", "strategy_position_risk_parameter_set" }
            .Select(table => new SchemaObjectDefinition(table + "_lifecycle_guard",
                (ConfigurationSchemaSql.EnsureMarketConditionLifecycleConstraints + ConfigurationSchemaSql.CreateMarketConditionLifecycleGuard)
                    .Replace("market_condition_parameter_set", table,System.StringComparison.Ordinal).Replace("MarketCondition", table, StringComparison.Ordinal),
                $"DROP TRIGGER IF EXISTS trg_guard_{table} ON reference_configuration.{table}; DROP FUNCTION IF EXISTS reference_configuration.guard_{table}();")))
        .Append(new SchemaObjectDefinition("ix_market_condition_parameter_set_effective",
            ConfigurationSchemaSql.CreateMarketConditionEffectiveIndex,
            "DROP INDEX IF EXISTS reference_configuration.ix_market_condition_parameter_set_effective;"))
        .Append(new SchemaObjectDefinition("market_condition_parameter_set_lifecycle_constraints",
            ConfigurationSchemaSql.EnsureMarketConditionLifecycleConstraints,
            """
            ALTER TABLE IF EXISTS reference_configuration.market_condition_parameter_set
            DROP CONSTRAINT IF EXISTS ck_market_condition_parameter_set_lifecycle,
            DROP CONSTRAINT IF EXISTS ck_market_condition_parameter_set_status;
            """))
        .Append(new SchemaObjectDefinition("market_condition_parameter_set_lifecycle_guard",
            ConfigurationSchemaSql.CreateMarketConditionLifecycleGuard,
            """
            DROP TRIGGER IF EXISTS trg_guard_market_condition_parameter_set
            ON reference_configuration.market_condition_parameter_set;
            DROP FUNCTION IF EXISTS reference_configuration.guard_market_condition_parameter_set();
            """))
        .Append(new SchemaObjectDefinition("market_condition_assessment_parameter_set",
            MarketConditionAssessmentSchemaSql.Create,
            "DROP TABLE IF EXISTS reference_configuration.market_condition_assessment_parameter_set;"))
        .Append(new SchemaObjectDefinition("volatility_series_definition",
            VolatilitySeriesDefinitionSchemaSql.Create, VolatilitySeriesDefinitionSchemaSql.Drop))
        .Append(new SchemaObjectDefinition("lookup_definition", LookupDefinitionSchemaSql.Create, LookupDefinitionSchemaSql.Drop))
        .Append(new SchemaObjectDefinition("strategy_catalog",
            StrategyCatalogSchemaSql.Create, StrategyCatalogSchemaSql.Drop))
        .Append(new SchemaObjectDefinition("parameter_registry", ParameterSets.ParameterRegistrySchemaSql.Create,
            "DROP TABLE IF EXISTS reference_configuration.parameter_schema_version; DROP TABLE IF EXISTS reference_configuration.parameter_component; DROP TABLE IF EXISTS reference_configuration.parameter_area;"))
        .Append(new SchemaObjectDefinition("parameter_sets", ParameterSets.ParameterSetSchemaSql.Create,
            "DROP TABLE IF EXISTS reference_configuration.parameter_assignment_revision; DROP TABLE IF EXISTS reference_configuration.parameter_assignment; DROP TABLE IF EXISTS reference_configuration.parameter_operation; DROP TABLE IF EXISTS reference_configuration.parameter_set_version; DROP TABLE IF EXISTS reference_configuration.parameter_set;"))
        .Append(new SchemaObjectDefinition("configuration_baseline_application", ConfigurationProjectionBaseline.CreateCompletion,
            "DROP TABLE IF EXISTS reference_configuration.configuration_baseline_application;"))
        .ToArray();

    readonly SchemaObjectDefinition[] definitions;

    /// <inheritdoc />
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => definitions;
}
