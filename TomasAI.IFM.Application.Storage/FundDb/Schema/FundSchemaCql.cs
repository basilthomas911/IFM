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

    public const string CreateFundOrderByOrderIdV3Table = """
    CREATE TABLE IF NOT EXISTS fund_order_by_order_id_v3 (
    orderId int PRIMARY KEY,
    fundId int,
    reservationToken uuid
    );
    """;

    public const string CreateFundOrderWriteOwnershipV3Table = """
    CREATE TABLE IF NOT EXISTS fund_order_write_ownership_v3 (
    orderId int PRIMARY KEY,
    operationId uuid,
    startedOn timestamp
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

    // One immutable reservation per canonical logical transaction key. The full logical
    // key is the partition key so Paxos contention is isolated to actual retries rather
    // than serializing unrelated transactions for the same fund or value date.
    public const string CreateFundTransactionIdentityV4Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_identity_v4 (
    fundId int,
    valueDate date,
    orderId int,
    tradeId int,
    tradeType text,
    transactionType text,
    transactionDate timestamp,
    transactionId bigint,
    PRIMARY KEY ((fundId, valueDate, orderId, tradeId, tradeType, transactionType, transactionDate))
    );
    """;

    public const string CreateFundTransactionTimelineV3Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_timeline_v3 (
    fundId int,
    monthBucket date,
    valueDate date,
    transactionDate timestamp,
    transactionId bigint,
    transactionType text,
    orderId int,
    tradeId int,
    tradeType text,
    tradeStatus text,
    description text,
    amount decimal,
    balance decimal,
    PRIMARY KEY ((fundId, monthBucket), valueDate, orderId, tradeId, tradeType, transactionType, transactionDate, transactionId)
    );
    """;

    public const string CreateFundBalanceByStatusDayV3Table = """
    CREATE TABLE IF NOT EXISTS fund_balance_by_status_day_v3 (
    fundId int,
    monthBucket date,
    valueDate date,
    tradeStatus text,
    transactionDate timestamp,
    transactionId bigint,
    transactionType text,
    orderId int,
    tradeId int,
    tradeType text,
    balance decimal,
    PRIMARY KEY ((fundId, monthBucket), valueDate, tradeStatus, transactionDate, transactionId, orderId, tradeId, tradeType, transactionType)
    );
    """;

    public const string CreateFundTransactionAmountV3Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_amount_v3 (
    fundId int,
    monthBucket date,
    transactionType text,
    amountSign int,
    valueDate date,
    transactionDate timestamp,
    transactionId bigint,
    orderId int,
    tradeId int,
    tradeType text,
    amount decimal,
    PRIMARY KEY ((fundId, monthBucket), transactionType, amountSign, valueDate, transactionDate, transactionId, orderId, tradeId, tradeType)
    );
    """;

    public const string CreateFundTransactionProjectionStateV3Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_projection_state_v3 (
    fundId int,
    monthBucket date,
    generation uuid,
    isComplete boolean,
    sourceCount bigint,
    sourceFingerprint text,
    reconciledOn timestamp,
    PRIMARY KEY ((fundId, monthBucket))
    );
    """;

    public const string CreateFundTransactionProjectionMutationV3Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_projection_mutation_v3 (
    fundId int,
    monthBucket date,
    mutationId uuid,
    startedOn timestamp,
    PRIMARY KEY ((fundId, monthBucket), mutationId)
    );
    """;

    public const string CreateFundTransactionWriteMutationV3Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_write_mutation_v3 (
    fundId int,
    mutationId uuid,
    startedOn timestamp,
    PRIMARY KEY ((fundId), mutationId)
    );
    """;

    public const string CreateFundTransactionWriteOwnershipV3Table = """
    CREATE TABLE IF NOT EXISTS fund_transaction_write_ownership_v3 (
    fundId int PRIMARY KEY,
    ownerMutationId uuid,
    conflicted boolean,
    claimedOn timestamp
    );
    """;
}
