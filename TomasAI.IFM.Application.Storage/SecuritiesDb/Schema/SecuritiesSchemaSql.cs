namespace TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

/// <summary>Native PostgreSQL schema with explicit ordered indexes. No runtime CQL translation.</summary>
public static class SecuritiesSchemaSql
{
    public const string Create = """
        CREATE SCHEMA IF NOT EXISTS securities;
        
        CREATE TABLE IF NOT EXISTS securities.option_pricing_reference_bundle (bundle_id text PRIMARY KEY, payload text);
        
        CREATE TABLE IF NOT EXISTS securities.option_contract_expiry_calendar (
            symbol text,
            generation uuid,
            expiryDate date,
            providerRoot text,
            contractId text,
            underlyingContractId text,
            optionFamily text,
            definitionPayload bytea,
            refreshedAtUtc timestamptz,
            PRIMARY KEY (symbol, generation, expiryDate, providerRoot, contractId)
            );
        CREATE INDEX IF NOT EXISTS option_contract_expiry_calendar_ordered ON securities.option_contract_expiry_calendar (symbol, generation, expiryDate ASC, providerRoot ASC, contractId ASC);
        
        CREATE TABLE IF NOT EXISTS securities.option_contract_expiry_calendar_state (
            symbol text PRIMARY KEY,
            generation uuid,
            coverageFrom date,
            coverageThrough date,
            refreshedAtUtc timestamptz
            );
        
        CREATE TABLE IF NOT EXISTS securities.securities_reference_identity (identity_key text PRIMARY KEY, binding text);
        
        CREATE TABLE IF NOT EXISTS securities.securities_reference_version (contract_id text, version text, digest text, payload bytea, published boolean,
                    PRIMARY KEY (contract_id,version));
        
        CREATE TABLE IF NOT EXISTS securities.futures_contract_rollover (
            symbol text PRIMARY KEY,
            contractId text,
            nextRolloverDate date,
            updatedOn timestamptz,
            updatedBy text,
            createdOn timestamptz,
            createdBy text
            );
        
        CREATE TABLE IF NOT EXISTS securities.futures_contract (
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
            PRIMARY KEY (contractId, symbol, lastTradeDate)
            );
        CREATE INDEX IF NOT EXISTS futures_contract_ordered ON securities.futures_contract (contractId, symbol ASC, lastTradeDate DESC);
        
        CREATE TABLE IF NOT EXISTS securities.futures_option_contract (
            contractId text,
            description text,
            symbol text,
            localSymbol text,
            securityType text,
            currency text,
            exchange text,
            multiplier text,
            contractMonth date,
            strikePrice double precision,
            optionType text,
            PRIMARY KEY (contractId,contractMonth, symbol, optionType, strikePrice)
            );
        
        CREATE TABLE IF NOT EXISTS securities.futures_contract_by_symbol (
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
            PRIMARY KEY (symbol, rollover, onTheRun, lastTradeDate, contractId)
            );
        CREATE INDEX IF NOT EXISTS futures_contract_by_symbol_ordered ON securities.futures_contract_by_symbol (symbol, rollover DESC, onTheRun DESC, lastTradeDate ASC, contractId ASC);
        
        CREATE TABLE IF NOT EXISTS securities.futures_option_contract_by_symbol (
            symbol text,
            contractMonth date,
            contractId text,
            description text,
            localSymbol text,
            securityType text,
            currency text,
            exchange text,
            multiplier text,
            strikePrice double precision,
            optionType text,
            PRIMARY KEY (symbol, contractMonth, optionType, strikePrice, contractId)
            );
        CREATE INDEX IF NOT EXISTS futures_option_contract_by_symbol_ordered ON securities.futures_option_contract_by_symbol (symbol, contractMonth DESC, optionType ASC, strikePrice ASC, contractId ASC);
        
        CREATE TABLE IF NOT EXISTS securities.securities_projection_state (
            projectionName text,
            generation uuid,
            completed boolean,
            activeOperations uuid[],
            PRIMARY KEY (projectionName)
            );
        
        CREATE TABLE IF NOT EXISTS securities.securities_symbol_projection_state (
            projectionName text,
            symbol text,
            generation uuid,
            completed boolean,
            activeOperations uuid[],
            PRIMARY KEY (projectionName, symbol)
            );
        
        CREATE TABLE IF NOT EXISTS securities.securities_projection_operation (
            projectionName text,
            operationId uuid,
            startedOn timestamptz,
            stateMayBeActive boolean,
            PRIMARY KEY (projectionName, operationId)
            );
        
        CREATE TABLE IF NOT EXISTS securities.securities_projection_operation_scope (
            projectionName text,
            operationId uuid,
            scopeType text,
            scopeKey text,
            PRIMARY KEY (projectionName, operationId, scopeType, scopeKey)
            );
        
        ALTER TABLE securities.option_contract_expiry_calendar ADD COLUMN IF NOT EXISTS underlyingContractId text;
        
        ALTER TABLE securities.option_contract_expiry_calendar ADD COLUMN IF NOT EXISTS definitionPayload bytea;
        
        ALTER TABLE securities.futures_contract ADD COLUMN IF NOT EXISTS referencePayload bytea;
        
        ALTER TABLE securities.futures_contract_by_symbol ADD COLUMN IF NOT EXISTS referencePayload bytea;
        
        ALTER TABLE securities.futures_option_contract ADD COLUMN IF NOT EXISTS referencePayload bytea;
        
        ALTER TABLE securities.futures_option_contract_by_symbol ADD COLUMN IF NOT EXISTS referencePayload bytea;
        """;
}
