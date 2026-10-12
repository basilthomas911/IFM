namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Native PostgreSQL lookup queries. Ordering is explicit and insert replay is idempotent.</summary>
internal static class ReferenceDbSql
{
    const string Columns = "lookupTypeName, shortCode, orderId, description, createdOn, createdBy";
    public const string DeleteLookupType = "DELETE FROM reference.lookup_type WHERE lookupTypeName=$1 AND orderId=$2;";
    public const string GetLookupTypeById = "SELECT " + Columns + " FROM reference.lookup_type WHERE lookupTypeName=$1 AND orderId=$2 ORDER BY shortCode;";
    public const string GetLookupType = "SELECT " + Columns + " FROM reference.lookup_type WHERE lookupTypeName=$1 ORDER BY orderId, shortCode;";
    public const string GetLookupTypes = "SELECT " + Columns + " FROM reference.lookup_type ORDER BY lookupTypeName, orderId, shortCode;";
    public const string GetLookupTypeNames = "SELECT DISTINCT lookupTypeName FROM reference.lookup_type ORDER BY lookupTypeName;";
    public const string GetLookupTypeShortCodes = "SELECT shortCode, orderId FROM reference.lookup_type WHERE lookupTypeName=$1 ORDER BY orderId, shortCode;";
    public const string InsertLookupType = """
        INSERT INTO reference.lookup_type (lookupTypeName, shortCode, orderId, description, createdOn, createdBy)
        VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT (lookupTypeName,orderId,shortCode) DO UPDATE SET
        description=EXCLUDED.description,createdOn=EXCLUDED.createdOn,createdBy=EXCLUDED.createdBy;
        """;

    public const string GetTradeStrategyFamilies = "SELECT tradeStrategyFamilyId,definitionVersion,systemKey,family,strategy,timeFrame,symbol,currency,description,state,createdOnUtc,createdBy FROM reference.trade_strategy_family WHERE catalog=$1 ORDER BY tradeStrategyFamilyId,definitionVersion;";
    public const string InsertTradeStrategyFamily = "INSERT INTO reference.trade_strategy_family (catalog,tradeStrategyFamilyId,definitionVersion,systemKey,family,strategy,timeFrame,symbol,currency,description,state,createdOnUtc,createdBy) VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13) ON CONFLICT (catalog,systemKey,definitionVersion) DO NOTHING;";
    public const string GetTradeStrategyFamilyCatalog = "SELECT revision,payload_json FROM reference.trade_strategy_family_catalog WHERE catalog=$1;";
    public const string InitializeTradeStrategyFamilyCatalog = "INSERT INTO reference.trade_strategy_family_catalog(catalog,revision,payload_json) VALUES($1,$2,$3) ON CONFLICT (catalog) DO NOTHING;";
    public const string UpdateTradeStrategyFamilyCatalog = "UPDATE reference.trade_strategy_family_catalog SET revision=$1,payload_json=$2 WHERE catalog=$3 AND revision=$4;";

    public const string GetMDIForwardLossRatios = """
        SELECT trendDirection AS "TrendDirection", tradeType AS "TradeType", mdi AS "MDI", forwardLossRatio AS "ForwardLossRatio", createdBy AS "CreatedBy", createdOn AS "CreatedOn", updatedBy AS "UpdatedBy", updatedOn AS "UpdatedOn" FROM reference.mdi_forward_loss_ratio WHERE trendDirection = $1 AND tradeType = $2 ORDER BY mdi;
        """;

    public const string InsertMDIForwardLossRatio = """
        INSERT INTO reference.mdi_forward_loss_ratio (mdi, trendDirection, tradeType, forwardLossRatio, createdBy, createdOn, updatedBy, updatedOn) VALUES ($1, $2, $3, $4, $5, $6, $7, $8) ON CONFLICT (trendDirection,tradeType,mdi) DO UPDATE SET forwardLossRatio=EXCLUDED.forwardLossRatio,createdBy=EXCLUDED.createdBy,createdOn=EXCLUDED.createdOn,updatedBy=EXCLUDED.updatedBy,updatedOn=EXCLUDED.updatedOn;
        """;

    public const string DeleteMDIForwardLossRatio = """
        DELETE FROM reference.mdi_forward_loss_ratio WHERE trendDirection = $1 AND tradeType = $2;
        """;
}
