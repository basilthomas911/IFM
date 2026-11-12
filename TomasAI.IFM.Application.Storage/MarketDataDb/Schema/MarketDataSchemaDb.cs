using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaSql;

namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

/// <summary>Creates and removes the complete PostgreSQL MarketDataDb schema in dependency order.</summary>
/// <param name="connectionSettings">The named PostgreSQL connection settings.</param>
/// <param name="logger">The database provider logger.</param>
public sealed class MarketDataSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<MarketDataSchemaDb>(connectionSettings[MarketDataDbContext.MarketDataDbConnection] is { ProviderName: "System.Data.Postgres" } postgresConnection
        ? postgresConnection : throw new NotSupportedException("Market Data projection schema requires PostgreSQL."), logger)
{
    /// <summary>Creates the namespace before independent table preflight steps begin.</summary>
    /// <param name="cancellationToken">Cancels creation of the namespace.</param>
    /// <returns>Completion after PostgreSQL has committed the namespace.</returns>
    public async Task EnsureNamespaceAsync(CancellationToken cancellationToken)
    {
        await Use(nameof(MarketDataSchemaSql.EnsureNamespace), MarketDataSchemaSql.EnsureNamespace)

            .ExecuteCommandAsync(cancellationToken)

            .ConfigureAwait(false);
    }

    /// <summary>Gets the dependency-ordered table and index definitions.</summary>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => GetDefinitions();

    /// <summary>Provisions historical option-chain tables for an offline version-one baseline restore.</summary>
    /// <param name="transaction">The offline restore transaction.</param>
    /// <param name="cancellationToken">Cancels schema preparation.</param>
    /// <returns>Completion of schema preparation.</returns>
    public static Task CreateLegacyOptionChainBaselineTablesAsync(IEnlistedPostgresTransaction transaction, CancellationToken cancellationToken)
        => transaction.ExecuteAsync(MarketDataSchemaSql.LegacyOptionChainBaselineCreate, [], cancellationToken);

    /// <summary>Gets the version and current option-chain policy schema for baseline migrations.</summary>
    public static string OptionChainPoliciesCreateStatement { get; } = OptionChainVersionCreate + "\n" + OptionChainCurrentCreate;

    /// <summary>Gets the derived market data schema and its prerequisite policy and coverage tables.</summary>
    public static string CreateStatement { get; } =
        OptionChainPoliciesCreateStatement + "\n" + IndicatorSeedCoverageCreate + "\n" + CreateMarketDataTables;

    /// <summary>Returns every PostgreSQL schema object in dependency order.</summary>
    public static SchemaObjectDefinition[] GetDefinitions() =>
    [
        new("historical_data_loader", CreateHistoricalDataLoader, DropHistoricalDataLoader),
        new("historical_analytics_attempt_projection", CreateHistoricalAnalyticsAttemptProjection, DropHistoricalAnalyticsAttemptProjection),
        new("market_data", CreateStatement, DropMarketData),
        new("indicator_seed_coverage", IndicatorSeedCoverageCreate, DropIndicatorSeedCoverage),
        new("strategy_option_chain_parameter_version", OptionChainVersionCreate, DropOptionChainVersion),
        new("strategy_option_chain_parameter_current", OptionChainCurrentCreate, DropOptionChainCurrent),
    ];
}
