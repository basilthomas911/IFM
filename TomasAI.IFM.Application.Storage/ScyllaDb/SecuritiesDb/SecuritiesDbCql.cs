namespace TomasAI.IFM.Application.Storage.ScyllaDb.SecuritiesDb;

internal class SecuritiesDbCql
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

    public const string DeleteFuturesContract = """
        DELETE FROM futures_contract
        WHERE contractId = :contractId;
        """;

    public const string DeleteCurrentlyTradedFuturesContract = """
        DELETE FROM futures_contract
        WHERE symbol = :symbol
        AND currentlyTraded = true
        ALLOW FILTERING;
        """;


    public const string DeleteFuturesContractById = """
        DELETE FROM futures_contract
        WHERE contractId = :contractId
        AND symbol = :symbol
        AND lastTradeDate = :lastTradeDate;
        """;

    public const string DeleteFuturesOptionContract = """
        DELETE FROM futures_option_contract
        WHERE contractId = :contractId;
        """;

    public const string GetCurrentlyTradeFuturesContract = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract 
        WHERE symbol = :symbol
        and currentlyTraded = true
        LIMIT 1
        allow filtering;
        """;

    public const string GetCurrentlyTradeFuturesContracts = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract 
        WHERE symbol = :symbol
        and currentlyTraded = true
        allow filtering;
        """;

    public const string GetFuturesContract = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract
        WHERE contractId = :contractId;
        """;

    public const string GetFuturesContractById = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract
        WHERE contractId = :contractId
        AND symbol = :symbol
        AND lastTradeDate = :lastTradeDate;
        """;

    public const string GetFuturesContracts = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract;
        """;

    public const string GetFuturesContractsByIds = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract
        WHERE contractId in :contractIds
        AND symbol = :symbol;
        """;

    public const string GetFuturesContractsBySymbol = """
        SELECT 
            contractId AS "ContractId",
            description AS "Description",
            symbol AS "Symbol",
            localSymbol AS "LocalSymbol",
            securityType AS "SecurityType",
            currency AS "Currency",
            exchange AS "Exchange",
            multiplier AS "Multiplier",
            lastTradeDate AS "LastTradeDate",
            currentlyTraded AS "CurrentlyTraded"
        FROM futures_contract
        WHERE symbol = :symbol
        ALLOW FILTERING;
        """;

    public const string GetFuturesOptionContract = """
        SELECT 
            contractId AS "ContractId", 
            description AS "Description", 
            symbol AS "Symbol", 
            localSymbol AS "LocalSymbol", 
            securityType AS "SecurityType", 
            currency AS "Currency", 
            exchange AS "Exchange", 
            multiplier AS "Multiplier", 
            contractMonth AS "ContractMonth", 
            strikePrice AS "StrikePrice", 
            optionType AS "OptionType"
        FROM futures_option_contract
        WHERE contractId = :contractId;
        """;

    public const string GetFuturesOptionContracts = """
        SELECT 
            contractId AS "ContractId", 
            description AS "Description", 
            symbol AS "Symbol", 
            localSymbol AS "LocalSymbol", 
            securityType AS "SecurityType", 
            currency AS "Currency", 
            exchange AS "Exchange", 
            multiplier AS "Multiplier", 
            contractMonth AS "ContractMonth", 
            strikePrice AS "StrikePrice", 
            optionType AS "OptionType"
        FROM futures_option_contract;
        """;

    public const string InsertFuturesContract = """
        INSERT INTO futures_contract (
            contractId, 
            description, 
            symbol, 
            localSymbol, 
            securityType, 
            currency, 
            exchange, 
            multiplier, 
            lastTradeDate, 
            currentlyTraded
        )
        VALUES (
            :contractId, 
            :description, 
            :symbol, 
            :localSymbol, 
            :securityType, 
            :currency, 
            :exchange, 
            :multiplier, 
            :lastTradeDate, 
            :currentlyTraded
        )
        """;

    public const string InsertFuturesOptionContract = """
        INSERT INTO futures_option_contract (
            contractId, 
            description, 
            symbol, 
            localSymbol, 
            securityType, 
            currency, 
            exchange, 
            multiplier, 
            contractMonth, 
            strikePrice, 
            optionType
        )
        VALUES (
            :contractId, 
            :description, 
            :symbol, 
            :localSymbol, 
            :securityType, 
            :currency, 
            :exchange, 
            :multiplier, 
            :contractMonth, 
            :strikePrice, 
            :optionType
        );
        """;
}
