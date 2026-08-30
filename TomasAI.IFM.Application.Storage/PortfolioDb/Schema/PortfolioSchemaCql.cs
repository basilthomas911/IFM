namespace TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

public static class PortfolioSchemaCql
{
    const string Columns = "schemaVersion int, aggregateVersion bigint, sourceEventId bigint, updatedOnUtc timestamp, payloadJson text, payloadHash text";

    public static readonly string CreatePortfolioById = $"""
CREATE TABLE IF NOT EXISTS portfolio_by_id (portfolioId int, portfolioVersion bigint, operatingState text, {Columns}, PRIMARY KEY ((portfolioId), portfolioVersion)) WITH CLUSTERING ORDER BY (portfolioVersion DESC);
""";
    public static readonly string CreatePortfolioByState = $"""
CREATE TABLE IF NOT EXISTS portfolio_by_state (operatingState text, bucket int, portfolioId int, portfolioVersion bigint, {Columns}, PRIMARY KEY ((operatingState, bucket), portfolioId));
""";
    public static readonly string CreateFundByPortfolio = $"""
CREATE TABLE IF NOT EXISTS fund_by_portfolio (portfolioId int, fundId int, fundMandateVersion bigint, operatingState text, {Columns}, PRIMARY KEY ((portfolioId), fundId, fundMandateVersion)) WITH CLUSTERING ORDER BY (fundId ASC, fundMandateVersion DESC);
""";
    public static readonly string CreateFundById = $"""
CREATE TABLE IF NOT EXISTS fund_by_id (fundId int, fundMandateVersion bigint, portfolioId int, operatingState text, {Columns}, PRIMARY KEY ((fundId), fundMandateVersion)) WITH CLUSTERING ORDER BY (fundMandateVersion DESC);
""";
    public static readonly string CreateActiveFund = $"""
CREATE TABLE IF NOT EXISTS active_fund_by_portfolio_horizon (portfolioId int, tradingYear int, decisionHorizon text, effectiveFromUtc timestamp, fundId int, fundMandateVersion bigint, {Columns}, PRIMARY KEY ((portfolioId, tradingYear, decisionHorizon), effectiveFromUtc, fundId)) WITH CLUSTERING ORDER BY (effectiveFromUtc DESC, fundId ASC);
""";
    public static readonly string CreateAssignment = $"""
CREATE TABLE IF NOT EXISTS fund_template_assignment (portfolioId int, fundId int, fundMandateVersion bigint, tradeTemplateId uuid, tradeTemplateVersion bigint, {Columns}, PRIMARY KEY ((portfolioId, fundId, fundMandateVersion), tradeTemplateId, tradeTemplateVersion));
""";
    public static readonly string CreateAllocation = $"""
CREATE TABLE IF NOT EXISTS fund_allocation (portfolioId int, fundId int, allocationVersion bigint, {Columns}, PRIMARY KEY ((portfolioId, fundId), allocationVersion)) WITH CLUSTERING ORDER BY (allocationVersion DESC);
""";
    public static readonly string CreateEnvelope = $"""
CREATE TABLE IF NOT EXISTS fund_risk_envelope (portfolioId int, fundId int, envelopeVersion bigint, {Columns}, PRIMARY KEY ((portfolioId, fundId), envelopeVersion)) WITH CLUSTERING ORDER BY (envelopeVersion DESC);
""";
    public static readonly string CreateOrderTimeline = $"""
CREATE TABLE IF NOT EXISTS fund_order_by_portfolio_fund_month (portfolioId int, fundId int, orderMonth date, createdOnUtc timestamp, orderId int, status text, {Columns}, PRIMARY KEY ((portfolioId, fundId, orderMonth), createdOnUtc, orderId)) WITH CLUSTERING ORDER BY (createdOnUtc DESC, orderId DESC);
""";
    public static readonly string CreateOrderById = $"""
CREATE TABLE IF NOT EXISTS fund_order_by_order_id (orderId int PRIMARY KEY, portfolioId int, fundId int, status text, {Columns});
""";
    public static readonly string CreateTradesByOrder = $"""
CREATE TABLE IF NOT EXISTS fund_order_trade_by_order_id (orderId int, tradeId int, portfolioId int, fundId int, {Columns}, PRIMARY KEY ((orderId), tradeId));
""";
    public static readonly string CreateTradeById = $"""
CREATE TABLE IF NOT EXISTS fund_order_trade_by_trade_id (tradeId int PRIMARY KEY, orderId int, portfolioId int, fundId int, {Columns});
""";
    public static readonly string CreateCompositionByWorkflow = $"""
CREATE TABLE IF NOT EXISTS fund_composition_by_workflow (workflowId uuid, orderId int, portfolioId int, fundId int, status text, {Columns}, PRIMARY KEY ((workflowId), orderId));
""";
}
