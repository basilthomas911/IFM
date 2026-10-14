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
    public const string ReadScheduledJobs = """
        SELECT j.jobId,j.jobName,j.jobSchedule,j.jobScheduleDate,j.jobScheduleInterval,j.taskName,j.taskEnabled,
        j.createdOn,j.createdBy,j.updatedOn,j.updatedBy,d.jobId,d.monday,d.tuesday,d.wednesday,d.thursday,d.friday,d.saturday,d.sunday
        FROM reference.scheduled_job j LEFT JOIN reference.scheduled_job_days d ON d.jobId=j.jobId
        """;
    public const string InsertInstrumentDefinition = """

        INSERT INTO reference.instrument_definition (snapshot_id,dataset,bucket,record_index,publisher_id,instrument_id,raw_symbol,asset,instrument_class,currency,exchange,definition_json)

        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) ON CONFLICT(snapshot_id,dataset,bucket,record_index) DO UPDATE SET publisher_id=EXCLUDED.publisher_id,instrument_id=EXCLUDED.instrument_id,raw_symbol=EXCLUDED.raw_symbol,asset=EXCLUDED.asset,instrument_class=EXCLUDED.instrument_class,currency=EXCLUDED.currency,exchange=EXCLUDED.exchange,definition_json=EXCLUDED.definition_json;

        """;
    internal const string CountScheduledJobs = "SELECT count(*) FROM reference.scheduled_job;";
    internal const string CountOrphanScheduledJobDays = "SELECT count(*) FROM reference.scheduled_job_days d WHERE NOT EXISTS(SELECT 1 FROM reference.scheduled_job j WHERE j.jobId=d.jobId);";
    internal const string GetInstrumentDefinitionSnapshot = "SELECT snapshot_id,completed_utc,record_count,datasets_json FROM reference.instrument_definition_snapshot WHERE catalog='current';";
    internal const string GetInstrumentDefinitionSymbols = "SELECT symbol_id,symbol,currency,exchange FROM reference.instrument_definition_product WHERE snapshot_id=$1 AND family=$2 ORDER BY symbol,exchange,currency;";
    internal const string ReadInstrumentDefinitionJson = "SELECT definition_json FROM reference.instrument_definition WHERE snapshot_id=$1 AND dataset=$2 AND bucket=$3 ORDER BY record_index;";
    internal const string InsertInstrumentDefinitionSelection = """
                INSERT INTO reference.instrument_definition_selection(snapshot_id,dataset,root,options,instrument_key,payload)
                VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(snapshot_id,dataset,root,options,instrument_key) DO UPDATE SET payload=EXCLUDED.payload;
                """;
    internal const string InsertInstrumentDefinitionProduct = "INSERT INTO reference.instrument_definition_product(snapshot_id,family,symbol,exchange,currency,symbol_id) VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(snapshot_id,family,symbol,exchange,currency) DO UPDATE SET symbol_id=EXCLUDED.symbol_id;";
    internal const string CompleteInstrumentDefinitionSelection = "INSERT INTO reference.instrument_definition_selection_status(snapshot_id,complete) VALUES($1,true) ON CONFLICT(snapshot_id) DO UPDATE SET complete=true;";
    internal const string PublishInstrumentDefinitionSnapshot = "INSERT INTO reference.instrument_definition_snapshot(catalog,snapshot_id,completed_utc,record_count,datasets_json) VALUES('current',$1,$2,$3,$4) ON CONFLICT(catalog) DO UPDATE SET snapshot_id=EXCLUDED.snapshot_id,completed_utc=EXCLUDED.completed_utc,record_count=EXCLUDED.record_count,datasets_json=EXCLUDED.datasets_json;";
    internal const string GetInstrumentDefinitionSelectionStatus = "SELECT complete FROM reference.instrument_definition_selection_status WHERE snapshot_id=$1;";
    internal const string GetInstrumentDefinitionSelectionPage = "SELECT instrument_key,payload FROM reference.instrument_definition_selection WHERE snapshot_id=$1 AND dataset=$2 AND root=$3 AND options=$4 AND instrument_key>$5 ORDER BY instrument_key LIMIT $6;";
    internal const string GetTradeStrategyProduct = "SELECT id,symbol,currency,exchange FROM reference.trade_strategy_symbol WHERE family=$1 AND exchange=$2 AND symbol=$3 AND currency=$4;";
    internal const string GetTradeStrategySymbol = "SELECT id,symbol,currency,exchange FROM reference.trade_strategy_symbol WHERE family=$1 AND id=$2;";
    internal const string InsertTradeStrategyProduct = "INSERT INTO reference.trade_strategy_symbol (family,exchange,symbol,currency,id) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(family,exchange,symbol,currency) DO NOTHING;";
    internal const string LockLookup = "SELECT pg_advisory_xact_lock(hashtextextended($1,0));";
    internal const string InsertAcceptedLookup = """
            INSERT INTO reference.lookup_type (lookupTypeName,shortCode,orderId,description,createdOn,createdBy)
            VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT (lookupTypeName,orderId,shortCode)
            DO UPDATE SET description=EXCLUDED.description,createdOn=EXCLUDED.createdOn,createdBy=EXCLUDED.createdBy;
            """;
    internal const string DeleteLookupById = "DELETE FROM reference.lookup_type WHERE lookupTypeName=$1 AND orderId=$2;";
    internal const string ReadLookupSurvivors = """
            SELECT lookupTypeName,shortCode,orderId,description,createdOn,createdBy FROM reference.lookup_type
            WHERE lookupTypeName=$1 AND orderId<>$2 ORDER BY orderId,shortCode;
            """;
    internal const string DeleteLookupPartition = "DELETE FROM reference.lookup_type WHERE lookupTypeName=$1;";
    internal const string GetScheduledJobId = "SELECT jobId FROM reference.scheduled_job WHERE jobName COLLATE \"C\"=$1;";
    internal const string LockScheduledJob = "SELECT pg_advisory_xact_lock(hashtextextended($1,0));";
    internal const string InsertScheduledJob = """
            INSERT INTO reference.scheduled_job(jobId,jobName,jobSchedule,jobScheduleDate,jobScheduleInterval,taskName,taskEnabled,createdOn,createdBy,updatedOn,updatedBy)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11);
            """;
    internal const string UpdateScheduledJob = """
            UPDATE reference.scheduled_job SET jobName=$2,jobSchedule=$3,jobScheduleDate=$4,jobScheduleInterval=$5,taskName=$6,
            taskEnabled=$7,createdOn=$8,createdBy=$9,updatedOn=$10,updatedBy=$11 WHERE jobId=$1;
            """;
    internal const string DeleteScheduledJobDays = "DELETE FROM reference.scheduled_job_days WHERE jobId=$1;";
    internal const string DeleteScheduledJob = "DELETE FROM reference.scheduled_job WHERE jobId=$1;";
    internal const string DeleteOptionalScheduledJobDays = "DELETE FROM reference.scheduled_job_days WHERE jobId=$1;";
    internal const string UpsertScheduledJobDays = """
            INSERT INTO reference.scheduled_job_days(jobId,monday,tuesday,wednesday,thursday,friday,saturday,sunday)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8) ON CONFLICT(jobId) DO UPDATE SET monday=EXCLUDED.monday,tuesday=EXCLUDED.tuesday,
            wednesday=EXCLUDED.wednesday,thursday=EXCLUDED.thursday,friday=EXCLUDED.friday,saturday=EXCLUDED.saturday,sunday=EXCLUDED.sunday;
            """;
}
