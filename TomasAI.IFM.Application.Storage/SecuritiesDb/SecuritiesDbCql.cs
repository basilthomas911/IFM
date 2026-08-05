using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

internal class SecuritiesDbCql
{
    public const string DeleteFuturesContract = """
        DELETE FROM futures_contract
        WHERE contractId = :contractId;
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

    public const string DeleteFuturesOptionContractById = """
        DELETE FROM futures_option_contract
        WHERE contractId = :contractId
        AND contractMonth = :contractMonth
        AND symbol = :symbol
        AND optionType = :optionType
        AND strikePrice = :strikePrice;
        """;

    public const string DeleteFuturesContractBySymbolV2 = """
        DELETE FROM futures_contract_by_symbol_v2
        WHERE symbol = :symbol
        AND currentlyTraded = :currentlyTraded
        AND lastTradeDate = :lastTradeDate
        AND contractId = :contractId;
        """;

    public const string DeleteFuturesOptionContractBySymbolV2 = """
        DELETE FROM futures_option_contract_by_symbol_v2
        WHERE symbol = :symbol
        AND contractMonth = :contractMonth
        AND optionType = :optionType
        AND strikePrice = :strikePrice
        AND contractId = :contractId;
        """;

    public const string DeleteFuturesContractBySymbolV2Partition = """
        DELETE FROM futures_contract_by_symbol_v2
        WHERE symbol = :symbol;
        """;

    public const string DeleteFuturesOptionContractBySymbolV2Partition = """
        DELETE FROM futures_option_contract_by_symbol_v2
        WHERE symbol = :symbol;
        """;

    public const string DeleteSecuritiesProjectionStateV3 = """
        DELETE FROM securities_projection_state_v3
        WHERE projectionName = :projectionName;
        """;

    public const string DeleteSecuritiesSymbolProjectionStateV3 = """
        DELETE FROM securities_symbol_projection_state_v3
        WHERE projectionName = :projectionName
        AND symbol = :symbol;
        """;

    public const string GetFuturesContractProjectionSourceKeys = """
        SELECT
            symbol,
            currentlyTraded,
            lastTradeDate,
            contractId
        FROM futures_contract;
        """;

    public const string GetFuturesContractProjectionTargetKeys = """
        SELECT
            symbol,
            currentlyTraded,
            lastTradeDate,
            contractId
        FROM futures_contract_by_symbol_v2;
        """;

    public const string GetFuturesOptionContractProjectionSourceKeys = """
        SELECT
            symbol,
            contractMonth,
            optionType,
            strikePrice,
            contractId
        FROM futures_option_contract;
        """;

    public const string GetFuturesOptionContractProjectionTargetKeys = """
        SELECT
            symbol,
            contractMonth,
            optionType,
            strikePrice,
            contractId
        FROM futures_option_contract_by_symbol_v2;
        """;

    public const string GetSecuritiesProjectionStateV3 = """
        SELECT generation, completed, activeOperations
        FROM securities_projection_state_v3
        WHERE projectionName = :projectionName;
        """;

    public const string GetSecuritiesSymbolProjectionStateV3 = """
        SELECT generation, completed, activeOperations
        FROM securities_symbol_projection_state_v3
        WHERE projectionName = :projectionName
        AND symbol = :symbol;
        """;

    public const string GetSecuritiesSymbolProjectionStatesV3 = """
        SELECT symbol, generation, completed, activeOperations
        FROM securities_symbol_projection_state_v3
        WHERE projectionName = :projectionName
        AND symbol IN :symbols;
        """;

    public const string GetSecuritiesProjectionOperationsV3 = """
        SELECT operationId, startedOn, stateMayBeActive
        FROM securities_projection_operation_v3
        WHERE projectionName = :projectionName;
        """;

    public const string GetSecuritiesProjectionOperationScopesV3 = """
        SELECT scopeType, scopeKey
        FROM securities_projection_operation_scope_v3
        WHERE projectionName = :projectionName
        AND operationId = :operationId;
        """;

    public const string InsertSecuritiesProjectionOperationV3 = """
        INSERT INTO securities_projection_operation_v3 (
            projectionName, operationId, startedOn, stateMayBeActive)
        VALUES (:projectionName, :operationId, :startedOn, false);
        """;

    public const string SetSecuritiesProjectionOperationStateMayBeActiveV3 = """
        UPDATE securities_projection_operation_v3
        SET stateMayBeActive = :stateMayBeActive
        WHERE projectionName = :projectionName
        AND operationId = :operationId
        IF stateMayBeActive = :expectedStateMayBeActive;
        """;

    public const string InsertSecuritiesProjectionOperationScopeV3 = """
        INSERT INTO securities_projection_operation_scope_v3 (
            projectionName, operationId, scopeType, scopeKey)
        VALUES (:projectionName, :operationId, :scopeType, :scopeKey);
        """;

    public const string DeleteSecuritiesProjectionOperationV3 = """
        DELETE FROM securities_projection_operation_v3
        WHERE projectionName = :projectionName
        AND operationId = :operationId;
        """;

    public const string DeleteSecuritiesProjectionOperationScopesV3 = """
        DELETE FROM securities_projection_operation_scope_v3
        WHERE projectionName = :projectionName
        AND operationId = :operationId;
        """;

    public const string InvalidateSecuritiesProjectionStateV3 = """
        UPDATE securities_projection_state_v3
        SET generation = :generation,
            completed = false
        WHERE projectionName = :projectionName;
        """;

    public const string BeginSecuritiesProjectionOperationV3 = """
        UPDATE securities_projection_state_v3
        SET generation = :generation,
            completed = false,
            activeOperations = activeOperations + :activeOperations
        WHERE projectionName = :projectionName;
        """;

    public const string EndSecuritiesProjectionOperationV3 = """
        UPDATE securities_projection_state_v3
        SET generation = :generation,
            completed = false,
            activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName;
        """;

    public const string RemoveSecuritiesProjectionOperationV3 = """
        DELETE activeOperations[:operationId]
        FROM securities_projection_state_v3
        WHERE projectionName = :projectionName;
        """;

    public const string CompleteSecuritiesProjectionOperationV3 = """
        UPDATE securities_projection_state_v3
        SET completed = true,
            activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName
        IF generation = :generation
        AND activeOperations = :expectedActiveOperations;
        """;

    public const string BeginSecuritiesSymbolProjectionOperationV3 = """
        UPDATE securities_symbol_projection_state_v3
        SET generation = :generation,
            completed = false,
            activeOperations = activeOperations + :activeOperations
        WHERE projectionName = :projectionName
        AND symbol = :symbol;
        """;

    public const string EndSecuritiesSymbolProjectionOperationV3 = """
        UPDATE securities_symbol_projection_state_v3
        SET generation = :generation,
            completed = false,
            activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName
        AND symbol = :symbol;
        """;

    public const string RemoveSecuritiesSymbolProjectionOperationV3 = """
        DELETE activeOperations[:operationId]
        FROM securities_symbol_projection_state_v3
        WHERE projectionName = :projectionName
        AND symbol = :symbol;
        """;

    public const string CompleteSecuritiesSymbolProjectionOperationV3 = """
        UPDATE securities_symbol_projection_state_v3
        SET completed = true,
            activeOperations = activeOperations - :activeOperations
        WHERE projectionName = :projectionName
        AND symbol = :symbol
        IF generation = :generation
        AND activeOperations = :expectedActiveOperations;
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
        FROM futures_contract_by_symbol_v2
        WHERE symbol = :symbol
        AND currentlyTraded = true
        ORDER BY lastTradeDate DESC, contractId ASC
        LIMIT 1;
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
        FROM futures_contract_by_symbol_v2
        WHERE symbol = :symbol
        AND currentlyTraded = true;
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
        FROM futures_contract_by_symbol_v2
        WHERE symbol = :symbol;
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

    public const string GetFuturesOptionContractsByIds = """
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
        WHERE contractId IN :contractIds;
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

    public const string GetFuturesOptionContractsBySymbol = """
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
        FROM futures_option_contract_by_symbol_v2
        WHERE symbol = :symbol;
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

    public const string InsertFuturesContractBySymbolV2 = """
        INSERT INTO futures_contract_by_symbol_v2 (
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
        );
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

    public const string InsertFuturesOptionContractBySymbolV2 = """
        INSERT INTO futures_option_contract_by_symbol_v2 (
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
