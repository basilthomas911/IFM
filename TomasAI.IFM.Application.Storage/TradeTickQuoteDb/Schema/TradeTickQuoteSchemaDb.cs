using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;

/// <summary>Creates the isolated raw trade and quote schema in dependency order.</summary>
public sealed class TradeTickQuoteSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradeTickQuoteSchemaDb>(settings[TradeTickQuoteDbContext.TradeTickQuoteDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("ifm_recovery_probe", TradeTickQuoteSchemaCql.CreateRecoveryProbeTable, "DROP TABLE IF EXISTS ifm_recovery_probe;"),
        new("tick_quote_item", TradeTickQuoteSchemaCql.CreateTickQuoteItemType, "DROP TYPE IF EXISTS tick_quote_item;"),
        new("tick_trade_data", TradeTickQuoteSchemaCql.CreateTickTradeDataTable, "DROP TABLE IF EXISTS tick_trade_data;"),
        new("tick_quote_data", TradeTickQuoteSchemaCql.CreateTickQuoteDataTable, "DROP TABLE IF EXISTS tick_quote_data;"),
        new("futures_tick_data", TradeTickQuoteSchemaCql.CreateFuturesTickDataTable, "DROP TABLE IF EXISTS futures_tick_data;"),
        new("futures_tick_data_by_time", TradeTickQuoteSchemaCql.CreateFuturesTickDataByTimeTable, "DROP TABLE IF EXISTS futures_tick_data_by_time;"),
        new("futures_option_tick_data", TradeTickQuoteSchemaCql.CreateFuturesOptionTickDataTable, "DROP TABLE IF EXISTS futures_option_tick_data;")
    ];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
