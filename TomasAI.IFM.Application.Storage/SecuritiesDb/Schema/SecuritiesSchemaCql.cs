namespace TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

internal static class SecuritiesSchemaCql
{
    public const string CreateFuturesContractTable = """
    CREATE TABLE IF NOT EXISTS futures_contract (
    contractId text,
    description text,
    symbol text,
    localSymbol text,
    securityType text,
    currency text,
    exchange text,
    multiplier text,
    lastTradeDate date,
    currentlyTraded boolean,
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
}
