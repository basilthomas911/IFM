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
        + TradeSchemaSql.StrategyExecutionHandoffCreate + "\n" + TradeSchemaSql.CreateTradeTables
        + "\n" + TradeSchemaSql.StrategySignalQueueCreate + "\n" + TradeSchemaSql.StrategyUpdatesQueueCreate
        + "\n" + TradeSchemaSql.DropDuplicateIndexes + "\n" + TradeSchemaSql.PositionEndOfDayRecoveryCreate;

    /// <summary>Gets the live-feed recovery table schema for isolated qualification.</summary>
    public static string LiveFeedRecoveryCreateStatement => TradeSchemaSql.LiveFeedRecoveryCreate;

    static readonly SchemaObjectDefinition[] Objects = [
        new("trade", CreateStatement, TradeSchemaSql.Drop),
        new("position_eod_recovery", TradeSchemaSql.PositionEndOfDayRecoveryCreate, "DROP TABLE IF EXISTS trade.position_eod_recovery;"),
        new("live_feed_recovery", TradeSchemaSql.LiveFeedRecoveryCreate, TradeSchemaSql.DropLiveFeedRecovery),
        new("intrinsic_time_strategy_signal_queue", TradeSchemaSql.StrategySignalQueueCreate, TradeSchemaSql.StrategySignalQueueDrop),
        new("intrinsic_time_strategy_updates_queue", TradeSchemaSql.StrategyUpdatesQueueCreate, TradeSchemaSql.StrategyUpdatesQueueDrop)];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
