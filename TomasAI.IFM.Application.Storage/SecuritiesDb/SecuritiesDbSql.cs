namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Native PostgreSQL option-definition queries with explicit deterministic ordering.</summary>
internal static class SecuritiesDbSql
{
    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesContractProjectionSourceKeys = "SELECT symbol, rollover, onTheRun, lastTradeDate, contractId FROM securities.futures_contract;";

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesContractProjectionTargetKeys = "SELECT symbol, rollover, onTheRun, lastTradeDate, contractId FROM securities.futures_contract_by_symbol;";

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesOptionContractProjectionSourceKeys = "SELECT symbol, contractMonth, optionType, strikePrice, contractId FROM securities.futures_option_contract;";

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesOptionContractProjectionTargetKeys = "SELECT symbol, contractMonth, optionType, strikePrice, contractId FROM securities.futures_option_contract_by_symbol;";


    /// <summary>Fences catalog CRUD and projection reads/writes for one atomic rebuild.</summary>
    internal const string LockSymbolProjectionRebuild = """
        LOCK TABLE securities.futures_contract, securities.futures_option_contract,
            securities.futures_contract_by_symbol, securities.futures_option_contract_by_symbol
            IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>Rejects ambiguous canonical IDs before modifying either projection.</summary>
    internal const string HasAmbiguousCanonicalContractIds = """
        SELECT EXISTS (SELECT contractId FROM securities.futures_contract GROUP BY contractId HAVING count(*) > 1)
            OR EXISTS (SELECT contractId FROM securities.futures_option_contract GROUP BY contractId HAVING count(*) > 1);
        """;

    /// <summary>Clears only derived catalog rows inside the rebuild transaction.</summary>
    internal const string ClearSymbolProjections = """
        DELETE FROM securities.futures_contract_by_symbol;
        DELETE FROM securities.futures_option_contract_by_symbol;
        """;

    /// <summary>Rebuilds the futures symbol projection, preserving the exact reference payload.</summary>
    internal const string RebuildFuturesSymbolProjection = """
        INSERT INTO securities.futures_contract_by_symbol (contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, onTheRun, rollover, referencePayload)
        SELECT contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, onTheRun, rollover, referencePayload FROM securities.futures_contract;
        """;

    /// <summary>Rebuilds the option symbol projection, preserving the exact reference payload.</summary>
    internal const string RebuildOptionSymbolProjection = """
        INSERT INTO securities.futures_option_contract_by_symbol (contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType, referencePayload)
        SELECT contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType, referencePayload FROM securities.futures_option_contract;
        """;

    public const string GetNativeOptionContractsPage = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", contractMonth AS "ContractMonth", strikePrice AS "StrikePrice", optionType AS "OptionType", referencePayload AS "ReferencePayload" FROM securities.futures_option_contract_by_symbol WHERE symbol = $1
          AND ($2::date IS NULL OR (contractMonth,optionType COLLATE "C",strikePrice,contractId COLLATE "C") > ($2,$3,$4,$5))
        ORDER BY contractMonth,optionType COLLATE "C",strikePrice,contractId COLLATE "C" LIMIT $6;
        """;

    public const string InsertFuturesContractRolloverIfMissing = """
        INSERT INTO securities.futures_contract_rollover(symbol,createdOn,createdBy) VALUES($1,$2,$3)
        ON CONFLICT(symbol) DO NOTHING;
        """;

    public const string GetOptionContractExpiryCalendarState = """
        SELECT generation,coverageFrom,coverageThrough,refreshedAtUtc
        FROM securities.option_contract_expiry_calendar_state WHERE symbol=$1;
        """;
    public const string GetOptionContractExpiries = """
        SELECT symbol,underlyingContractId,expiryDate,providerRoot,optionFamily,refreshedAtUtc
        FROM securities.option_contract_expiry_calendar
        WHERE symbol=$1 AND generation=$2 AND expiryDate>=$3 AND expiryDate<=$4
        ORDER BY expiryDate,providerRoot,contractId;
        """;
    public const string GetCachedOptionContractDefinitions = """
        SELECT symbol,underlyingContractId,expiryDate,providerRoot,optionFamily,definitionPayload,refreshedAtUtc
        FROM securities.option_contract_expiry_calendar WHERE symbol=$1 AND generation=$2 AND expiryDate=$3
        ORDER BY providerRoot,contractId;
        """;

    public const string GetCachedOptionDefinitionPartitions = """
        SELECT symbol,underlyingContractId,expiryDate,providerRoot,optionFamily,definitionPayload,refreshedAtUtc
        FROM securities.option_contract_expiry_calendar WHERE symbol=$1 AND generation=$2 AND expiryDate=ANY($3::date[])
        ORDER BY expiryDate,providerRoot,contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContractRollover = """
        SELECT symbol, contractId, nextRolloverDate, updatedOn, updatedBy, createdOn, createdBy FROM securities.futures_contract_rollover WHERE symbol = $1 ORDER BY symbol;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContractRollovers = """
        SELECT symbol, contractId, nextRolloverDate, updatedOn, updatedBy, createdOn, createdBy FROM securities.futures_contract_rollover ORDER BY symbol;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string UpdateFuturesContractRollover = """
        UPDATE securities.futures_contract_rollover SET contractId = $1, nextRolloverDate = $2, updatedOn = $3, updatedBy = $4 WHERE symbol = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesContract = """
        DELETE FROM securities.futures_contract WHERE contractId = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesContractById = """
        DELETE FROM securities.futures_contract WHERE contractId = $1 AND symbol = $2 AND lastTradeDate = $3;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesOptionContract = """
        DELETE FROM securities.futures_option_contract WHERE contractId = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesOptionContractById = """
        DELETE FROM securities.futures_option_contract WHERE contractId = $1 AND contractMonth = $2 AND symbol = $3 AND optionType = $4 AND strikePrice = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesContractBySymbolV3 = """
        DELETE FROM securities.futures_contract_by_symbol WHERE symbol = $1 AND rollover = $2 AND onTheRun = $3 AND lastTradeDate = $4 AND contractId = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesOptionContractBySymbolV2 = """
        DELETE FROM securities.futures_option_contract_by_symbol WHERE symbol = $1 AND contractMonth = $2 AND optionType = $3 AND strikePrice = $4 AND contractId = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesContractBySymbolV3Partition = """
        DELETE FROM securities.futures_contract_by_symbol WHERE symbol = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string DeleteFuturesOptionContractBySymbolV2Partition = """
        DELETE FROM securities.futures_option_contract_by_symbol WHERE symbol = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetOnTheRunFuturesContract = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract_by_symbol WHERE symbol = $1 AND rollover = true AND onTheRun = true ORDER BY lastTradeDate ASC, contractId ASC LIMIT 1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetRolloverFuturesContracts = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract_by_symbol WHERE symbol = $1 AND rollover = true ORDER BY onTheRun DESC, lastTradeDate ASC, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContract = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract WHERE contractId = $1 ORDER BY lastTradeDate DESC, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContractById = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract WHERE contractId = $1 AND symbol = $2 AND lastTradeDate = $3 ORDER BY lastTradeDate DESC, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContracts = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract ORDER BY lastTradeDate DESC, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContractsByIds = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract WHERE contractId = ANY($1) AND symbol = $2 ORDER BY lastTradeDate DESC, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesContractsBySymbol = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", lastTradeDate AS "LastTradeDate", onTheRun AS "OnTheRun", rollover AS "Rollover", referencePayload AS "ReferencePayload" FROM securities.futures_contract_by_symbol WHERE symbol = $1 ORDER BY lastTradeDate DESC, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesOptionContract = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", contractMonth AS "ContractMonth", strikePrice AS "StrikePrice", optionType AS "OptionType", referencePayload AS "ReferencePayload" FROM securities.futures_option_contract WHERE contractId = $1 ORDER BY contractMonth, optionType, strikePrice, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesOptionContractsByIds = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", contractMonth AS "ContractMonth", strikePrice AS "StrikePrice", optionType AS "OptionType", referencePayload AS "ReferencePayload" FROM securities.futures_option_contract WHERE contractId = ANY($1) ORDER BY contractMonth, optionType, strikePrice, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesOptionContracts = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", contractMonth AS "ContractMonth", strikePrice AS "StrikePrice", optionType AS "OptionType", referencePayload AS "ReferencePayload" FROM securities.futures_option_contract ORDER BY contractMonth, optionType, strikePrice, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string GetFuturesOptionContractsBySymbol = """
        SELECT contractId AS "ContractId", description AS "Description", symbol AS "Symbol", localSymbol AS "LocalSymbol", securityType AS "SecurityType", currency AS "Currency", exchange AS "Exchange", multiplier AS "Multiplier", contractMonth AS "ContractMonth", strikePrice AS "StrikePrice", optionType AS "OptionType", referencePayload AS "ReferencePayload" FROM securities.futures_option_contract_by_symbol WHERE symbol = $1 ORDER BY contractMonth, optionType, strikePrice, contractId;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string InsertFuturesContract = """
        INSERT INTO securities.futures_contract ( contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, onTheRun, rollover, referencePayload ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 ) ON CONFLICT (contractId, symbol, lastTradeDate) DO UPDATE SET description = EXCLUDED.description, localSymbol = EXCLUDED.localSymbol, securityType = EXCLUDED.securityType, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange, multiplier = EXCLUDED.multiplier, onTheRun = EXCLUDED.onTheRun, rollover = EXCLUDED.rollover, referencePayload = EXCLUDED.referencePayload;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string InsertFuturesContractBySymbolV3 = """
        INSERT INTO securities.futures_contract_by_symbol ( contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, lastTradeDate, onTheRun, rollover, referencePayload ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 ) ON CONFLICT (symbol, rollover, onTheRun, lastTradeDate, contractId) DO UPDATE SET description = EXCLUDED.description, localSymbol = EXCLUDED.localSymbol, securityType = EXCLUDED.securityType, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange, multiplier = EXCLUDED.multiplier, referencePayload = EXCLUDED.referencePayload;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string InsertFuturesOptionContract = """
        INSERT INTO securities.futures_option_contract ( contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType, referencePayload ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 ) ON CONFLICT (contractId, contractMonth, symbol, optionType, strikePrice) DO UPDATE SET description = EXCLUDED.description, localSymbol = EXCLUDED.localSymbol, securityType = EXCLUDED.securityType, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange, multiplier = EXCLUDED.multiplier, referencePayload = EXCLUDED.referencePayload;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    public const string InsertFuturesOptionContractBySymbolV2 = """
        INSERT INTO securities.futures_option_contract_by_symbol ( contractId, description, symbol, localSymbol, securityType, currency, exchange, multiplier, contractMonth, strikePrice, optionType, referencePayload ) VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 ) ON CONFLICT (symbol, contractMonth, optionType, strikePrice, contractId) DO UPDATE SET description = EXCLUDED.description, localSymbol = EXCLUDED.localSymbol, securityType = EXCLUDED.securityType, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange, multiplier = EXCLUDED.multiplier, referencePayload = EXCLUDED.referencePayload;
        """;
}
