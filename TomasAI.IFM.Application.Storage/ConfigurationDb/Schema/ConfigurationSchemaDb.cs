using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

/// <summary>Creates the PostgreSQL strategy-configuration schema and all six pipeline parameter tables.</summary>
public sealed class ConfigurationSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<ConfigurationSchemaDb>(connectionSettings[ConfigurationDbContext.ConfigurationDbConnection], logger)
{
    static readonly string[] Tables =
    [
        "intrinsic_time_strategy_workflow_parameter_set",
        "regime_discovery_parameter_set",
        "market_condition_parameter_set",
        "trade_selection_parameter_set",
        "order_composition_parameter_set",
        "risk_management_parameter_set"
    ];

    static readonly SchemaObjectDefinition[] Objects =
        new[] { new SchemaObjectDefinition("reference_configuration", ConfigurationSchemaSql.CreateSchema,
            "DROP SCHEMA IF EXISTS reference_configuration CASCADE;") }
        .Concat(Tables.Select(table => new SchemaObjectDefinition(
            table,
            ConfigurationSchemaSql.CreateTable(table),
            $"DROP TABLE IF EXISTS reference_configuration.{table};")))
        .Append(new SchemaObjectDefinition("ix_market_condition_parameter_set_effective",
            ConfigurationSchemaSql.CreateMarketConditionEffectiveIndex,
            "DROP INDEX IF EXISTS reference_configuration.ix_market_condition_parameter_set_effective;"))
        .ToArray();

    /// <inheritdoc />
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
