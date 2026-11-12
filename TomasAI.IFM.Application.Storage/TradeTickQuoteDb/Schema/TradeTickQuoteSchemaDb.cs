using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;

/// <summary>Creates the isolated raw trade and quote schema in dependency order.</summary>
/// <param name="settings">Named Scylla connection settings.</param>
/// <param name="logger">Schema execution diagnostics.</param>
public sealed class TradeTickQuoteSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradeTickQuoteSchemaDb>(settings[TradeTickQuoteDbContext.TradeTickQuoteDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("ifm_recovery_probe", TradeTickQuoteSchemaSql.CreateRecoveryProbeTable, TradeTickQuoteSchemaSql.DropRecoveryProbeTable),
        new("tick_quote_item", TradeTickQuoteSchemaSql.CreateTickQuoteItemType, TradeTickQuoteSchemaSql.DropTickQuoteItemType),
        new("tick_trade_data", TradeTickQuoteSchemaSql.CreateTickTradeDataTable, TradeTickQuoteSchemaSql.DropTickTradeDataTable),
        new("tick_quote_data", TradeTickQuoteSchemaSql.CreateTickQuoteDataTable, TradeTickQuoteSchemaSql.DropTickQuoteDataTable),
        new("futures_tick_data", TradeTickQuoteSchemaSql.CreateFuturesTickDataTable, TradeTickQuoteSchemaSql.DropFuturesTickDataTable),
        new("futures_tick_data_by_time", TradeTickQuoteSchemaSql.CreateFuturesTickDataByTimeTable, TradeTickQuoteSchemaSql.DropFuturesTickDataByTimeTable),
        new("futures_option_tick_data", TradeTickQuoteSchemaSql.CreateFuturesOptionTickDataTable, TradeTickQuoteSchemaSql.DropFuturesOptionTickDataTable),
    ];

    /// <inheritdoc />
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
