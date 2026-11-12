using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;
/// <summary>Creates all PostgreSQL trade tables, indexes and additive changes from the single DDL catalog.</summary>
public sealed class TradeSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : SchemaDbContext<TradeSchemaDb>(connectionSettings[TradeDbContext.TradeDbConnection], logger)
{
    /// <summary>Gets the full trade schema batch, including the additive recovery and handoff tables.</summary>
    public static string CreateStatement { get; } = TradeSchemaSql.LiveFeedRecoveryCreate + "\n"
        + TradeSchemaSql.StrategyExecutionHandoffCreate + "\n" + TradeSchemaSql.CreateTradeTables;

    /// <summary>Gets the live-feed recovery table schema for isolated qualification.</summary>
    public static string LiveFeedRecoveryCreateStatement => TradeSchemaSql.LiveFeedRecoveryCreate;

    static readonly SchemaObjectDefinition[] Objects = [
        new("trade", CreateStatement, TradeSchemaSql.Drop),
        new("live_feed_recovery", TradeSchemaSql.LiveFeedRecoveryCreate, TradeSchemaSql.DropLiveFeedRecovery)];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
