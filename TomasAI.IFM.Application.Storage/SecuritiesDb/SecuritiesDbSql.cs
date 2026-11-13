namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>SQL statements and query templates for Securities storage.</summary>
public static class SecuritiesDbSql
{
    /// <summary>SQL for catalog projection has rows.</summary>
    internal const string CatalogProjectionHasRows =
        """
        SELECT EXISTS (SELECT FROM securities.futures_contract UNION ALL SELECT FROM securities.futures_contract_by_symbol
            UNION ALL SELECT FROM securities.futures_option_contract UNION ALL SELECT FROM securities.futures_option_contract_by_symbol);
        """;

    /// <summary>Clears only derived catalog rows inside the rebuild transaction.</summary>
    internal const string ClearSymbolProjections =
        """
        DELETE FROM securities.futures_contract_by_symbol;
        DELETE FROM securities.futures_option_contract_by_symbol;
        """;

    /// <summary>Reads option pricing reference bundle rows.</summary>
    internal const string DbContextExtensions_GetOptionPricingReferenceBundlePayloadCoreAsync_1 =
        """
        SELECT payload FROM securities.option_pricing_reference_bundle WHERE bundle_id=$1;
        """;

    /// <summary>Reads securities reference version rows.</summary>
    internal const string DbContextExtensions_ListReferenceVersionsCoreAsync_1 =
        """
        SELECT version,published
        FROM securities.securities_reference_version
        WHERE contract_id=$1 AND version>$2 ORDER BY version COLLATE "C" LIMIT $3;
        """;

    /// <summary>Reads securities reference version rows.</summary>
    internal const string DbContextExtensions_ReadReferenceVersionAsync_1 =
        """
        SELECT digest,published,payload
        FROM securities.securities_reference_version WHERE contract_id=$1 AND version=$2;
        """;

    /// <summary>Deletes futures contract by symbol rows.</summary>
    internal const string DbContextExtensions_RemoveFuturesContractAsyncCoreBinding_1 =
        """
        DELETE FROM securities.futures_contract_by_symbol WHERE contractid=$1;
        """;

    /// <summary>Deletes futures contract rows.</summary>
    internal const string DbContextExtensions_RemoveFuturesContractAsyncCoreBinding_2 =
        """
        DELETE FROM securities.futures_contract WHERE contractid=$1;
        """;

    /// <summary>Deletes futures option contract by symbol rows.</summary>
    internal const string DbContextExtensions_RemoveOptionContractAsyncCoreBinding_1 =
        """
        DELETE FROM securities.futures_option_contract_by_symbol WHERE contractid=$1;
        """;

    /// <summary>Deletes futures option contract rows.</summary>
    internal const string DbContextExtensions_RemoveOptionContractAsyncCoreBinding_2 =
        """
        DELETE FROM securities.futures_option_contract WHERE contractid=$1;
        """;

    /// <summary>Reads securities reference identity rows.</summary>
    internal const string DbContextExtensions_ResolveOptionReferenceIdentitiesCoreAsync_1 =
        """
        SELECT identity_key,binding FROM securities.securities_reference_identity
        WHERE identity_key IN (SELECT jsonb_array_elements_text($1::jsonb));
        """;

    /// <summary>Reads unnest rows.</summary>
    internal const string DbContext_GetManyAsync_1 =
        """
        SELECT v.contract_id, v.version, v.digest, v.published, v.payload
        FROM unnest($1::text[], $2::text[]) AS requested(contract_id, version)
        JOIN securities.securities_reference_version v USING (contract_id, version);
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesContract =
        """
        DELETE FROM securities.futures_contract WHERE contractid = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesContractById =
        """
        DELETE FROM securities.futures_contract WHERE contractid = $1 AND symbol = $2 AND lasttradedate = $3;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesContractBySymbolV3 =
        """
        DELETE
        FROM securities.futures_contract_by_symbol
        WHERE symbol = $1 AND rollover = $2 AND ontherun = $3 AND lasttradedate = $4 AND contractid = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesContractBySymbolV3Partition =
        """
        DELETE FROM securities.futures_contract_by_symbol WHERE symbol = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesOptionContract =
        """
        DELETE FROM securities.futures_option_contract WHERE contractid = $1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesOptionContractById =
        """
        DELETE
        FROM securities.futures_option_contract
        WHERE contractid = $1 AND contractmonth = $2 AND symbol = $3 AND optiontype = $4 AND strikeprice = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesOptionContractBySymbolV2 =
        """
        DELETE
        FROM securities.futures_option_contract_by_symbol
        WHERE symbol = $1 AND contractmonth = $2 AND optiontype = $3 AND strikeprice = $4 AND contractid = $5;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string DeleteFuturesOptionContractBySymbolV2Partition =
        """
        DELETE FROM securities.futures_option_contract_by_symbol WHERE symbol = $1;
        """;

    /// <summary>SQL for get cached option contract definitions.</summary>
    internal const string GetCachedOptionContractDefinitions =
        """
        SELECT symbol,underlyingcontractid,expirydate,providerroot,optionfamily,definitionpayload,refreshedatutc
        FROM securities.option_contract_expiry_calendar WHERE symbol=$1 AND generation=$2 AND expirydate=$3
        ORDER BY providerroot,contractid;
        """;

    /// <summary>SQL for get cached option definition partitions.</summary>
    internal const string GetCachedOptionDefinitionPartitions =
        """
        SELECT symbol,underlyingcontractid,expirydate,providerroot,optionfamily,definitionpayload,refreshedatutc
        FROM securities.option_contract_expiry_calendar
        WHERE symbol=$1 AND generation=$2 AND expirydate=ANY($3::date[])
        ORDER BY expirydate,providerroot,contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContract =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract WHERE contractid = $1 ORDER BY lasttradedate DESC, contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContractById =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract
        WHERE contractid = $1 AND symbol = $2 AND lasttradedate = $3 ORDER BY lasttradedate DESC, contractid;
        """;

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesContractProjectionSourceKeys =
        """
        SELECT symbol, rollover, ontherun, lasttradedate, contractid FROM securities.futures_contract;
        """;

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesContractProjectionTargetKeys =
        """
        SELECT symbol, rollover, ontherun, lasttradedate, contractid FROM securities.futures_contract_by_symbol;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContractRollover =
        """
        SELECT symbol, contractid, nextrolloverdate, updatedon, updatedby, createdon, createdby
        FROM securities.futures_contract_rollover WHERE symbol = $1 ORDER BY symbol;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContractRollovers =
        """
        SELECT symbol, contractid, nextrolloverdate, updatedon, updatedby, createdon, createdby
        FROM securities.futures_contract_rollover ORDER BY symbol;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContracts =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract ORDER BY lasttradedate DESC, contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContractsByIds =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract
        WHERE contractid = ANY($1) AND symbol = $2 ORDER BY lasttradedate DESC, contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesContractsBySymbol =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract_by_symbol WHERE symbol = $1 ORDER BY lasttradedate DESC, contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesOptionContract =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", contractmonth AS "ContractMonth", strikeprice AS "StrikePrice",
            optiontype AS "OptionType", referencepayload AS "ReferencePayload"
        FROM securities.futures_option_contract
        WHERE contractid = $1 ORDER BY contractmonth, optiontype, strikeprice, contractid;
        """;

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesOptionContractProjectionSourceKeys =
        """
        SELECT symbol, contractmonth, optiontype, strikeprice, contractid
        FROM securities.futures_option_contract;
        """;

    /// <summary>Reads native reconciliation keys without CQL projection journals.</summary>
    internal const string GetFuturesOptionContractProjectionTargetKeys =
        """
        SELECT symbol, contractmonth, optiontype, strikeprice, contractid
        FROM securities.futures_option_contract_by_symbol;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesOptionContracts =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", contractmonth AS "ContractMonth", strikeprice AS "StrikePrice",
            optiontype AS "OptionType", referencepayload AS "ReferencePayload"
        FROM securities.futures_option_contract ORDER BY contractmonth, optiontype, strikeprice, contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesOptionContractsByIds =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", contractmonth AS "ContractMonth", strikeprice AS "StrikePrice",
            optiontype AS "OptionType", referencepayload AS "ReferencePayload"
        FROM securities.futures_option_contract
        WHERE contractid = ANY($1) ORDER BY contractmonth, optiontype, strikeprice, contractid;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetFuturesOptionContractsBySymbol =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", contractmonth AS "ContractMonth", strikeprice AS "StrikePrice",
            optiontype AS "OptionType", referencepayload AS "ReferencePayload"
        FROM securities.futures_option_contract_by_symbol
        WHERE symbol = $1 ORDER BY contractmonth, optiontype, strikeprice, contractid;
        """;

    const string NativeOptionPageSelect =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", contractmonth AS "ContractMonth", strikeprice AS "StrikePrice",
            optiontype AS "OptionType", referencepayload AS "ReferencePayload"
        FROM securities.futures_option_contract_by_symbol WHERE symbol = $1
        """;

    const string NativeOptionPageOrder =
        """ORDER BY contractmonth,optiontype COLLATE "C",strikeprice,contractid COLLATE "C" """;

    /// <summary>Reads the first option catalog page in cursor order.</summary>
    internal const string GetNativeOptionContractsFirstPage =
        NativeOptionPageSelect + "\n" + NativeOptionPageOrder + "LIMIT $2;";

    /// <summary>Reads a page strictly after the last key returned to the caller.</summary>
    internal const string GetNativeOptionContractsContinuationPage =
        NativeOptionPageSelect + "\n" +
        """AND (contractmonth,optiontype COLLATE "C",strikeprice,contractid COLLATE "C") > ($2,$3,$4,$5)""" +
        "\n" + NativeOptionPageOrder + "LIMIT $6;";

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetOnTheRunFuturesContract =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract_by_symbol
        WHERE symbol = $1 AND rollover = TRUE AND ontherun = TRUE
        ORDER BY lasttradedate ASC, contractid ASC LIMIT 1;
        """;

    /// <summary>SQL for get option contract expiries.</summary>
    internal const string GetOptionContractExpiries =
        """
        SELECT symbol,underlyingcontractid,expirydate,providerroot,optionfamily,refreshedatutc
        FROM securities.option_contract_expiry_calendar
        WHERE symbol=$1 AND generation=$2 AND expirydate>=$3 AND expirydate<=$4
        ORDER BY expirydate,providerroot,contractid;
        """;

    /// <summary>SQL for get option contract expiry calendar state.</summary>
    internal const string GetOptionContractExpiryCalendarState =
        """
        SELECT generation,coveragefrom,coveragethrough,refreshedatutc
        FROM securities.option_contract_expiry_calendar_state WHERE symbol=$1;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string GetRolloverFuturesContracts =
        """
        SELECT contractid AS "ContractId", description AS "Description", symbol AS "Symbol",
            localsymbol AS "LocalSymbol", securitytype AS "SecurityType", currency AS "Currency", exchange AS "Exchange",
            multiplier AS "Multiplier", lasttradedate AS "LastTradeDate", ontherun AS "OnTheRun", rollover AS "Rollover",
            referencepayload AS "ReferencePayload"
        FROM securities.futures_contract_by_symbol
        WHERE symbol = $1 AND rollover = TRUE ORDER BY ontherun DESC, lasttradedate ASC, contractid;
        """;

    /// <summary>Rejects ambiguous canonical IDs before modifying either projection.</summary>
    internal const string HasAmbiguousCanonicalContractIds =
        """
        SELECT EXISTS (SELECT contractid FROM securities.futures_contract GROUP BY contractid HAVING COUNT(*) > 1)
            OR EXISTS (SELECT contractid FROM securities.futures_option_contract GROUP BY contractid HAVING COUNT(*) > 1);
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string InsertFuturesContract =
        """
        INSERT INTO securities.futures_contract ( contractid, description, symbol, localsymbol, securitytype, currency,
            exchange, multiplier, lasttradedate, ontherun, rollover, referencepayload )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 )
        ON CONFLICT (contractid, symbol, lasttradedate) DO UPDATE SET description = EXCLUDED.description,
            localsymbol = EXCLUDED.localsymbol, securitytype = EXCLUDED.securitytype, currency = EXCLUDED.currency,
            exchange = EXCLUDED.exchange, multiplier = EXCLUDED.multiplier, ontherun = EXCLUDED.ontherun,
            rollover = EXCLUDED.rollover, referencepayload = EXCLUDED.referencepayload;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string InsertFuturesContractBySymbolV3 =
        """
        INSERT INTO securities.futures_contract_by_symbol ( contractid, description, symbol, localsymbol, securitytype,
            currency, exchange, multiplier, lasttradedate, ontherun, rollover, referencepayload )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 )
        ON CONFLICT (symbol, rollover, ontherun, lasttradedate,
            contractid) DO UPDATE SET description = EXCLUDED.description, localsymbol = EXCLUDED.localsymbol,
            securitytype = EXCLUDED.securitytype, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange,
            multiplier = EXCLUDED.multiplier, referencepayload = EXCLUDED.referencepayload;
        """;

    /// <summary>SQL for insert futures contract rollover if missing.</summary>
    internal const string InsertFuturesContractRolloverIfMissing =
        """
        INSERT INTO securities.futures_contract_rollover(symbol,createdon,createdby) VALUES($1,$2,$3)
        ON CONFLICT(symbol) DO NOTHING;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string InsertFuturesOptionContract =
        """
        INSERT INTO securities.futures_option_contract ( contractid, description, symbol, localsymbol, securitytype,
            currency, exchange, multiplier, contractmonth, strikeprice, optiontype, referencepayload )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 )
        ON CONFLICT (contractid, contractmonth, symbol, optiontype,
            strikeprice) DO UPDATE SET description = EXCLUDED.description, localsymbol = EXCLUDED.localsymbol,
            securitytype = EXCLUDED.securitytype, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange,
            multiplier = EXCLUDED.multiplier, referencepayload = EXCLUDED.referencepayload;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string InsertFuturesOptionContractBySymbolV2 =
        """
        INSERT INTO securities.futures_option_contract_by_symbol ( contractid, description, symbol, localsymbol,
            securitytype, currency, exchange, multiplier, contractmonth, strikeprice, optiontype, referencepayload )
        VALUES ( $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12 )
        ON CONFLICT (symbol, contractmonth, optiontype, strikeprice,
            contractid) DO UPDATE SET description = EXCLUDED.description, localsymbol = EXCLUDED.localsymbol,
            securitytype = EXCLUDED.securitytype, currency = EXCLUDED.currency, exchange = EXCLUDED.exchange,
            multiplier = EXCLUDED.multiplier, referencepayload = EXCLUDED.referencepayload;
        """;

    /// <summary>Inserts every definition for a single immutable generation in one command.</summary>
    internal const string InsertOptionDefinitionGenerationRows =
        """
        INSERT INTO securities.option_contract_expiry_calendar
            (symbol,generation,expirydate,providerroot,contractid,underlyingcontractid,optionfamily,definitionpayload,
            refreshedatutc)
        SELECT $1,$2,definition.expiry,definition.root,definition.contract_id,definition.underlying_id,definition.family,
            definition.payload,$9
        FROM unnest($3::date[],$4::text[],$5::text[],$6::text[],$7::text[],$8::bytea[])
            AS definition(expiry,root,contract_id,underlying_id,family,payload);
        """;

    /// <summary>SQL for lock baseline tables.</summary>
    internal const string LockBaselineTables =
        """
        LOCK TABLE public.event_stream_id, public.event_log,
            securities.futures_contract, securities.futures_option_contract, securities.futures_contract_rollover
            IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>SQL for lock rebuild tables.</summary>
    internal const string LockRebuildTables =
        """
        LOCK TABLE public.event_stream_id, public.event_log,
            securities.futures_contract, securities.futures_contract_by_symbol,
            securities.futures_option_contract, securities.futures_option_contract_by_symbol
            IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>Fences catalog CRUD and projection reads/writes for one atomic rebuild.</summary>
    internal const string LockSymbolProjectionRebuild =
        """
        LOCK TABLE securities.futures_contract, securities.futures_option_contract,
            securities.futures_contract_by_symbol, securities.futures_option_contract_by_symbol
            IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>Inserts securities reference identity rows.</summary>
    internal const string PostgresReferenceStore_ClaimIdentityAsync_1 =
        """
        INSERT INTO securities.securities_reference_identity(identity_key,binding) VALUES($1,$2)
        ON CONFLICT(identity_key) DO UPDATE SET binding=EXCLUDED.binding
        WHERE securities.securities_reference_identity.binding=EXCLUDED.binding RETURNING identity_key;
        """;

    /// <summary>Inserts option pricing reference bundle rows.</summary>
    internal const string PostgresReferenceStore_InsertBundleAsync_1 =
        """
        INSERT INTO securities.option_pricing_reference_bundle(bundle_id,payload) VALUES($1,$2)
        ON CONFLICT(bundle_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE securities.option_pricing_reference_bundle.payload=EXCLUDED.payload RETURNING bundle_id;
        """;

    /// <summary>Updates securities reference version rows.</summary>
    internal const string PostgresReferenceStore_PublishAsync_1 =
        """
        UPDATE securities.securities_reference_version SET published=TRUE
        WHERE contract_id=$1 AND version=$2 AND digest=$3 RETURNING version;
        """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string PostgresReferenceStore_PublishDefinitionsAsync_1 =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1,0));
        """;

    /// <summary>Inserts option contract expiry calendar state rows.</summary>
    internal const string PostgresReferenceStore_PublishDefinitionsAsync_3 =
        """
        INSERT INTO securities.option_contract_expiry_calendar_state(symbol,generation,coveragefrom,coveragethrough,
            refreshedatutc)
        VALUES($1,$2,$3,$4,$5) ON CONFLICT(symbol) DO UPDATE SET generation=EXCLUDED.generation,
        coveragefrom=EXCLUDED.coveragefrom,coveragethrough=EXCLUDED.coveragethrough,refreshedatutc=EXCLUDED.refreshedatutc;
        """;

    /// <summary>Reads securities reference identity rows.</summary>
    internal const string PostgresReferenceStore_StageAsync_1 =
        """
        SELECT binding FROM securities.securities_reference_identity WHERE identity_key=$1;
        """;

    /// <summary>Inserts securities reference version rows.</summary>
    internal const string PostgresReferenceStore_StageAsync_2 =
        """
        INSERT INTO securities.securities_reference_version(contract_id,version,digest,payload,published)
        VALUES($1,$2,$3,$4,FALSE) ON CONFLICT(contract_id,version) DO UPDATE SET digest=EXCLUDED.digest
        WHERE securities.securities_reference_version.digest=EXCLUDED.digest
          AND securities.securities_reference_version.payload=EXCLUDED.payload RETURNING version;
        """;

    /// <summary>Rebuilds the futures symbol projection, preserving the exact reference payload.</summary>
    internal const string RebuildFuturesSymbolProjection =
        """
        INSERT INTO securities.futures_contract_by_symbol (contractid, description, symbol, localsymbol, securitytype,
            currency, exchange, multiplier, lasttradedate, ontherun, rollover, referencepayload)
        SELECT contractid, description, symbol, localsymbol, securitytype, currency, exchange, multiplier, lasttradedate,
            ontherun, rollover, referencepayload
        FROM securities.futures_contract;
        """;

    /// <summary>Rebuilds the option symbol projection, preserving the exact reference payload.</summary>
    internal const string RebuildOptionSymbolProjection =
        """
        INSERT INTO securities.futures_option_contract_by_symbol (contractid, description, symbol, localsymbol,
            securitytype, currency, exchange, multiplier, contractmonth, strikeprice, optiontype, referencepayload)
        SELECT contractid, description, symbol, localsymbol, securitytype, currency, exchange, multiplier, contractmonth,
            strikeprice, optiontype, referencepayload
        FROM securities.futures_option_contract;
        """;

    /// <summary>Native PostgreSQL securities persistence.</summary>
    internal const string UpdateFuturesContractRollover =
        """
        UPDATE securities.futures_contract_rollover
        SET contractid = $1, nextrolloverdate = $2, updatedon = $3, updatedby = $4 WHERE symbol = $5;
        """;
}
