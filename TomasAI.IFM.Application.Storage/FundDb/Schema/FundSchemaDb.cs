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
        new("fund_order_by_order_id_v3", FundSchemaCql.CreateFundOrderByOrderIdV3Table, "DROP TABLE IF EXISTS fund_order_by_order_id_v3;"),
        new("fund_order_write_ownership_v3", FundSchemaCql.CreateFundOrderWriteOwnershipV3Table, "DROP TABLE IF EXISTS fund_order_write_ownership_v3;"),
        new("fund_order_trade", FundSchemaCql.CreateFundOrderTradeTable, "DROP TABLE IF EXISTS fund_order_trade;"),
        new("fund_transaction", FundSchemaCql.CreateFundTransactionTable, "DROP TABLE IF EXISTS fund_transaction;"),
        new("fund_transaction_identity_v4", FundSchemaCql.CreateFundTransactionIdentityV4Table, "DROP TABLE IF EXISTS fund_transaction_identity_v4;"),
        new("fund_transaction_timeline_v3", FundSchemaCql.CreateFundTransactionTimelineV3Table, "DROP TABLE IF EXISTS fund_transaction_timeline_v3;"),
        new("fund_balance_by_status_day_v3", FundSchemaCql.CreateFundBalanceByStatusDayV3Table, "DROP TABLE IF EXISTS fund_balance_by_status_day_v3;"),
        new("fund_transaction_amount_v3", FundSchemaCql.CreateFundTransactionAmountV3Table, "DROP TABLE IF EXISTS fund_transaction_amount_v3;"),
        new("fund_transaction_projection_state_v3", FundSchemaCql.CreateFundTransactionProjectionStateV3Table, "DROP TABLE IF EXISTS fund_transaction_projection_state_v3;"),
        new("fund_transaction_projection_mutation_v3", FundSchemaCql.CreateFundTransactionProjectionMutationV3Table, "DROP TABLE IF EXISTS fund_transaction_projection_mutation_v3;"),
        new("fund_transaction_write_mutation_v3", FundSchemaCql.CreateFundTransactionWriteMutationV3Table, "DROP TABLE IF EXISTS fund_transaction_write_mutation_v3;"),
        new("fund_transaction_write_ownership_v3", FundSchemaCql.CreateFundTransactionWriteOwnershipV3Table, "DROP TABLE IF EXISTS fund_transaction_write_ownership_v3;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
