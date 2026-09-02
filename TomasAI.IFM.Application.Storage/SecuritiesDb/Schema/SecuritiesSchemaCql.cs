namespace TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

internal static class SecuritiesSchemaCql
{
    public const string CreateFuturesContractRolloverTable = """
    CREATE TABLE IF NOT EXISTS futures_contract_rollover (
    symbol text PRIMARY KEY,
    contractId text,
    nextRolloverDate date,
    updatedOn timestamp,
    updatedBy text,
    createdOn timestamp,
    createdBy text
    );
    """;

    public const string CreateFuturesContractTable = """
    CREATE TABLE IF NOT EXISTS futures_contract_v3 (
    contractId text,
    description text,
    symbol text,
    localSymbol text,
    securityType text,
    currency text,
    exchange text,
    multiplier text,
    lastTradeDate date,
    onTheRun boolean,
    rollover boolean,
    PRIMARY KEY ((contractId), symbol, lastTradeDate)
    )
    WITH CLUSTERING ORDER BY (symbol ASC, lastTradeDate DESC);
    """;

    public const string CreateFuturesOptionContractTable = """
    CREATE TABLE IF NOT EXISTS futures_option_contract (
    contractId text,
    description text,
    symbol text,
    localSymbol text,
    securityType text,
    currency text,
    exchange text,
    multiplier text,
    contractMonth date,
    strikePrice double,
    optionType text,
    PRIMARY KEY ((contractId),contractMonth, symbol, optionType, strikePrice)
    );
    """;

    // Explicit query tables are used instead of materialized views so projection writes,
    // backfill progress, validation, and rollback can be controlled by the application.
    public const string CreateFuturesContractBySymbolV3Table = """
    CREATE TABLE IF NOT EXISTS futures_contract_by_symbol_v3 (
    symbol text,
    rollover boolean,
    onTheRun boolean,
    lastTradeDate date,
    contractId text,
    description text,
    localSymbol text,
    securityType text,
    currency text,
    exchange text,
    multiplier text,
    PRIMARY KEY ((symbol), rollover, onTheRun, lastTradeDate, contractId)
    )
    WITH CLUSTERING ORDER BY (rollover DESC, onTheRun DESC, lastTradeDate ASC, contractId ASC);
    """;

    public const string CreateFuturesOptionContractBySymbolV2Table = """
    CREATE TABLE IF NOT EXISTS futures_option_contract_by_symbol_v2 (
    symbol text,
    contractMonth date,
    contractId text,
    description text,
    localSymbol text,
    securityType text,
    currency text,
    exchange text,
    multiplier text,
    strikePrice double,
    optionType text,
    PRIMARY KEY ((symbol), contractMonth, optionType, strikePrice, contractId)
    )
    WITH CLUSTERING ORDER BY (contractMonth DESC, optionType ASC, strikePrice ASC, contractId ASC);
    """;

    public const string CreateSecuritiesProjectionStateV3Table = """
    CREATE TABLE IF NOT EXISTS securities_projection_state_v3 (
    projectionName text,
    generation uuid,
    completed boolean,
    activeOperations set<uuid>,
    PRIMARY KEY ((projectionName))
    );
    """;

    public const string CreateSecuritiesSymbolProjectionStateV3Table = """
    CREATE TABLE IF NOT EXISTS securities_symbol_projection_state_v3 (
    projectionName text,
    symbol text,
    generation uuid,
    completed boolean,
    activeOperations set<uuid>,
    PRIMARY KEY ((projectionName, symbol))
    );
    """;

    // Operation rows are durable recovery evidence. They intentionally have no TTL:
    // an in-flight operation must never disappear merely because it became old.
    public const string CreateSecuritiesProjectionOperationV3Table = """
    CREATE TABLE IF NOT EXISTS securities_projection_operation_v3 (
    projectionName text,
    operationId uuid,
    startedOn timestamp,
    stateMayBeActive boolean,
    PRIMARY KEY ((projectionName), operationId)
    );
    """;

    // Scopes are journaled before any projection state is invalidated. This lets an
    // operator remove a verified-dead operation from only the rows it could have touched.
    public const string CreateSecuritiesProjectionOperationScopeV3Table = """
    CREATE TABLE IF NOT EXISTS securities_projection_operation_scope_v3 (
    projectionName text,
    operationId uuid,
    scopeType text,
    scopeKey text,
    PRIMARY KEY ((projectionName, operationId), scopeType, scopeKey)
    );
    """;
}
