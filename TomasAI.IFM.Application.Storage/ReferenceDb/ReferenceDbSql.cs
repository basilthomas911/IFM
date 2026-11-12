namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>SQL statements and query templates for Reference storage.</summary>
public static class ReferenceDbSql
{
    /// <summary>SQL for complete instrument definition selection.</summary>
    internal const string CompleteInstrumentDefinitionSelection =
        """
        INSERT INTO reference.instrument_definition_selection_status(snapshot_id,complete)
        VALUES($1,TRUE) ON CONFLICT(snapshot_id) DO UPDATE SET complete=TRUE;
        """;

    /// <summary>SQL for count orphan scheduled job days.</summary>
    internal const string CountOrphanScheduledJobDays =
        """
        SELECT COUNT(*)
        FROM reference.scheduled_job_days d
        WHERE NOT EXISTS(SELECT 1 FROM reference.scheduled_job j WHERE j.jobid=d.jobid);
        """;

    /// <summary>SQL for count scheduled jobs.</summary>
    internal const string CountScheduledJobs =
        """
        SELECT COUNT(*) FROM reference.scheduled_job;
        """;

    /// <summary>SQL for delete lookup by id.</summary>
    internal const string DeleteLookupById =
        """
        DELETE FROM reference.lookup_type WHERE lookuptypename=$1 AND orderid=$2;
        """;

    /// <summary>SQL for delete lookup partition.</summary>
    internal const string DeleteLookupPartition =
        """
        DELETE FROM reference.lookup_type WHERE lookuptypename=$1;
        """;

    /// <summary>SQL for delete lookup type.</summary>
    internal const string DeleteLookupType =
        """
        DELETE FROM reference.lookup_type WHERE lookuptypename=$1 AND orderid=$2;
        """;

    /// <summary>SQL for delete mdiforward loss ratio.</summary>
    internal const string DeleteMDIForwardLossRatio =
        """
        DELETE FROM reference.mdi_forward_loss_ratio WHERE trenddirection = $1 AND tradetype = $2;
        """;

    /// <summary>SQL for delete optional scheduled job days.</summary>
    internal const string DeleteOptionalScheduledJobDays =
        """
        DELETE FROM reference.scheduled_job_days WHERE jobid=$1;
        """;

    /// <summary>SQL for delete scheduled job.</summary>
    internal const string DeleteScheduledJob =
        """
        DELETE FROM reference.scheduled_job WHERE jobid=$1;
        """;

    /// <summary>SQL for delete scheduled job days.</summary>
    internal const string DeleteScheduledJobDays =
        """
        DELETE FROM reference.scheduled_job_days WHERE jobid=$1;
        """;

    /// <summary>SQL for get contract descriptions.</summary>
    internal const string GetContractDescriptions =
        """
        SELECT contract,MIN(description) FROM reference.product_description
        WHERE exchange=$1 AND family=1 AND symbol=$2 AND contract<>'' AND resolved
        GROUP BY contract HAVING COUNT(DISTINCT description)=1;
        """;

    /// <summary>SQL for get instrument definition json.</summary>
    internal const string GetInstrumentDefinitionJson =
        """
        SELECT definition_json
        FROM reference.instrument_definition
        WHERE snapshot_id=$1 AND dataset=$2 AND bucket=$3 AND record_index=$4;
        """;

    /// <summary>SQL for get instrument definition selection page.</summary>
    internal const string GetInstrumentDefinitionSelectionPage =
        """
        SELECT instrument_key,payload
        FROM reference.instrument_definition_selection
        WHERE snapshot_id=$1 AND dataset=$2 AND root=$3 AND options=$4 AND instrument_key>$5
        ORDER BY instrument_key LIMIT $6;
        """;

    /// <summary>SQL for get instrument definition selection status.</summary>
    internal const string GetInstrumentDefinitionSelectionStatus =
        """
        SELECT complete FROM reference.instrument_definition_selection_status WHERE snapshot_id=$1;
        """;

    /// <summary>SQL for get instrument definition snapshot.</summary>
    internal const string GetInstrumentDefinitionSnapshot =
        """
        SELECT snapshot_id,completed_utc,record_count,datasets_json
        FROM reference.instrument_definition_snapshot WHERE catalog='current';
        """;

    /// <summary>SQL for get instrument definition symbols.</summary>
    internal const string GetInstrumentDefinitionSymbols =
        """
        SELECT p.symbol_id,p.symbol,p.currency,p.exchange,
          COALESCE((SELECT MIN(d.description) FROM reference.product_description d
            WHERE d.exchange=p.exchange AND d.family=p.family AND d.symbol=p.symbol
              AND d.contract='' AND d.resolved
            HAVING COUNT(DISTINCT d.description)=1),'')
        FROM reference.instrument_definition_product p WHERE p.snapshot_id=$1 AND p.family=$2
        ORDER BY p.symbol,p.exchange,p.currency;
        """;

    /// <summary>SQL for get lookup type.</summary>
    internal const string GetLookupType =
        """
        SELECT lookuptypename, shortcode, orderid, description, createdon, createdby
        FROM reference.lookup_type WHERE lookuptypename=$1 ORDER BY orderid, shortcode;
        """;

    /// <summary>SQL for get lookup type by id.</summary>
    internal const string GetLookupTypeById =
        """
        SELECT lookuptypename, shortcode, orderid, description, createdon, createdby
        FROM reference.lookup_type WHERE lookuptypename=$1 AND orderid=$2 ORDER BY shortcode;
        """;

    /// <summary>SQL for get lookup type names.</summary>
    internal const string GetLookupTypeNames =
        """
        SELECT DISTINCT lookuptypename FROM reference.lookup_type ORDER BY lookuptypename;
        """;

    /// <summary>SQL for get lookup type short codes.</summary>
    internal const string GetLookupTypeShortCodes =
        """
        SELECT shortcode, orderid FROM reference.lookup_type WHERE lookuptypename=$1 ORDER BY orderid, shortcode;
        """;

    /// <summary>SQL for get lookup types.</summary>
    internal const string GetLookupTypes =
        """
        SELECT lookuptypename, shortcode, orderid, description, createdon, createdby
        FROM reference.lookup_type ORDER BY lookuptypename, orderid, shortcode;
        """;

    /// <summary>SQL for get mdiforward loss ratios.</summary>
    internal const string GetMDIForwardLossRatios =
        """
        SELECT trenddirection AS "TrendDirection", tradetype AS "TradeType", mdi AS "MDI",
            forwardlossratio AS "ForwardLossRatio", createdby AS "CreatedBy", createdon AS "CreatedOn",
            updatedby AS "UpdatedBy", updatedon AS "UpdatedOn"
        FROM reference.mdi_forward_loss_ratio WHERE trenddirection = $1 AND tradetype = $2 ORDER BY mdi;
        """;

    /// <summary>SQL for get scheduled job id.</summary>
    internal const string GetScheduledJobId =
        """
        SELECT jobid FROM reference.scheduled_job WHERE jobname COLLATE "C"=$1;
        """;

    /// <summary>SQL for get trade strategy families.</summary>
    internal const string GetTradeStrategyFamilies =
        """
        SELECT tradestrategyfamilyid,definitionversion,systemkey,family,strategy,timeframe,symbol,currency,description,
            state,createdonutc,createdby
        FROM reference.trade_strategy_family WHERE catalog=$1 ORDER BY tradestrategyfamilyid,definitionversion;
        """;

    /// <summary>SQL for get trade strategy family catalog.</summary>
    internal const string GetTradeStrategyFamilyCatalog =
        """
        SELECT revision,payload_json FROM reference.trade_strategy_family_catalog WHERE catalog=$1;
        """;

    /// <summary>SQL for get trade strategy product.</summary>
    internal const string GetTradeStrategyProduct =
        """
        SELECT id,symbol,currency,exchange
        FROM reference.trade_strategy_symbol WHERE family=$1 AND exchange=$2 AND symbol=$3 AND currency=$4;
        """;

    /// <summary>SQL for get trade strategy symbol.</summary>
    internal const string GetTradeStrategySymbol =
        """
        SELECT id,symbol,currency,exchange FROM reference.trade_strategy_symbol WHERE family=$1 AND id=$2;
        """;

    /// <summary>SQL for has instrument catalog receipt.</summary>
    internal const string HasInstrumentCatalogReceipt =
        """
        SELECT EXISTS (
            SELECT 1 FROM public.event_log l JOIN public.event_stream_id s USING(eventstreamid)
            WHERE s.eventstream=$1 AND l.commandid=$2);
        """;

    /// <summary>SQL for has instrument definition selection.</summary>
    internal const string HasInstrumentDefinitionSelection =
        """
        SELECT EXISTS (SELECT 1 FROM reference.instrument_definition_selection WHERE snapshot_id=$1);
        """;

    /// <summary>SQL for initialize trade strategy family catalog.</summary>
    internal const string InitializeTradeStrategyFamilyCatalog =
        """
        INSERT INTO reference.trade_strategy_family_catalog(catalog,revision,payload_json)
        VALUES($1,$2,$3) ON CONFLICT (catalog) DO NOTHING;
        """;

    /// <summary>SQL for insert accepted lookup.</summary>
    internal const string InsertAcceptedLookup =
        """
        INSERT INTO reference.lookup_type (lookuptypename,shortcode,orderid,description,createdon,createdby)
        VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT (lookuptypename,orderid,shortcode)
        DO UPDATE SET description=EXCLUDED.description,createdon=EXCLUDED.createdon,createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert instrument definition.</summary>
    internal const string InsertInstrumentDefinition =
        """
        INSERT INTO reference.instrument_definition (snapshot_id,dataset,bucket,record_index,publisher_id,instrument_id,
            raw_symbol,asset,instrument_class,currency,exchange,definition_json)

        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
        ON CONFLICT(snapshot_id,dataset,bucket,record_index) DO UPDATE SET publisher_id=EXCLUDED.publisher_id,
            instrument_id=EXCLUDED.instrument_id,raw_symbol=EXCLUDED.raw_symbol,asset=EXCLUDED.asset,
            instrument_class=EXCLUDED.instrument_class,currency=EXCLUDED.currency,exchange=EXCLUDED.exchange,
            definition_json=EXCLUDED.definition_json;
        """;

    /// <summary>SQL for insert instrument definition product.</summary>
    internal const string InsertInstrumentDefinitionProduct =
        """
        INSERT INTO reference.instrument_definition_product(snapshot_id,family,symbol,exchange,currency,symbol_id)
        VALUES($1,$2,$3,$4,$5,$6)
        ON CONFLICT(snapshot_id,family,symbol,exchange,currency) DO UPDATE SET symbol_id=EXCLUDED.symbol_id;
        """;

    /// <summary>SQL for insert instrument definition selection.</summary>
    internal const string InsertInstrumentDefinitionSelection =
        """
        INSERT INTO reference.instrument_definition_selection(snapshot_id,dataset,root,options,instrument_key,payload)
        VALUES($1,$2,$3,$4,$5,$6)
        ON CONFLICT(snapshot_id,dataset,root,options,instrument_key) DO UPDATE SET payload=EXCLUDED.payload;
        """;

    /// <summary>SQL for insert lookup type.</summary>
    internal const string InsertLookupType =
        """
        INSERT INTO reference.lookup_type (lookuptypename, shortcode, orderid, description, createdon, createdby)
        VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT (lookuptypename,orderid,shortcode) DO UPDATE SET
        description=EXCLUDED.description,createdon=EXCLUDED.createdon,createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert mdiforward loss ratio.</summary>
    internal const string InsertMDIForwardLossRatio =
        """
        INSERT INTO reference.mdi_forward_loss_ratio (mdi, trenddirection, tradetype, forwardlossratio, createdby,
            createdon, updatedby, updatedon)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
        ON CONFLICT (trenddirection,tradetype,mdi) DO UPDATE SET forwardlossratio=EXCLUDED.forwardlossratio,
            createdby=EXCLUDED.createdby,createdon=EXCLUDED.createdon,updatedby=EXCLUDED.updatedby,updatedon=EXCLUDED.updatedon;
        """;

    /// <summary>SQL for insert scheduled job.</summary>
    internal const string InsertScheduledJob =
        """
        INSERT INTO reference.scheduled_job(jobid,jobname,jobschedule,jobscheduledate,jobscheduleinterval,taskname,
            taskenabled,createdon,createdby,updatedon,updatedby)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11);
        """;

    /// <summary>SQL for insert trade strategy family.</summary>
    internal const string InsertTradeStrategyFamily =
        """
        INSERT INTO reference.trade_strategy_family (catalog,tradestrategyfamilyid,definitionversion,systemkey,family,
            strategy,timeframe,symbol,currency,description,state,createdonutc,createdby)
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
        ON CONFLICT (catalog,systemkey,definitionversion) DO NOTHING;
        """;

    /// <summary>SQL for insert trade strategy product.</summary>
    internal const string InsertTradeStrategyProduct =
        """
        INSERT INTO reference.trade_strategy_symbol (family,exchange,symbol,currency,id)
        VALUES ($1,$2,$3,$4,$5) ON CONFLICT(family,exchange,symbol,currency) DO NOTHING;
        """;

    /// <summary>SQL for lock instrument catalog.</summary>
    internal const string LockInstrumentCatalog =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1, 0));
        """;

    /// <summary>SQL for lock lookup.</summary>
    internal const string LockLookup =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1,0));
        """;

    /// <summary>SQL for lock scheduled job.</summary>
    internal const string LockScheduledJob =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1,0));
        """;

    /// <summary>SQL for publish instrument definition snapshot.</summary>
    internal const string PublishInstrumentDefinitionSnapshot =
        """
        INSERT INTO reference.instrument_definition_snapshot(catalog,snapshot_id,completed_utc,record_count,datasets_json)
        VALUES('current',$1,$2,$3,$4)
        ON CONFLICT(catalog) DO UPDATE SET snapshot_id=EXCLUDED.snapshot_id,completed_utc=EXCLUDED.completed_utc,
            record_count=EXCLUDED.record_count,datasets_json=EXCLUDED.datasets_json;
        """;

    /// <summary>SQL for read instrument catalog event.</summary>
    internal const string ReadInstrumentCatalogEvent =
        """
        SELECT n.eventtypename, l.eventversion, l.eventpayload
        FROM public.event_log l JOIN public.event_stream_id s USING(eventstreamid)
        JOIN public.event_name_id n USING(eventnameid)
        WHERE s.eventstream=$1 AND l.streamversion=$2;
        """;

    /// <summary>SQL for read instrument catalog revision.</summary>
    internal const string ReadInstrumentCatalogRevision =
        """
        SELECT currentversion FROM public.event_stream_id WHERE eventstream=$1;
        """;

    /// <summary>SQL for read instrument definition json.</summary>
    internal const string ReadInstrumentDefinitionJson =
        """
        SELECT definition_json
        FROM reference.instrument_definition
        WHERE snapshot_id=$1 AND dataset=$2 AND bucket=$3 ORDER BY record_index;
        """;

    /// <summary>SQL for read lookup survivors.</summary>
    internal const string ReadLookupSurvivors =
        """
        SELECT lookuptypename,shortcode,orderid,description,createdon,createdby FROM reference.lookup_type
        WHERE lookuptypename=$1 AND orderid<>$2 ORDER BY orderid,shortcode;
        """;

    /// <summary>SQL for read previous instrument completion.</summary>
    internal const string ReadPreviousInstrumentCompletion =
        """
        SELECT completed_utc FROM reference.instrument_definition_snapshot WHERE catalog='current';
        """;

    /// <summary>SQL for read scheduled jobs.</summary>
    internal const string ReadScheduledJobs =
        """
        SELECT j.jobid,j.jobname,j.jobschedule,j.jobscheduledate,j.jobscheduleinterval,j.taskname,j.taskenabled,
        j.createdon,j.createdby,j.updatedon,j.updatedby,d.jobid,d.monday,d.tuesday,d.wednesday,d.thursday,d.friday,
            d.saturday,d.sunday
        FROM reference.scheduled_job j
        LEFT JOIN reference.scheduled_job_days d ON d.jobid=j.jobid
        ORDER BY j.jobid;
        """;

    /// <summary>SQL for update scheduled job.</summary>
    internal const string UpdateScheduledJob =
        """
        UPDATE reference.scheduled_job
        SET jobname=$2,jobschedule=$3,jobscheduledate=$4,jobscheduleinterval=$5,taskname=$6,
        taskenabled=$7,createdon=$8,createdby=$9,updatedon=$10,updatedby=$11 WHERE jobid=$1;
        """;

    /// <summary>SQL for update trade strategy family catalog.</summary>
    internal const string UpdateTradeStrategyFamilyCatalog =
        """
        UPDATE reference.trade_strategy_family_catalog
        SET revision=$1,payload_json=$2 WHERE catalog=$3 AND revision=$4;
        """;

    /// <summary>SQL for upsert scheduled job days.</summary>
    internal const string UpsertScheduledJobDays =
        """
        INSERT INTO reference.scheduled_job_days(jobid,monday,tuesday,wednesday,thursday,friday,saturday,sunday)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8)
        ON CONFLICT(jobid) DO UPDATE SET monday=EXCLUDED.monday,tuesday=EXCLUDED.tuesday,
        wednesday=EXCLUDED.wednesday,thursday=EXCLUDED.thursday,friday=EXCLUDED.friday,saturday=EXCLUDED.saturday,
            sunday=EXCLUDED.sunday;
        """;
}
