namespace TomasAI.IFM.Application.Storage.FundDb.Schema;

internal static class FundSchemaCql
{
    public const string CreateFundOrderTable = """
    CREATE TABLE IF NOT EXISTS fund_order (
    FundId int,
    OrderId int,
    OrderDate timestamp,
    OrderStatus text,
    BaseContractId text,
    TradeDate date,
    MaturityDate date,
    Reference text,
    CreatedOn timestamp,
    CreatedBy text,
    UpdatedOn timestamp,
    UpdatedBy text,
    PRIMARY KEY (FundId, OrderId)
    );
    """;

    public const string CreateFundOrderTradeTable = """
    CREATE TABLE IF NOT EXISTS fund_order_trade (
    fundId int,
    orderId int,
    tradeId int,
    tradeType text,
    tradeDate date,
    maturityDate date,
    tradeState text,
    tradeAction text,
    reference text,
    primaryTrade boolean,
    baseContractSymbol text,
    createdOn timestamp,
    createdBy text,
    updatedOn timestamp,
    updatedBy text,
    PRIMARY KEY ((fundId, orderId), tradeId)
    );
    """;

    public const string CreateFundTable = """
    CREATE TABLE IF NOT EXISTS fund (
    FundId int PRIMARY KEY,
    Name text,
    Description text,
    Balance decimal,
    IsProduction boolean,
    CreatedOn timestamp,
    CreatedBy text
    )
    """;

    public const string CreateFundTransactionTable = """
    CREATE TABLE IF NOT EXISTS fund_transaction (
    transactionId bigint,
    transactionDate timestamp,
    transactionType text,
    fundId int,
    orderId int,
    tradeId int,
    tradeType text,
    valueDate date,
    tradeStatus text,
    description text,
    amount decimal,
    balance decimal,
    PRIMARY KEY (fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate, transactionId)
    );
    """;
}
