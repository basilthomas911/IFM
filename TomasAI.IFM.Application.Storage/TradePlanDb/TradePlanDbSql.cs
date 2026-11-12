namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>SQL statements and query templates for TradePlan storage.</summary>
public static class TradePlanDbSql
{
    /// <summary>SQL for insert activity.</summary>
    internal const string InsertActivity =
        """
        INSERT INTO trade_plan.position_trade_plan_activity_by_date (valuedate,calculatedatutc,portfolioid,fundid,orderid,
            tradeid,positionid,strategykind,planrevision,state,requiresexit,contenthash,payload)
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13) ON CONFLICT DO NOTHING;
        """;

    /// <summary>SQL for insert exit workflow.</summary>
    internal const string InsertExitWorkflow =
        """
        INSERT INTO trade_plan.position_exit_workflow (portfolioid,fundid,orderid,tradeid,positionid,valuedate,
            updatedatutc,exitdecisionid,stagerevision,strategykind,state,sourceplaneventid,payload)
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
        ON CONFLICT (portfolioid,fundid,orderid,tradeid,positionid,valuedate,updatedatutc,exitdecisionid,
            stagerevision) DO UPDATE SET payload=trade_plan.position_exit_workflow.payload
        WHERE trade_plan.position_exit_workflow.payload=EXCLUDED.payload RETURNING stagerevision;
        """;

    /// <summary>SQL for insert plan.</summary>
    internal const string InsertPlan =
        """
        INSERT INTO {0} (portfolioid,fundid,orderid,tradeid,positionid,valuedate,planrevision,calculatedatutc,state,
            requiresexit,contenthash,payload)
        VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
        ON CONFLICT (portfolioid,fundid,orderid,tradeid,positionid,valuedate,
            planrevision) DO UPDATE SET contenthash={0}.contenthash
        WHERE {0}.contenthash=EXCLUDED.contenthash RETURNING contenthash;
        """;

    /// <summary>SQL for lock exit workflow baseline.</summary>
    internal const string LockExitWorkflowBaseline =
        """
        LOCK TABLE trade_plan.position_exit_workflow IN SHARE MODE;
        """;

    /// <summary>SQL for offset history chart template.</summary>
    internal const string OffsetHistoryChartTemplate =
        """
        SELECT DISTINCT ON (date_bin(INTERVAL '1 minute',calculatedatutc,timestamptz '2000-01-01')) calculatedatutc,
            combinedspreadprice,CASE WHEN calculationversion = 'IronCondor/EntryCreditForwardPnl/v2' THEN -combinedforwardprice ELSE combinedforwardprice END
        FROM {0}
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6
            AND ($7::bigint IS NULL OR planrevision <= $7) AND calculatedatutc >= $8 AND calculatedatutc <= $9
            AND combinedspreadprice IS NOT NULL
        ORDER BY date_bin(INTERVAL '1 minute',calculatedatutc,timestamptz '2000-01-01'),calculatedatutc DESC,planrevision DESC;
        """;

    /// <summary>SQL for offset history metadata template.</summary>
    internal const string OffsetHistoryMetadataTemplate =
        """
        SELECT COUNT(*),COALESCE(MAX(planrevision),0),MAX(calculatedatutc)
        FROM {0}
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6
            AND ($7::bigint IS NULL OR planrevision <= $7);
        """;

    /// <summary>SQL for offset history rows template.</summary>
    internal const string OffsetHistoryRowsTemplate =
        """
        SELECT payload
        FROM {0}
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6
            AND ($7::bigint IS NULL OR planrevision <= $7)
        ORDER BY planrevision DESC OFFSET $8 LIMIT $9;
        """;

    /// <summary>SQL for read committed exit workflow evidence.</summary>
    internal const string ReadCommittedExitWorkflowEvidence =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,
               e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_stream_id s
        JOIN event_log e ON e.eventstreamid=s.eventstreamid
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE s.eventstream=$1 AND e.commandid=$2
        ORDER BY e.streamversion DESC
        LIMIT 2;
        """;

    /// <summary>SQL for read exit workflow baseline.</summary>
    internal const string ReadExitWorkflowBaseline =
        """
        SELECT payload
        FROM trade_plan.position_exit_workflow
        ORDER BY portfolioid,fundid,orderid,tradeid,positionid,valuedate,updatedatutc,exitdecisionid,stagerevision;
        """;

    /// <summary>SQL for read latest exit workflow owner source.</summary>
    internal const string ReadLatestExitWorkflowOwnerSource =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,
               e.commandid,e.eventtimestamp::text,e.streamversion
        FROM event_stream_id s
        JOIN event_log e ON e.eventstreamid=s.eventstreamid AND e.streamversion=s.currentversion
        JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE s.eventstream=$1;
        """;

    /// <summary>SQL for read page template.</summary>
    internal const string ReadPageTemplate =
        """
        SELECT payload,{0} FROM {1} WHERE {2} ORDER BY {3} LIMIT {4};
        """;

    /// <summary>Reads iron condor trade plan rows.</summary>
    internal const string TradePlanDbContextExtensions_ReadChartRangeAsync_1 =
        """
        SELECT DISTINCT ON (date_bin(INTERVAL '15 minutes',calculatedatutc,timestamptz '2000-01-01'))
            calculatedatutc,combinedspreadprice,
            CASE WHEN calculationversion = 'IronCondor/EntryCreditForwardPnl/v2' THEN -combinedforwardprice ELSE combinedforwardprice END
        FROM trade_plan.iron_condor_trade_plan
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5
            AND valuedate >= $6 AND valuedate <= $7 AND calculatedatutc >= $8 AND calculatedatutc <= $9
            AND combinedspreadprice IS NOT NULL
        ORDER BY date_bin(INTERVAL '15 minutes',calculatedatutc,timestamptz '2000-01-01'),calculatedatutc DESC,planrevision DESC;
        """;

    /// <summary>Reads unnest rows.</summary>
    internal const string TradePlanDbContextExtensions_ReadDailyForwardSpreadsAsync_1 =
        """
        SELECT day.valuedate, observation.forwardspread
        FROM unnest($6::date[], $7::timestamptz[]) AS day(valuedate, endutc)
        CROSS JOIN LATERAL (
            SELECT CASE WHEN calculationversion = 'IronCondor/EntryCreditForwardPnl/v2'
                THEN -combinedforwardprice ELSE combinedforwardprice END AS forwardspread
            FROM trade_plan.iron_condor_trade_plan
            WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5
                AND valuedate=day.valuedate AND calculatedatutc <= day.endutc
            ORDER BY calculatedatutc DESC, planrevision DESC
            LIMIT 1
        ) observation;
        """;

    /// <summary>SQL for update scalars.</summary>
    internal const string UpdateScalars =
        """
        UPDATE trade_plan.iron_condor_trade_plan
        SET sequenceid=$1,actiondate=$2,tradedate=$3,maturitydate=$4,tradetype=$5,actiontype=$6,actionsubtype=$7,
            actionstate=$8,actionreason=$9,tradepnl=$10,forwardlossratio=$11,lossprobability=$12,mscore=$13,maxprofit=$14,
            maxloss=$15,minprofittarget=$16,dailyprofittarget=$17,assetprice=$18,assetstddev=$19,assetmean=$20,
            assetpricechange=$21,markettrend=$22,marketvolatility=$23,marketdirection=$24,volatilityprice=$25,
            traderisk=$26,fiftydayma=$27,fivedayxma=$28,putotmprobability=$29,callotmprobability=$30,shortputgamma=$31,
            shortcallgamma=$32,gammarisk=$33,netprice=$34,forwardprice=$35,forwarddelta=$36,stoplosslimit=$37,
            trendtype=$38,trendstrength=$39,rsi=$40,rsislope=$41,tdi=$42,tdistrength=$43,createdon=$44,createdby=$45,
            sourceeventid=$46,positionsequence=$47,routegeneration=$48,calculationversion=$49,inputstatus=$50,
            actiondatetime=$51,dailypnl=$52,dailyloss=$53,dailylosslimit=$54,lossheadroom=$55,forwarddailypnl=$56,
            forwardloss=$57,forwardlossheadroom=$58,projectedpositionvaluechange=$59,estimatedexitcommission=$60,
            estimatedexitslippage=$61,estimatedexitcosts=$62,putspreadprice=$63,callspreadprice=$64,
            combinedspreadprice=$65,putforwardprice=$66,callforwardprice=$67,combinedforwardprice=$68,
            currentpositionvalue=$69,forwardpositionvalue=$70,estimatedclosevalue=$71,netdelta=$72,netgamma=$73,
            netvega=$74,nettheta=$75,underlyingprice=$76,distancetoshortput=$77,distancetoshortcall=$78,
            timetoexpirydays=$79,scenariohorizonseconds=$80,scenariounderlyingprice=$81,scenariounderlyingmove=$82,
            scenariovolatilityshift=$83,scenariokind=$84,pricingmodelversion=$85,oldestquoteatutc=$86,
            riskvaliduntilutc=$87,calculationstatus=$88,exitrecommended=$89,exitreason=$90,quantity=$91,
            contractmultiplier=$92,currency=$93,riskparametersetid=$94,riskparametersetversion=$95,
            riskparametersethash=$96,snapshotpayload=$97
        WHERE portfolioid=$98 AND fundid=$99 AND orderid=$100 AND tradeid=$101 AND positionid=$102 AND valuedate=$103
            AND planrevision=$104;
        """;
}
