using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.FundDb.Schema;

public sealed class FundSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<FundSchemaDb>(connectionSettings[FundDbContext.FundDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("fund", FundSchemaCql.CreateFundTable, "DROP TABLE IF EXISTS fund;"),
        new("fund_order", FundSchemaCql.CreateFundOrderTable, "DROP TABLE IF EXISTS fund_order;"),
        new("fund_order_trade", FundSchemaCql.CreateFundOrderTradeTable, "DROP TABLE IF EXISTS fund_order_trade;"),
        new("fund_transaction", FundSchemaCql.CreateFundTransactionTable, "DROP TABLE IF EXISTS fund_transaction;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
