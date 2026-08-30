using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

public sealed class PortfolioSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<PortfolioSchemaDb>(settings[PortfolioDbContext.PortfolioDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("portfolio_by_id", PortfolioSchemaCql.CreatePortfolioById, "DROP TABLE IF EXISTS portfolio_by_id;"),
        new("portfolio_by_state", PortfolioSchemaCql.CreatePortfolioByState, "DROP TABLE IF EXISTS portfolio_by_state;"),
        new("fund_by_portfolio", PortfolioSchemaCql.CreateFundByPortfolio, "DROP TABLE IF EXISTS fund_by_portfolio;"),
        new("fund_by_id", PortfolioSchemaCql.CreateFundById, "DROP TABLE IF EXISTS fund_by_id;"),
        new("active_fund_by_portfolio_horizon", PortfolioSchemaCql.CreateActiveFund, "DROP TABLE IF EXISTS active_fund_by_portfolio_horizon;"),
        new("fund_template_assignment", PortfolioSchemaCql.CreateAssignment, "DROP TABLE IF EXISTS fund_template_assignment;"),
        new("fund_allocation", PortfolioSchemaCql.CreateAllocation, "DROP TABLE IF EXISTS fund_allocation;"),
        new("fund_risk_envelope", PortfolioSchemaCql.CreateEnvelope, "DROP TABLE IF EXISTS fund_risk_envelope;"),
        new("fund_order_by_portfolio_fund_month", PortfolioSchemaCql.CreateOrderTimeline, "DROP TABLE IF EXISTS fund_order_by_portfolio_fund_month;"),
        new("fund_order_by_order_id", PortfolioSchemaCql.CreateOrderById, "DROP TABLE IF EXISTS fund_order_by_order_id;"),
        new("fund_order_trade_by_order_id", PortfolioSchemaCql.CreateTradesByOrder, "DROP TABLE IF EXISTS fund_order_trade_by_order_id;"),
        new("fund_order_trade_by_trade_id", PortfolioSchemaCql.CreateTradeById, "DROP TABLE IF EXISTS fund_order_trade_by_trade_id;"),
        new("fund_composition_by_workflow", PortfolioSchemaCql.CreateCompositionByWorkflow, "DROP TABLE IF EXISTS fund_composition_by_workflow;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
