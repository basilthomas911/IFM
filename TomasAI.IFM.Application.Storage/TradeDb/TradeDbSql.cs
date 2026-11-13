namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>SQL statements and query templates for Trade storage.</summary>
public static class TradeDbSql
{
    /// <summary>SQL for context statement1.</summary>
    internal const string ContextStatement1 =
        """
        SELECT payload,payload_sha256 FROM trade.market_condition_assessment WHERE workflow_id=$1;
        """;

    /// <summary>SQL for context statement2.</summary>
    internal const string ContextStatement2 =
        """
        SELECT payload,content_hash
        FROM trade.risk_management_invocation
        WHERE workflow_id=$1 AND invocation_id=$2 ORDER BY revision DESC LIMIT 1;
        """;

    /// <summary>SQL for context statement4.</summary>
    internal const string ContextStatement4 =
        """
        SELECT payload
        FROM trade.trade_order WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 ORDER BY revision DESC LIMIT 1;
        """;

    /// <summary>SQL for delete active intrinsic time strategy workflow.</summary>
    internal const string DeleteActiveIntrinsicTimeStrategyWorkflow =
        """
        DELETE FROM trade.intrinsic_time_strategy_workflow_active
        WHERE workflowentityid = $1;
        """;

    /// <summary>SQL for delete option leg.</summary>
    internal const string DeleteOptionLeg =
        """
        DELETE FROM trade.option_leg
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for delete option leg by id.</summary>
    internal const string DeleteOptionLegById =
        """
        DELETE FROM trade.option_leg
        WHERE orderid = $1
        AND tradeid = $2
        AND contractid = $3;
        """;

    /// <summary>SQL for delete option leg data.</summary>
    internal const string DeleteOptionLegData =
        """
        DELETE FROM trade.option_leg_data
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for delete option leg data by id.</summary>
    internal const string DeleteOptionLegDataById =
        """
        DELETE FROM trade.option_leg_data
        WHERE orderid = $1
        AND tradeid = $2
        AND valuedate = $3
        AND optionlegid = $4;
        """;

    /// <summary>SQL for delete option trade.</summary>
    internal const string DeleteOptionTrade =
        """
        DELETE FROM trade.option_trade
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for delete option trade spread bar data.</summary>
    internal const string DeleteOptionTradeSpreadBarData =
        """
        DELETE FROM trade.option_trade_spread_bar_data
        WHERE orderid = $1 AND tradeid = $2 AND valuedate = $3 AND tradetype = $4;
        """;

    /// <summary>SQL for delete option trade spread data.</summary>
    internal const string DeleteOptionTradeSpreadData =
        """
        DELETE FROM trade.option_trade_spread_data
        WHERE orderid = $1 AND tradeid = $2 AND valuedate = $3 AND tradetype = $4;
        """;

    /// <summary>SQL for delete trade fill.</summary>
    internal const string DeleteTradeFill =
        """
        DELETE FROM trade.trade_fill
                WHERE orderid = $1
                AND tradeid = $2;
        """;

    /// <summary>SQL for delete trade fill by id.</summary>
    internal const string DeleteTradeFillById =
        """
        DELETE FROM trade.trade_fill
                WHERE orderid = $1
                AND tradeid = $2
                AND filldate = $3;
        """;

    /// <summary>SQL for delete trade fill data.</summary>
    internal const string DeleteTradeFillData =
        """
        DELETE FROM trade.trade_fill_data
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for delete trade limit.</summary>
    internal const string DeleteTradeLimit =
        """
        DELETE FROM trade.trade_limit
        WHERE tradeid = $1;
        """;

    /// <summary>SQL for delete trade limit by trade type.</summary>
    internal const string DeleteTradeLimitByTradeType =
        """
        DELETE FROM trade.trade_limit
        WHERE tradeid = $1
        AND tradetype = $2;
        """;

    /// <summary>SQL for delete trade live feed.</summary>
    internal const string DeleteTradeLiveFeed =
        """
        DELETE FROM trade.trade_live_feed
        WHERE orderid = $1 AND tradeid = $2;
        """;

    /// <summary>SQL for delete trade live feeds.</summary>
    internal const string DeleteTradeLiveFeeds =
        """
        DELETE FROM trade.trade_live_feed
        WHERE orderid = $1;
        """;

    /// <summary>SQL for delete trade placement signal.</summary>
    internal const string DeleteTradePlacementSignal =
        """
        DELETE FROM trade.trade_placement_signal
        WHERE contractid = $1
        AND valuedate = $2;
        """;

    /// <summary>SQL for delete trade placement signal by sequence id.</summary>
    internal const string DeleteTradePlacementSignalBySequenceId =
        """
        DELETE FROM trade.trade_placement_signal
        WHERE contractid = $1
        AND valuedate = $2
        AND sequenceid = $3;
        """;

    /// <summary>SQL for delete trade plan forward loss limit.</summary>
    internal const string DeleteTradePlanForwardLossLimit =
        """
        DELETE FROM trade.trade_plan_forward_loss_limit
        WHERE orderid = $1 AND tradeid = $2 AND valuedate = $3 AND tradetype = $4;
        """;

    /// <summary>SQL for delete trade position.</summary>
    internal const string DeleteTradePosition =
        """
        DELETE FROM trade.trade_position
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for delete trade position by primary key.</summary>
    internal const string DeleteTradePositionByPrimaryKey =
        """
        DELETE FROM trade.trade_position
                WHERE orderid = $1
              AND tradeid = $2
              AND valuedate = $3
              AND tradestatus = $4
              AND daystoexpiry = $5
              AND tradetype = $6;
        """;

    /// <summary>SQL for delete trade position state.</summary>
    internal const string DeleteTradePositionState =
        """
        DELETE FROM trade.trade_position_state
        WHERE orderid = $1
          AND tradeid = $2;
        """;

    /// <summary>SQL for delete trade type limit.</summary>
    internal const string DeleteTradeTypeLimit =
        """
        DELETE FROM trade.trade_type_limit
        WHERE tradeid = $1;
        """;

    /// <summary>Continues established history using the complete stable ordering key.</summary>
    internal const string EstablishedTradeHistoryNextPage =
        """
        SELECT payload, establishedatutc, orderid, tradeid, evidencerevision FROM trade.established_trade_history
        WHERE portfolioid=$1 AND fundid=$2 AND strategykind=$3 AND establishedatutc >= $4 AND establishedatutc <= $5
          AND (establishedatutc < $6 OR (establishedatutc = $6 AND
            (orderid > $7 OR (orderid = $7 AND (tradeid > $8 OR (tradeid = $8 AND evidencerevision < $9))))))
        ORDER BY establishedatutc DESC, orderid, tradeid, evidencerevision DESC LIMIT $10;
        """;

    /// <summary>SQL for get active intrinsic time strategy workflow.</summary>
    internal const string GetActiveIntrinsicTimeStrategyWorkflow =
        """
        SELECT workflowentityid, workflowid, contractid, timeframestartvaluedate, timeperiod,
               currentstage, workflowrevision, lasteventid, stateschemaversion, statepayload,
               startedatutc, updatedatutc
        FROM trade.intrinsic_time_strategy_workflow_active
        WHERE workflowentityid = $1;
        """;

    /// <summary>SQL for get broker account.</summary>
    internal const string GetBrokerAccount =
        """
        SELECT account_definition FROM trade.broker_account_read_model
        WHERE account_alias = $1
        ORDER BY revision DESC LIMIT 1;
        """;

    /// <summary>Reads an ordered, bounded first page of established-trade evidence.</summary>
    internal const string GetEstablishedTradeHistoryFirstPage =
        """
        SELECT payload, establishedatutc, orderid, tradeid, evidencerevision FROM trade.established_trade_history
        WHERE portfolioid=$1 AND fundid=$2 AND strategykind=$3 AND establishedatutc >= $4 AND establishedatutc <= $5
        ORDER BY establishedatutc DESC, orderid, tradeid, evidencerevision DESC LIMIT $6;
        """;

    /// <summary>SQL for get intrinsic time strategy workflow.</summary>
    internal const string GetIntrinsicTimeStrategyWorkflow =
        """
        SELECT workflowid, workflowentityid, workflowdefinitionid, workflowdefinitionversion,
               contractid, timeframestartvaluedate, timeperiod, triggereventid, correlationid,
               status, outcome, currentstage, workflowrevision, lasteventid, stateschemaversion,
               statepayload, stopreasoncode, startedatutc, terminalatutc, updatedatutc
        FROM trade.intrinsic_time_strategy_workflow
        WHERE workflowid = $1;
        """;

    /// <summary>SQL for get intrinsic time strategy workflow start attempts.</summary>
    internal const string GetIntrinsicTimeStrategyWorkflowStartAttempts =
        """
        SELECT workflowentityid, requestedatutc, requestedworkflowid, decision, activeworkflowid,
               startcommandid, triggereventid, activestage, reasoncode, sourceeventid
        FROM trade.intrinsic_time_strategy_workflow_start_attempt
        WHERE workflowentityid = $1 AND requestedatutc < $2
        LIMIT $3;
        """;

    /// <summary>SQL for get intrinsic time strategy workflow timeline.</summary>
    internal const string GetIntrinsicTimeStrategyWorkflowTimeline =
        """
        SELECT workflowid, eventid, workflowentityid, workflowrevision, stage, eventname,
               eventschemaversion, eventpayload, occurredatutc
        FROM trade.intrinsic_time_strategy_workflow_timeline
        WHERE workflowid = $1 AND eventid > $2
        LIMIT $3;
        """;

    /// <summary>SQL for get intrinsic time strategy workflows by entity.</summary>
    internal const string GetIntrinsicTimeStrategyWorkflowsByEntity =
        """
        SELECT workflowentityid, startedatutc, workflowid, status, outcome, currentstage,
               workflowrevision, terminalatutc, stopreasoncode
        FROM trade.intrinsic_time_strategy_workflow_by_entity
        WHERE workflowentityid = $1 AND startedatutc < $2
        LIMIT $3;
        """;

    /// <summary>SQL for get intrinsic time strategy workflows by status day.</summary>
    internal const string GetIntrinsicTimeStrategyWorkflowsByStatusDay =
        """
        SELECT workflowentityid, startedatutc, workflowid, status, outcome, currentstage,
               workflowrevision, terminalatutc, stopreasoncode
        FROM trade.intrinsic_time_strategy_workflow_by_status_day
        WHERE status = $1 AND starteddate = $2
        LIMIT $3;
        """;

    /// <summary>SQL for get last trade plan forward loss ratio.</summary>
    internal const string GetLastTradePlanForwardLossRatio =
        """
        SELECT forwardlossratio AS "ForwardLossRatio", sequenceid AS "SequenceId"
        FROM trade.trade_plan_forward_loss_ratio
        WHERE partitionid = 1
        AND valuedate = $1;
        """;

    /// <summary>SQL for get last trade plans.</summary>
    internal const string GetLastTradePlans =
        """
        SELECT sequenceid AS "SequenceId",
               orderid AS "OrderId",
               tradeid AS "TradeId",
               valuedate AS "ValueDate",
               actiondate AS "ActionDate",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               actiontype AS "ActionType",
               actionsubtype AS "ActionSubType",
               actionstate AS "ActionState",
               actionreason AS "ActionReason",
               tradepnl AS "TradePnl",
               forwardlossratio AS "ForwardLossRatio",
               lossprobability AS "LossProbability",
               mscore AS "MScore",
               maxprofit AS "MaxProfit",
               maxloss AS "MaxLoss",
               minprofittarget AS "MinProfitTarget",
               dailyprofittarget AS "DailyProfitTarget",
               assetprice AS "AssetPrice",
               assetstddev AS "AssetStdDev",
               assetmean AS "AssetMean",
               assetpricechange AS "AssetPriceChange",
               markettrend AS "MarketTrend",
               marketvolatility AS "MarketVolatility",
               marketdirection AS "MarketDirection",
               vixvolatility AS "VixVolatility",
               traderisk AS "TradeRisk",
               fiftydayma AS "FiftyDayMA",
               fivedayxma AS "FiveDayXMA",
               putotmprobability AS "PutOTMProbability",
               callotmprobability AS "CallOTMProbability",
               shortputgamma AS "ShortPutGamma",
               shortcallgamma AS "ShortCallGamma",
               gammarisk AS "GammaRisk",
               netprice AS "NetPrice",
               forwardprice AS "ForwardPrice",
               forwarddelta AS "ForwardDelta",
               stoplosslimit AS "StopLossLimit",
               trendtype AS "TrendType",
               trendstrength AS "TrendStrength",
               rsi AS "RSI",
               rsislope AS "RSISlope",
               tdi AS "TDI",
               tdistrength AS "TDIStrength",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_plan
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for get market condition.</summary>
    internal const string GetMarketCondition =
        """
        SELECT workflowid, workflowentityid, inputworkflowrevision, commandid, sourceeventid,
               fundid, instrumentroot, targethorizon, parametersetid, parametersetversion,
               parameterpayloadsha256, snapshotid, snapshotsha256, tradeability, conditiontype,
               direction, phase, strength, confidence, primaryreasoncode, resultpayload,
               resultpayloadsha256, evaluatedatutc, validuntilutc, marketdataasofutc, completedatutc, updatedatutc,
               volatilitybehavior, liquidityquality, dataquality, upstreamalignment, evidencepayload,
               conflictingevidencepayload, blockingreasonspayload, reasonspayload, summarytext
        FROM trade.market_condition WHERE workflowid = $1;
        """;

    /// <summary>SQL for get market condition history.</summary>
    internal const string GetMarketConditionHistory =
        """
        SELECT workflowid, workflowentityid, inputworkflowrevision, commandid, sourceeventid,
               fundid, instrumentroot, targethorizon, parametersetid, parametersetversion,
               parameterpayloadsha256, snapshotid, snapshotsha256, tradeability, conditiontype,
               direction, phase, strength, confidence, primaryreasoncode, resultpayload,
               resultpayloadsha256, evaluatedatutc, validuntilutc, marketdataasofutc, completedatutc, updatedatutc,
               volatilitybehavior, liquidityquality, dataquality, upstreamalignment, evidencepayload,
               conflictingevidencepayload, blockingreasonspayload, reasonspayload, summarytext
        FROM trade.market_condition_by_fund
        WHERE fundid = $1 AND instrumentroot = $2 AND targethorizon = $3
          AND evaluatedatutc < $4
        LIMIT $5;
        """;

    /// <summary>SQL for get option leg.</summary>
    internal const string GetOptionLeg =
        """
        SELECT
            tradeid AS "TradeId",
            contractid AS "ContractId",
            quantity AS "Quantity",
            strikeprice AS "StrikePrice",
            optionlegtype AS "OptionLegType",
            optionlegaction AS "OptionLegAction",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy",
            updatedon AS "UpdatedOn",
            updatedby AS "UpdatedBy"
        FROM trade.option_leg
        WHERE tradeid = $1
        AND contractid = $2;
        """;

    /// <summary>SQL for get option leg data.</summary>
    internal const string GetOptionLegData =
        """
        SELECT
                orderid AS "OrderId",
                tradeid AS "TradeId",
               tradetype AS "TradeType",
               valuedate AS "ValueDate",
               daystoexpiry AS "DaysToExpiry",
               tradestatus AS "TradeStatus",
               optionlegid AS "OptionLegId",
               bidprice AS "BidPrice",
               askprice AS "AskPrice",
               impliedvolatility AS "ImpliedVolatility",
               delta AS "Delta",
               gamma AS "Gamma",
               theta AS "Theta",
               vega AS "Vega",
               rho AS "Rho",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
        FROM trade.option_leg_data
        WHERE orderid = $1
        AND tradeid = $2
        AND valuedate = $3;
        """;

    /// <summary>SQL for get option leg data all.</summary>
    internal const string GetOptionLegDataAll =
        """
        SELECT  orderid AS "OrderId",
           tradeid AS "TradeId",
           tradetype AS "TradeType",
           valuedate AS "ValueDate",
           daystoexpiry AS "DaysToExpiry",
           tradestatus AS "TradeStatus",
           optionlegid AS "OptionLegId",
           bidprice AS "BidPrice",
           askprice AS "AskPrice",
           impliedvolatility AS "ImpliedVolatility",
           delta AS "Delta",
           gamma AS "Gamma",
           theta AS "Theta",
           vega AS "Vega",
           rho AS "Rho",
           createdon AS "CreatedOn",
           createdby AS "CreatedBy",
           updatedon AS "UpdatedOn",
           updatedby AS "UpdatedBy"
        FROM trade.option_leg_data;
        """;

    /// <summary>SQL for get option leg data by option leg id.</summary>
    internal const string GetOptionLegDataByOptionLegId =
        """
        SELECT tradeid AS "TradeId",
               tradetype AS "TradeType",
               valuedate AS "ValueDate",
               daystoexpiry AS "DaysToExpiry",
               tradestatus AS "TradeStatus",
               optionlegid AS "OptionLegId",
               bidprice AS "BidPrice",
               askprice AS "AskPrice",
               impliedvolatility AS "ImpliedVolatility",
               delta AS "Delta",
               gamma AS "Gamma",
               theta AS "Theta",
               vega AS "Vega",
               rho AS "Rho",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
        FROM trade.option_leg_data
        WHERE tradeid = $1
        AND valuedate = $2
        AND optionlegid = $3;
        """;

    /// <summary>SQL for get option legs.</summary>
    internal const string GetOptionLegs =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            contractid AS "ContractId",
            quantity AS "Quantity",
            strikeprice AS "StrikePrice",
            optionlegtype AS "OptionLegType",
            optionlegaction AS "OptionLegAction",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy",
            updatedon AS "UpdatedOn",
            updatedby AS "UpdatedBy"
        FROM trade.option_leg
        WHERE tradeid = $1;
        """;

    /// <summary>SQL for get option legs all.</summary>
    internal const string GetOptionLegsAll =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            contractid AS "ContractId",
            quantity AS "Quantity",
            strikeprice AS "StrikePrice",
            optionlegtype AS "OptionLegType",
            optionlegaction AS "OptionLegAction",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy",
            updatedon AS "UpdatedOn",
            updatedby AS "UpdatedBy"
        FROM trade.option_leg;
        """;

    /// <summary>SQL for get option legs by order and trade.</summary>
    internal const string GetOptionLegsByOrderAndTrade =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            contractid AS "ContractId",
            quantity AS "Quantity",
            strikeprice AS "StrikePrice",
            optionlegtype AS "OptionLegType",
            optionlegaction AS "OptionLegAction",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy",
            updatedon AS "UpdatedOn",
            updatedby AS "UpdatedBy"
        FROM trade.option_leg
        WHERE orderid = $1
          AND tradeid = $2;
        """;

    /// <summary>SQL for get option trade.</summary>
    internal const string GetOptionTrade =
        """
        SELECT orderid AS "OrderId",
               tradeid AS "TradeId",
               tradestrategy AS "TradeStrategy",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               tradestate AS "TradeState",
               tradeaction AS "TradeAction",
               underlyingcontractid AS "UnderlyingContractId",
               underlyingassettype AS "UnderlyingAssetType",
               isprimarytrade AS "IsPrimaryTrade",
               ishedgetrade AS "IsHedgeTrade",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
        FROM trade.option_trade
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for get option trade spread bar data.</summary>
    internal const string GetOptionTradeSpreadBarData =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            tradetype AS "TradeType",
            valuedate AS "ValueDate",
            bardate AS "BarDate",
            losslimit AS "LossLimit",
            winlimit AS "WinLimit",
            forwardspread AS "ForwardSpread",
            netspread AS "NetSpread"
        FROM trade.option_trade_spread_bar_data
        WHERE orderid = $1
          AND tradeid = $2
          AND valuedate = $3
          AND tradetype = $4
          AND bardate >= $5
          AND bardate <= $6;
        """;

    /// <summary>SQL for get option trade spread bar data all.</summary>
    internal const string GetOptionTradeSpreadBarDataAll =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            tradetype AS "TradeType",
            valuedate AS "ValueDate",
            bardate AS "BarDate",
            losslimit AS "LossLimit",
            winlimit AS "WinLimit",
            forwardspread AS "ForwardSpread",
            netspread AS "NetSpread"
        FROM trade.option_trade_spread_bar_data;
        """;

    /// <summary>SQL for get option trade spread data.</summary>
    internal const string GetOptionTradeSpreadData =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            valuedate AS "ValueDate",
            tradetype AS "TradeType",
            sequenceid AS "SequenceId",
            losslimit AS "LossLimit",
            winlimit AS "WinLimit",
            forwardspread AS "ForwardSpread",
            netspread AS "NetSpread",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy"
        FROM trade.option_trade_spread_data
        WHERE
            orderid = $1
            AND tradeid = $2
            AND valuedate = $3
            AND tradetype = $4
        LIMIT 1;
        """;

    /// <summary>SQL for get option trade spread data all.</summary>
    internal const string GetOptionTradeSpreadDataAll =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            valuedate AS "ValueDate",
            tradetype AS "TradeType",
            sequenceid AS "SequenceId",
            losslimit AS "LossLimit",
            winlimit AS "WinLimit",
            forwardspread AS "ForwardSpread",
            netspread AS "NetSpread",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy"
        FROM trade.option_trade_spread_data;
        """;

    /// <summary>SQL for get option trades.</summary>
    internal const string GetOptionTrades =
        """
        SELECT orderid AS "OrderId",
               tradeid AS "TradeId",
               tradestrategy AS "TradeStrategy",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               tradestate AS "TradeState",
               tradeaction AS "TradeAction",
               underlyingcontractid AS "UnderlyingContractId",
               underlyingassettype AS "UnderlyingAssetType",
               isprimarytrade AS "IsPrimaryTrade",
               ishedgetrade AS "IsHedgeTrade",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
        FROM trade.option_trade
        WHERE orderid = $1;
        """;

    /// <summary>SQL for get option trades all.</summary>
    internal const string GetOptionTradesAll =
        """
        SELECT orderid AS "OrderId",
               tradeid AS "TradeId",
               tradestrategy AS "TradeStrategy",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               tradestate AS "TradeState",
               tradeaction AS "TradeAction",
               underlyingcontractid AS "UnderlyingContractId",
               underlyingassettype AS "UnderlyingAssetType",
               isprimarytrade AS "IsPrimaryTrade",
               ishedgetrade AS "IsHedgeTrade",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
            FROM trade.option_trade;
        """;

    /// <summary>SQL for get regime discovery.</summary>
    internal const string GetRegimeDiscovery =
        """
        SELECT workflowid, workflowentityid, inputworkflowrevision, commandid, sourceeventid,
               sourceeventsequence, status, parameterpayloadsha256, signalsnapshotid, resultpayload,
               resultpayloadsha256, failurecode, failuremessage, reasonspayload, schemaversion,
               terminalatutc, updatedatutc
        FROM trade.regime_discovery
        WHERE workflowid = $1;
        """;

    /// <summary>SQL for get trade fill data.</summary>
    internal const string GetTradeFillData =
        """
        SELECT orderid AS "OrderId",
               tradeid AS "TradeId",
               contractid AS "ContractId",
               filldate AS "FillDate",
               bidprice AS "BidPrice",
               askprice AS "AskPrice",
               commission AS "Commission",
               optionlegaction AS "OptionLegAction",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_fill_data
        WHERE orderid = $1
        AND tradeid = $2
        AND filldate >= $3;
        """;

    /// <summary>SQL for get trade fill data by trade id.</summary>
    internal const string GetTradeFillDataByTradeId =
        """
        SELECT
               orderid AS "OrderId",
               tradeid AS "TradeId",
               contractid AS "ContractId",
               filldate AS "FillDate",
               bidprice AS "BidPrice",
               askprice AS "AskPrice",
               commission AS "Commission",
               optionlegaction AS "OptionLegAction",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_fill_data
        WHERE tradeid = $1;
        """;

    /// <summary>SQL for get trade fills.</summary>
    internal const string GetTradeFills =
        """
        SELECT orderid AS "OrderId",
                       tradeid AS "TradeId",
                       filldate AS "FillDate",
                       fillquantity AS "FillQuantity",
                       createdon AS "CreatedOn",
                       createdby AS "CreatedBy"
                FROM trade.trade_fill
                WHERE orderid = $1
                AND tradeid = $2;
        """;

    /// <summary>SQL for get trade fills all.</summary>
    internal const string GetTradeFillsAll =
        """
        SELECT orderid AS "OrderId",
               tradeid AS "TradeId",
               filldate AS "FillDate",
               fillquantity AS "FillQuantity",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_fill;
        """;

    /// <summary>SQL for get trade history.</summary>
    internal const string GetTradeHistory =
        """
        SELECT orderid AS "OrderId",
               tradeid AS "TradeId",
               valuedate AS "ValueDate",
               tradestatus AS "TradeStatus",
               daystoexpiry AS "DaysToExpiry",
               tradetype AS "TradeType",
               SUM(commission) AS "Commission",
               SUM(netspread) AS "NetSpread",
               SUM(tradepnl) AS "TradePnl"
        FROM trade.trade_position
        WHERE orderid = $1
        GROUP BY orderid, tradeid, valuedate, tradestatus, daystoexpiry, tradetype;
        """;

    /// <summary>SQL for get trade limit.</summary>
    internal const string GetTradeLimit =
        """
        SELECT tradeid AS "TradeId",
               tradetype AS "TradeType",
               riskmargin AS "RiskMargin",
               maxprofit AS "MaxProfit",
               maxloss AS "MaxLoss",
               maxreturn AS "MaxReturn",
               maxlosslimit AS "MaxLossLimit",
               minprofitlimit AS "MinProfitLimit",
               maxprofitlimit AS "MaxProfitLimit",
               minprofittarget AS "MinProfitTarget",
               dailyprofittarget AS "DailyProfitTarget",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
        FROM trade.trade_limit
        WHERE tradeid = $1;
        """;

    /// <summary>SQL for get trade limit all.</summary>
    internal const string GetTradeLimitAll =
        """
        SELECT tradeid AS "TradeId",
               tradetype AS "TradeType",
               riskmargin AS "RiskMargin",
               maxprofit AS "MaxProfit",
               maxloss AS "MaxLoss",
               maxreturn AS "MaxReturn",
               maxlosslimit AS "MaxLossLimit",
               minprofitlimit AS "MinProfitLimit",
               maxprofitlimit AS "MaxProfitLimit",
               minprofittarget AS "MinProfitTarget",
               dailyprofittarget AS "DailyProfitTarget",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy",
               updatedon AS "UpdatedOn",
               updatedby AS "UpdatedBy"
            FROM trade.trade_limit;
        """;

    /// <summary>SQL for get trade live feed.</summary>
    internal const string GetTradeLiveFeed =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            livefeed AS "LiveFeed"
        FROM trade.trade_live_feed
        WHERE orderid = $1
        AND tradeid = $2;
        """;

    /// <summary>SQL for get trade placement signal.</summary>
    internal const string GetTradePlacementSignal =
        """
        SELECT
            contractid AS "ContractId",
            valuedate AS "ValueDate",
            sequenceid AS "SequenceId",
            tradeplacementsignal AS "TradePlacementSignal",
            tradeprice AS "TradePrice",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy"
        FROM trade.trade_placement_signal
        WHERE contractid = $1
          AND valuedate = $2
          LIMIT 1;
        """;

    /// <summary>SQL for get trade plan forward loss limit.</summary>
    internal const string GetTradePlanForwardLossLimit =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            tradetype AS "TradeType",
            valuedate AS "ValueDate",
            limittype AS "LimitType"
        FROM trade.trade_plan_forward_loss_limit
        WHERE orderid = $1 AND tradeid = $2 AND valuedate = $3 AND tradetype = $4;
        """;

    /// <summary>SQL for get trade plan forward loss ratios.</summary>
    internal const string GetTradePlanForwardLossRatios =
        """
        SELECT forwardlossratio AS "ForwardLossRatio"
        FROM trade.trade_plan_forward_loss_ratio
        WHERE partitionid = 1
        AND  valuedate >= $1
        AND valuedate <= $2;
        """;

    /// <summary>SQL for get trade plan stop loss limit.</summary>
    internal const string GetTradePlanStopLossLimit =
        """
        SELECT stoplosslimit AS "StopLossLimit"
        FROM trade.trade_plan
        WHERE orderid = $1 AND tradeid = $2
        LIMIT 1;
        """;

    /// <summary>SQL for get trade plans.</summary>
    internal const string GetTradePlans =
        """
        SELECT sequenceid AS "SequenceId",
               orderid AS "OrderId",
               tradeid AS "TradeId",
               valuedate AS "ValueDate",
               actiondate AS "ActionDate",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               actiontype AS "ActionType",
               actionsubtype AS "ActionSubType",
               actionstate AS "ActionState",
               actionreason AS "ActionReason",
               tradepnl AS "TradePnl",
               forwardlossratio AS "ForwardLossRatio",
               lossprobability AS "LossProbability",
               mscore AS "MScore",
               maxprofit AS "MaxProfit",
               maxloss AS "MaxLoss",
               minprofittarget AS "MinProfitTarget",
               dailyprofittarget AS "DailyProfitTarget",
               assetprice AS "AssetPrice",
               assetstddev AS "AssetStdDev",
               assetmean AS "AssetMean",
               assetpricechange AS "AssetPriceChange",
               markettrend AS "MarketTrend",
               marketvolatility AS "MarketVolatility",
               marketdirection AS "MarketDirection",
               vixvolatility AS "VixVolatility",
               traderisk AS "TradeRisk",
               fiftydayma AS "FiftyDayMA",
               fivedayxma AS "FiveDayXMA",
               putotmprobability AS "PutOTMProbability",
               callotmprobability AS "CallOTMProbability",
               shortputgamma AS "ShortPutGamma",
               shortcallgamma AS "ShortCallGamma",
               gammarisk AS "GammaRisk",
               netprice AS "NetPrice",
               forwardprice AS "ForwardPrice",
               forwarddelta AS "ForwardDelta",
               stoplosslimit AS "StopLossLimit",
               trendtype AS "TrendType",
               trendstrength AS "TrendStrength",
               rsi AS "RSI",
               rsislope AS "RSISlope",
               tdi AS "TDI",
               tdistrength AS "TDIStrength",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_plan
        WHERE orderid = $1;
        """;

    /// <summary>SQL for get trade plans all.</summary>
    internal const string GetTradePlansAll =
        """
        SELECT sequenceid AS "SequenceId",
                   orderid AS "OrderId",
                   tradeid AS "TradeId",
                   valuedate AS "ValueDate",
                   actiondate AS "ActionDate",
                   tradedate AS "TradeDate",
                   maturitydate AS "MaturityDate",
                   tradetype AS "TradeType",
                   actiontype AS "ActionType",
                   actionsubtype AS "ActionSubType",
                   actionstate AS "ActionState",
                   actionreason AS "ActionReason",
                   tradepnl AS "TradePnl",
                   forwardlossratio AS "ForwardLossRatio",
                   lossprobability AS "LossProbability",
                   mscore AS "MScore",
                   maxprofit AS "MaxProfit",
                   maxloss AS "MaxLoss",
                   minprofittarget AS "MinProfitTarget",
                   dailyprofittarget AS "DailyProfitTarget",
                   assetprice AS "AssetPrice",
                   assetstddev AS "AssetStdDev",
                   assetmean AS "AssetMean",
                   assetpricechange AS "AssetPriceChange",
                   markettrend AS "MarketTrend",
                   marketvolatility AS "MarketVolatility",
                   marketdirection AS "MarketDirection",
                   vixvolatility AS "VixVolatility",
                   traderisk AS "TradeRisk",
                   fiftydayma AS "FiftyDayMA",
                   fivedayxma AS "FiveDayXMA",
                   putotmprobability AS "PutOTMProbability",
                   callotmprobability AS "CallOTMProbability",
                   shortputgamma AS "ShortPutGamma",
                   shortcallgamma AS "ShortCallGamma",
                   gammarisk AS "GammaRisk",
                   netprice AS "NetPrice",
                   forwardprice AS "ForwardPrice",
                   forwarddelta AS "ForwardDelta",
                   stoplosslimit AS "StopLossLimit",
                   trendtype AS "TrendType",
                   trendstrength AS "TrendStrength",
                   rsi AS "RSI",
                   rsislope AS "RSISlope",
                   tdi AS "TDI",
                   tdistrength AS "TDIStrength",
                   createdon AS "CreatedOn",
                   createdby AS "CreatedBy"
            FROM trade.trade_plan;
        """;

    /// <summary>SQL for get trade plans by date range.</summary>
    internal const string GetTradePlansByDateRange =
        """
        SELECT sequenceid AS "SequenceId",
               orderid AS "OrderId",
               tradeid AS "TradeId",
               valuedate AS "ValueDate",
               actiondate AS "ActionDate",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               actiontype AS "ActionType",
               actionsubtype AS "ActionSubType",
               actionstate AS "ActionState",
               actionreason AS "ActionReason",
               tradepnl AS "TradePnl",
               forwardlossratio AS "ForwardLossRatio",
               lossprobability AS "LossProbability",
               mscore AS "MScore",
               maxprofit AS "MaxProfit",
               maxloss AS "MaxLoss",
               minprofittarget AS "MinProfitTarget",
               dailyprofittarget AS "DailyProfitTarget",
               assetprice AS "AssetPrice",
               assetstddev AS "AssetStdDev",
               assetmean AS "AssetMean",
               assetpricechange AS "AssetPriceChange",
               markettrend AS "MarketTrend",
               marketvolatility AS "MarketVolatility",
               marketdirection AS "MarketDirection",
               vixvolatility AS "VixVolatility",
               traderisk AS "TradeRisk",
               fiftydayma AS "FiftyDayMA",
               fivedayxma AS "FiveDayXMA",
               putotmprobability AS "PutOTMProbability",
               callotmprobability AS "CallOTMProbability",
               shortputgamma AS "ShortPutGamma",
               shortcallgamma AS "ShortCallGamma",
               gammarisk AS "GammaRisk",
               netprice AS "NetPrice",
               forwardprice AS "ForwardPrice",
               forwarddelta AS "ForwardDelta",
               stoplosslimit AS "StopLossLimit",
               trendtype AS "TrendType",
               trendstrength AS "TrendStrength",
               rsi AS "RSI",
               rsislope AS "RSISlope",
               tdi AS "TDI",
               tdistrength AS "TDIStrength",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_plan
        WHERE orderid = $1
        AND tradeid = $2
        AND valuedate >= $3
        AND valuedate <= $4;
        """;

    /// <summary>SQL for get trade plans by value date.</summary>
    internal const string GetTradePlansByValueDate =
        """
        SELECT sequenceid AS "SequenceId",
               orderid AS "OrderId",
               tradeid AS "TradeId",
               valuedate AS "ValueDate",
               actiondate AS "ActionDate",
               tradedate AS "TradeDate",
               maturitydate AS "MaturityDate",
               tradetype AS "TradeType",
               actiontype AS "ActionType",
               actionsubtype AS "ActionSubType",
               actionstate AS "ActionState",
               actionreason AS "ActionReason",
               tradepnl AS "TradePnl",
               forwardlossratio AS "ForwardLossRatio",
               lossprobability AS "LossProbability",
               mscore AS "MScore",
               maxprofit AS "MaxProfit",
               maxloss AS "MaxLoss",
               minprofittarget AS "MinProfitTarget",
               dailyprofittarget AS "DailyProfitTarget",
               assetprice AS "AssetPrice",
               assetstddev AS "AssetStdDev",
               assetmean AS "AssetMean",
               assetpricechange AS "AssetPriceChange",
               markettrend AS "MarketTrend",
               marketvolatility AS "MarketVolatility",
               marketdirection AS "MarketDirection",
               vixvolatility AS "VixVolatility",
               traderisk AS "TradeRisk",
               fiftydayma AS "FiftyDayMA",
               fivedayxma AS "FiveDayXMA",
               putotmprobability AS "PutOTMProbability",
               callotmprobability AS "CallOTMProbability",
               shortputgamma AS "ShortPutGamma",
               shortcallgamma AS "ShortCallGamma",
               gammarisk AS "GammaRisk",
               netprice AS "NetPrice",
               forwardprice AS "ForwardPrice",
               forwarddelta AS "ForwardDelta",
               stoplosslimit AS "StopLossLimit",
               trendtype AS "TrendType",
               trendstrength AS "TrendStrength",
               rsi AS "RSI",
               rsislope AS "RSISlope",
               tdi AS "TDI",
               tdistrength AS "TDIStrength",
               createdon AS "CreatedOn",
               createdby AS "CreatedBy"
        FROM trade.trade_plan
        WHERE orderid = $1
        AND tradeid = $2
        AND valuedate = $3;
        """;

    /// <summary>SQL for get trade position.</summary>
    internal const string GetTradePosition =
        """
        SELECT
            orderid AS "OrderId",
            tradeid AS "TradeId",
            tradetype AS "TradeType",
            valuedate AS "ValueDate",
            daystoexpiry AS "DaysToExpiry",
            tradestatus AS "TradeStatus",
            commission AS "Commission",
            deltahedge AS "DeltaHedge",
            netspread AS "NetSpread",
            tradevalue AS "TradeValue",
            tradepnl AS "TradePnl",
            assetprice AS "AssetPrice",
            otmprobability AS "OTMProbability",
            forwardprice AS "ForwardPrice",
            forwardlossratio AS "ForwardLossRatio",
            lossprobability AS "LossProbability",
            riskfreerate AS "RiskFreeRate",
            createdon AS "CreatedOn",
            createdby AS "CreatedBy",
            updatedon AS "UpdatedOn",
            updatedby AS "UpdatedBy"
        FROM trade.trade_position
        WHERE orderid = $1
          AND tradeid = $2
          AND valuedate = $3
          AND tradestatus = $4
          AND daystoexpiry = $5
          AND tradetype = $6;
        """;

    /// <summary>SQL for get trade positions.</summary>
    internal const string GetTradePositions =
        """
        SELECT
                    orderid AS "OrderId",
                    tradeid AS "TradeId",
                    tradetype AS "TradeType",
                    valuedate AS "ValueDate",
                    tradestatus AS "TradeStatus",
                    daystoexpiry AS "DaysToExpiry",
                    commission AS "Commission",
                    deltahedge AS "DeltaHedge",
                    netspread AS "NetSpread",
                    tradevalue AS "TradeValue",
                    tradepnl AS "TradePnl",
                    assetprice AS "AssetPrice",
                    otmprobability AS "OTMProbability",
                    forwardprice AS "ForwardPrice",
                    forwardlossratio AS "ForwardLossRatio",
                    lossprobability AS "LossProbability",
                    riskfreerate AS "RiskFreeRate",
                    createdon AS "CreatedOn",
                    createdby AS "CreatedBy",
                    updatedon AS "UpdatedOn",
                    updatedby AS "UpdatedBy"
                FROM trade.trade_position
                WHERE orderid = $1
                AND tradeid = $2;
        """;

    /// <summary>SQL for get trade positions all.</summary>
    internal const string GetTradePositionsAll =
        """
        SELECT
                    orderid AS "OrderId",
                    tradeid AS "TradeId",
                    tradetype AS "TradeType",
                    valuedate AS "ValueDate",
                    daystoexpiry AS "DaysToExpiry",
                    tradestatus AS "TradeStatus",
                    commission AS "Commission",
                    deltahedge AS "DeltaHedge",
                    netspread AS "NetSpread",
                    tradevalue AS "TradeValue",
                    tradepnl AS "TradePnl",
                    assetprice AS "AssetPrice",
                    otmprobability AS "OTMProbability",
                    forwardprice AS "ForwardPrice",
                    forwardlossratio AS "ForwardLossRatio",
                    lossprobability AS "LossProbability",
                    riskfreerate AS "RiskFreeRate",
                    createdon AS "CreatedOn",
                    createdby AS "CreatedBy",
                    updatedon AS "UpdatedOn",
                    updatedby AS "UpdatedBy"
                FROM trade.trade_position;
        """;

    /// <summary>SQL for get trade positions by id.</summary>
    internal const string GetTradePositionsById =
        """
        SELECT
                    orderid AS "OrderId",
                    tradeid AS "TradeId",
                    tradetype AS "TradeType",
                    valuedate AS "ValueDate",
                    tradestatus AS "TradeStatus",
                    daystoexpiry AS "DaysToExpiry",
                    commission AS "Commission",
                    deltahedge AS "DeltaHedge",
                    netspread AS "NetSpread",
                    tradevalue AS "TradeValue",
                    tradepnl AS "TradePnl",
                    assetprice AS "AssetPrice",
                    otmprobability AS "OTMProbability",
                    forwardprice AS "ForwardPrice",
                    forwardlossratio AS "ForwardLossRatio",
                    lossprobability AS "LossProbability",
                    riskfreerate AS "RiskFreeRate",
                    createdon AS "CreatedOn",
                    createdby AS "CreatedBy",
                    updatedon AS "UpdatedOn",
                    updatedby AS "UpdatedBy"
                FROM trade.trade_position
                WHERE orderid = $1
                AND tradeid = $2
                AND valuedate = $3
                AND tradestatus = $4
                AND daystoexpiry = $5;
        """;

    /// <summary>SQL for get trade type limit.</summary>
    internal const string GetTradeTypeLimit =
        """
        SELECT tradeid AS "TradeId",
         tradetype AS "TradeType",
            maxlosslimit AS "MaxLossLimit",
            minprofitlimit AS "MinProfitLimit",
            maxprofitlimit AS "MaxProfitLimit"
        FROM trade.trade_type_limit
        WHERE tradeid = $1
        AND tradetype = $2;
        """;

    /// <summary>SQL for get trade type limit all.</summary>
    internal const string GetTradeTypeLimitAll =
        """
        SELECT tradeid AS "TradeId",
               tradetype AS "TradeType",
               maxlosslimit AS "MaxLossLimit",
               minprofitlimit AS "MinProfitLimit",
               maxprofitlimit AS "MaxProfitLimit"
            FROM trade.trade_type_limit;
        """;

    /// <summary>SQL for get trade type limits.</summary>
    internal const string GetTradeTypeLimits =
        """
        SELECT tradeid AS "TradeId",
               tradetype AS "TradeType",
               maxlosslimit AS "MaxLossLimit",
               minprofitlimit AS "MinProfitLimit",
               maxprofitlimit AS "MaxProfitLimit"
        FROM trade.trade_type_limit
        WHERE tradeid = $1;
        """;

    /// <summary>SQL for helper statement10.</summary>
    internal const string HelperStatement10 =
        """
        WITH changed AS (UPDATE trade.risk_management_history SET revision=$1,payload=$2 WHERE portfolio_id=$3
            AND fund_id=$4 AND value_date=$5 AND evaluated_at_utc=$6 AND invocation_id=$7
            AND revision<$8 RETURNING 1) SELECT EXISTS(SELECT 1 FROM changed);
        """;

    /// <summary>SQL for helper statement11.</summary>
    internal const string HelperStatement11 =
        """
        INSERT INTO trade.trade_order(portfolioid,fundid,orderid,revision,status,valuedate,updatedatutc,definitionhash,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
        ON CONFLICT(portfolioid,fundid,orderid,revision) DO UPDATE SET payload=EXCLUDED.payload,status=EXCLUDED.status,
          valuedate=EXCLUDED.valuedate,definitionhash=EXCLUDED.definitionhash,updatedatutc=EXCLUDED.updatedatutc
          WHERE trade.trade_order.payload=EXCLUDED.payload AND trade.trade_order.updatedatutc=EXCLUDED.updatedatutc
          RETURNING revision;
        """;

    /// <summary>SQL for helper statement12.</summary>
    internal const string HelperStatement12 =
        """
        INSERT INTO trade.order_execution(portfolioid,fundid,orderid,executionattemptid,status,startedatutc,
            completedatutc,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8)
        ON CONFLICT(portfolioid,fundid,orderid,executionattemptid) DO UPDATE SET status=EXCLUDED.status,
          startedatutc=EXCLUDED.startedatutc,completedatutc=EXCLUDED.completedatutc,payload=EXCLUDED.payload;
        """;

    /// <summary>SQL for helper statement13.</summary>
    internal const string HelperStatement13 =
        """
        INSERT INTO trade.order_execution_fill(executionattemptid,executionfillid,componentid,tradelegid,contractid,
            filledatutc,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7)
        ON CONFLICT(executionattemptid,filledatutc,executionfillid) DO UPDATE SET componentid=EXCLUDED.componentid,
          tradelegid=EXCLUDED.tradelegid,contractid=EXCLUDED.contractid,payload=EXCLUDED.payload;
        """;

    /// <summary>SQL for helper statement14.</summary>
    internal const string HelperStatement14 =
        """
        INSERT INTO trade.established_trade_history(portfolioid,fundid,strategykind,establishedatutc,orderid,tradeid,
            evidencerevision,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8)
        ON CONFLICT(portfolioid,fundid,strategykind,establishedatutc,orderid,tradeid,evidencerevision)
        DO UPDATE SET payload=EXCLUDED.payload
          WHERE trade.established_trade_history.payload=EXCLUDED.payload
        RETURNING evidencerevision;
        """;

    /// <summary>SQL for helper statement15.</summary>
    internal const string HelperStatement15 =
        """
        INSERT INTO trade.established_trade(portfolioid,fundid,orderid,tradeid,assetfamily,strategykind,establishedatutc,
            evidencerevision,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
        ON CONFLICT(portfolioid,fundid,orderid,tradeid) DO UPDATE SET assetfamily=EXCLUDED.assetfamily,
            strategykind=EXCLUDED.strategykind,
          establishedatutc=EXCLUDED.establishedatutc,evidencerevision=EXCLUDED.evidencerevision,payload=EXCLUDED.payload
          WHERE trade.established_trade.evidencerevision<EXCLUDED.evidencerevision OR
            (trade.established_trade.evidencerevision=EXCLUDED.evidencerevision
            AND trade.established_trade.payload=EXCLUDED.payload)
        RETURNING evidencerevision;
        """;

    /// <summary>SQL for helper statement16.</summary>
    internal const string HelperStatement16 =
        """
        SELECT evidencerevision
        FROM trade.established_trade WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4;
        """;

    /// <summary>SQL for helper statement17.</summary>
    internal const string HelperStatement17 =
        """
        INSERT INTO trade.strategy_position_current(portfolioid,fundid,orderid,tradeid,positionid,strategykind,
            positionsequence,routegeneration,isopen,asofutc,payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
        ON CONFLICT(portfolioid,fundid,orderid,tradeid,positionid) DO UPDATE SET strategykind=EXCLUDED.strategykind,
          positionsequence=EXCLUDED.positionsequence,routegeneration=EXCLUDED.routegeneration,isopen=EXCLUDED.isopen,
          asofutc=EXCLUDED.asofutc,payload=EXCLUDED.payload
          WHERE trade.strategy_position_current.positionsequence<EXCLUDED.positionsequence OR
            (trade.strategy_position_current.positionsequence=EXCLUDED.positionsequence
            AND trade.strategy_position_current.payload=EXCLUDED.payload)
          RETURNING positionsequence;
        """;

    /// <summary>SQL for helper statement18.</summary>
    internal const string HelperStatement18 =
        """
        SELECT positionsequence
        FROM trade.strategy_position_current
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5;
        """;

    /// <summary>SQL for helper statement19.</summary>
    internal const string HelperStatement19 =
        """
        INSERT INTO trade.strategy_position_history(positionid,asofutc,positionsequence,phase,payload,projection_sequence)
        VALUES($1,$2,$3,$4,$5,$6)
        ON CONFLICT(positionid,asofutc,positionsequence) DO UPDATE SET phase=EXCLUDED.phase,payload=EXCLUDED.payload,
          projection_sequence=EXCLUDED.projection_sequence
          WHERE trade.strategy_position_history.projection_sequence<=EXCLUDED.projection_sequence;
        """;

    /// <summary>SQL for helper statement20.</summary>
    internal const string HelperStatement20 =
        """
        DELETE FROM trade.open_position_route WHERE positionid=$1;
        """;

    /// <summary>SQL for helper statement21.</summary>
    internal const string HelperStatement21 =
        """
        DELETE FROM trade.open_position_route_recovery WHERE positionid=$1;
        """;

    /// <summary>SQL for helper statement22.</summary>
    internal const string HelperStatement22 =
        """
        INSERT INTO trade.open_position_route(contractid,portfolioid,fundid,orderid,tradeid,positionid,tradelegid,
            tradetype,generation)
                        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
                        ON CONFLICT (contractid, positionid, tradelegid) DO UPDATE SET portfolioid=EXCLUDED.portfolioid,
                        fundid=EXCLUDED.fundid, orderid=EXCLUDED.orderid, tradeid=EXCLUDED.tradeid,
                        tradetype=EXCLUDED.tradetype, generation=EXCLUDED.generation;
        """;

    /// <summary>SQL for helper statement23.</summary>
    internal const string HelperStatement23 =
        """
        INSERT INTO trade.open_position_route_recovery(shard,positionid,tradelegid,contractid,portfolioid,fundid,orderid,
            tradeid,tradetype,generation)
                        VALUES(0,$1,$2,$3,$4,$5,$6,$7,$8,$9)
                        ON CONFLICT (shard, positionid, tradelegid) DO UPDATE SET contractid=EXCLUDED.contractid,
                        portfolioid=EXCLUDED.portfolioid, fundid=EXCLUDED.fundid, orderid=EXCLUDED.orderid,
                        tradeid=EXCLUDED.tradeid, tradetype=EXCLUDED.tradetype, generation=EXCLUDED.generation;
        """;

    /// <summary>SQL for helper statement24.</summary>
    internal const string HelperStatement24 =
        """
        INSERT INTO trade.trade_limit (tradeid,tradetype,riskmargin,maxprofit,maxloss,maxreturn,maxlosslimit,
            minprofitlimit,maxprofitlimit,minprofittarget,dailyprofittarget,createdon,createdby,updatedon,updatedby)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
        ON CONFLICT(tradeid,tradetype) DO UPDATE SET riskmargin=EXCLUDED.riskmargin,maxprofit=EXCLUDED.maxprofit,
            maxloss=EXCLUDED.maxloss,
            maxreturn=EXCLUDED.maxreturn,maxlosslimit=EXCLUDED.maxlosslimit,minprofitlimit=EXCLUDED.minprofitlimit,
            maxprofitlimit=EXCLUDED.maxprofitlimit,
            minprofittarget=EXCLUDED.minprofittarget,dailyprofittarget=EXCLUDED.dailyprofittarget,
            createdon=EXCLUDED.createdon,createdby=EXCLUDED.createdby,
            updatedon=EXCLUDED.updatedon,updatedby=EXCLUDED.updatedby;
        """;

    /// <summary>SQL for helper statement25.</summary>
    internal const string HelperStatement25 =
        """
        INSERT INTO trade.trade_type_limit(tradeid,tradetype,maxlosslimit,minprofitlimit,maxprofitlimit)
        VALUES($1,$2,$3,$4,$5)
        ON CONFLICT(tradeid,tradetype) DO UPDATE SET maxlosslimit=EXCLUDED.maxlosslimit,
            minprofitlimit=EXCLUDED.minprofitlimit,maxprofitlimit=EXCLUDED.maxprofitlimit;
        """;

    /// <summary>SQL for helper statement27.</summary>
    internal const string HelperStatement27 =
        """
        SELECT payload
        FROM trade.strategy_position_current
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5;
        """;

    /// <summary>SQL for helper statement28.</summary>
    internal const string HelperStatement28 =
        """
        SELECT;
        """;

    /// <summary>SQL for helper statement29.</summary>
    internal const string HelperStatement29 =
        """
        SELECT;
        """;

    /// <summary>SQL for helper statement30.</summary>
    internal const string HelperStatement30 =
        """
        SELECT;
        """;

    /// <summary>SQL for helper statement31.</summary>
    internal const string HelperStatement31 =
        """
        SELECT payload
        FROM trade.order_execution WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND executionattemptid=$4;
        """;

    /// <summary>SQL for helper statement32.</summary>
    internal const string HelperStatement32 =
        """
        SELECT payload
        FROM trade.established_trade WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4;
        """;

    /// <summary>SQL for helper statement35.</summary>
    internal const string HelperStatement35 =
        """
        INSERT INTO trade.market_condition_assessment(workflow_id,payload,payload_sha256) VALUES($1,$2,$3)
        ON CONFLICT(workflow_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE trade.market_condition_assessment.payload=EXCLUDED.payload RETURNING workflow_id;
        """;

    /// <summary>SQL for helper statement36.</summary>
    internal const string HelperStatement36 =
        """
        INSERT INTO trade.market_condition_assessment_by_profile(market_profile_id,instrument_root,target_horizon,
            evaluated_at_utc,workflow_id,payload,payload_sha256)
        VALUES($1,$2,$3,$4,$5,$6,$7)
        ON CONFLICT(market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id) DO NOTHING;
        """;

    /// <summary>SQL for helper statement37.</summary>
    internal const string HelperStatement37 =
        """
        INSERT INTO trade.order_composition_invocation(workflow_id,invocation_id,result_hash,input_hash,payload)
        VALUES($1,$2,$3,$4,$5)
        ON CONFLICT(workflow_id,invocation_id) DO UPDATE SET payload=EXCLUDED.payload
        WHERE trade.order_composition_invocation.payload=EXCLUDED.payload RETURNING invocation_id;
        """;

    /// <summary>SQL for helper statement38.</summary>
    internal const string HelperStatement38 =
        """
        INSERT INTO trade.order_composition_history(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,
            workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
        ON CONFLICT(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id) DO NOTHING;
        """;

    /// <summary>SQL for helper statement39.</summary>
    internal const string HelperStatement39 =
        """
        INSERT INTO trade.trade_selection_invocation_event(workflow_id,invocation_id,source_sequence,event_id,
            portfolio_id,fund_id,target_horizon,lifecycle_status,outcome,occurred_at_utc,reason_code,
        selected_deployment_id,selected_deployment_version,selected_strategy_id,selected_strategy_version,
            selected_structure_id,selected_structure_version,selected_variant_id,selected_variant_version,
        candidate_set_sha256,binding_sha256,parameter_set_id,parameter_version,parameter_sha256,result_id,result_sha256,
            result_payload,event_payload)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28)
        ON CONFLICT(workflow_id,invocation_id,source_sequence) DO UPDATE SET event_payload=EXCLUDED.event_payload
        WHERE trade.trade_selection_invocation_event.event_payload=EXCLUDED.event_payload
        RETURNING invocation_id;
        """;

    /// <summary>SQL for helper statement40.</summary>
    internal const string HelperStatement40 =
        """
        INSERT INTO trade.trade_selection_history_by_fund_date(portfolio_id,fund_id,value_date,occurred_at_utc,
            workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
        ON CONFLICT(portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id) DO NOTHING;
        """;

    /// <summary>SQL for helper statement41.</summary>
    internal const string HelperStatement41 =
        """
        SELECT payload,result_hash,input_hash
        FROM trade.order_composition_invocation WHERE workflow_id=$1 AND invocation_id=$2;
        """;

    /// <summary>SQL for helper statement42.</summary>
    internal const string HelperStatement42 =
        """
        SELECT event_payload,result_sha256
        FROM trade.trade_selection_invocation_event
        WHERE workflow_id=$1 AND invocation_id=$2 AND source_sequence=1;
        """;

    /// <summary>SQL for helper statement43.</summary>
    internal const string HelperStatement43 =
        """
        INSERT INTO trade.regime_discovery_invocation(workflow_id,command_id,source_event_id,source_payload)
        VALUES($1,$2,$3,$4)
        ON CONFLICT(workflow_id,command_id) DO UPDATE SET source_payload=EXCLUDED.source_payload
        WHERE regime_discovery_invocation.source_payload=EXCLUDED.source_payload RETURNING command_id;
        """;

    /// <summary>SQL for helper statement44.</summary>
    internal const string HelperStatement44 =
        """
        INSERT INTO trade.regime_discovery(workflowid,workflowentityid,inputworkflowrevision,commandid,sourceeventid,
            sourceeventsequence,status,
            parameterpayloadsha256,signalsnapshotid,resultpayload,resultpayloadsha256,failurecode,failuremessage,
            reasonspayload,schemaversion,terminalatutc,updatedatutc)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)
        ON CONFLICT(workflowid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid,
            inputworkflowrevision=EXCLUDED.inputworkflowrevision,
            commandid=EXCLUDED.commandid,sourceeventid=EXCLUDED.sourceeventid,
            sourceeventsequence=EXCLUDED.sourceeventsequence,status=EXCLUDED.status,
            parameterpayloadsha256=EXCLUDED.parameterpayloadsha256,signalsnapshotid=EXCLUDED.signalsnapshotid,
            resultpayload=EXCLUDED.resultpayload,
            resultpayloadsha256=EXCLUDED.resultpayloadsha256,failurecode=EXCLUDED.failurecode,
            failuremessage=EXCLUDED.failuremessage,
            reasonspayload=EXCLUDED.reasonspayload,schemaversion=EXCLUDED.schemaversion,
            terminalatutc=EXCLUDED.terminalatutc,updatedatutc=EXCLUDED.updatedatutc
        WHERE regime_discovery.inputworkflowrevision < EXCLUDED.inputworkflowrevision
           OR (regime_discovery.inputworkflowrevision = EXCLUDED.inputworkflowrevision
           AND regime_discovery.commandid = EXCLUDED.commandid
               AND regime_discovery.resultpayload = EXCLUDED.resultpayload);
        """;

    /// <summary>SQL for helper statement45.</summary>
    internal const string HelperStatement45 =
        """
        SELECT workflowid,workflowentityid,inputworkflowrevision,commandid,sourceeventid,sourceeventsequence,status,
            parameterpayloadsha256,
            signalsnapshotid,resultpayload,resultpayloadsha256,failurecode,failuremessage,reasonspayload,schemaversion,
            terminalatutc,updatedatutc
        FROM trade.regime_discovery WHERE workflowid=$1;
        """;

    /// <summary>SQL for helper statement46.</summary>
    internal const string HelperStatement46 =
        """
        DELETE FROM trade.intrinsic_time_strategy_workflow_active WHERE workflowentityid=$1 AND lasteventid<=$2;
        """;

    /// <summary>SQL for helper statement47.</summary>
    internal const string HelperStatement47 =
        """
        INSERT INTO trade.risk_management_invocation(workflow_id,invocation_id,revision,payload,content_hash)
        VALUES($1,$2,$3,$4,$5)
        ON CONFLICT(workflow_id,invocation_id,revision) DO UPDATE SET content_hash=trade.risk_management_invocation.content_hash
        WHERE trade.risk_management_invocation.content_hash=EXCLUDED.content_hash RETURNING content_hash;
        """;

    /// <summary>SQL for helper statement48.</summary>
    internal const string HelperStatement48 =
        """
        INSERT INTO trade.;
        """;

    /// <summary>SQL for helper statement6.</summary>
    internal const string HelperStatement6 =
        """
        SELECT payload,payload_sha256
        FROM trade.market_condition_assessment_by_profile
        WHERE market_profile_id=$1 AND instrument_root=$2 AND target_horizon=$3 AND evaluated_at_utc<$4
        ORDER BY evaluated_at_utc DESC,workflow_id LIMIT $5;
        """;

    /// <summary>SQL for helper statement7.</summary>
    internal const string HelperStatement7 =
        """
        WITH inserted AS (INSERT INTO trade.risk_management_invocation (workflow_id,invocation_id,revision,payload,
            content_hash) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(workflow_id,invocation_id,
            revision) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM inserted);
        """;

    /// <summary>SQL for helper statement8.</summary>
    internal const string HelperStatement8 =
        """
        SELECT content_hash
        FROM trade.risk_management_invocation WHERE workflow_id=$1 AND invocation_id=$2 AND revision=$3;
        """;

    /// <summary>SQL for helper statement9.</summary>
    internal const string HelperStatement9 =
        """
        WITH inserted AS (INSERT INTO trade.risk_management_history (portfolio_id,fund_id,value_date,evaluated_at_utc,
            invocation_id,revision,payload) VALUES ($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(portfolio_id,fund_id,value_date,
            evaluated_at_utc,invocation_id) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM inserted);
        """;

    /// <summary>SQL for insert initial monitoring limit.</summary>
    internal const string InsertInitialMonitoringLimit =
        """
        INSERT INTO trade.trade_limit (tradeid,tradetype,riskmargin,maxprofit,maxloss,maxreturn,maxlosslimit,
            minprofitlimit,maxprofitlimit,minprofittarget,dailyprofittarget,createdon,createdby,updatedon,updatedby)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15) ON CONFLICT(tradeid,tradetype) DO NOTHING;
        """;

    /// <summary>SQL for insert initial spread limit.</summary>
    internal const string InsertInitialSpreadLimit =
        """
        INSERT INTO trade.trade_type_limit(tradeid,tradetype,maxlosslimit,minprofitlimit,maxprofitlimit)
        VALUES($1,$2,$3,$4,$5) ON CONFLICT(tradeid,tradetype) DO NOTHING;
        """;

    /// <summary>SQL for insert intrinsic time strategy workflow start attempt.</summary>
    internal const string InsertIntrinsicTimeStrategyWorkflowStartAttempt =
        """
        INSERT INTO trade.intrinsic_time_strategy_workflow_start_attempt (
            workflowentityid, requestedatutc, requestedworkflowid, decision, activeworkflowid,
            startcommandid, triggereventid, activestage, reasoncode, sourceeventid)
        VALUES ($1, $2, $3, $4, $5,
            $6, $7, $8, $9, $10)
            ON CONFLICT (workflowentityid, requestedatutc, requestedworkflowid) DO UPDATE SET decision=EXCLUDED.decision,
            activeworkflowid=EXCLUDED.activeworkflowid, startcommandid=EXCLUDED.startcommandid,
            triggereventid=EXCLUDED.triggereventid, activestage=EXCLUDED.activestage, reasoncode=EXCLUDED.reasoncode,
            sourceeventid=EXCLUDED.sourceeventid;
        """;

    /// <summary>SQL for insert intrinsic time strategy workflow timeline.</summary>
    internal const string InsertIntrinsicTimeStrategyWorkflowTimeline =
        """
        INSERT INTO trade.intrinsic_time_strategy_workflow_timeline (
            workflowid, eventid, workflowentityid, workflowrevision, stage, eventname,
            eventschemaversion, eventpayload, occurredatutc)
        VALUES ($1, $2, $3, $4, $5, $6,
            $7, $8, $9)
            ON CONFLICT (workflowid, eventid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid,
            workflowrevision=EXCLUDED.workflowrevision, stage=EXCLUDED.stage, eventname=EXCLUDED.eventname,
            eventschemaversion=EXCLUDED.eventschemaversion, eventpayload=EXCLUDED.eventpayload,
            occurredatutc=EXCLUDED.occurredatutc;
        """;

    /// <summary>SQL for insert option leg.</summary>
    internal const string InsertOptionLeg =
        """
        INSERT INTO trade.option_leg (
                    orderid,
                    tradeid,
                    contractid,
                    quantity,
                    strikeprice,
                    optionlegtype,
                    optionlegaction,
                    createdon,
                    createdby,
                    updatedon,
                    updatedby
                ) VALUES (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5,
                    $6,
                    $7,
                    $8,
                    $9,
                    $10,
                    $11
                )
                ON CONFLICT (tradeid, orderid, contractid) DO UPDATE SET quantity=EXCLUDED.quantity,
                strikeprice=EXCLUDED.strikeprice, optionlegtype=EXCLUDED.optionlegtype,
                optionlegaction=EXCLUDED.optionlegaction, createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby,
                updatedon=EXCLUDED.updatedon, updatedby=EXCLUDED.updatedby;
        """;

    /// <summary>SQL for insert option leg data.</summary>
    internal const string InsertOptionLegData =
        """
        INSERT INTO trade.option_leg_data (
                    orderid,
                    tradeid,
                    tradetype,
                    valuedate,
                    daystoexpiry,
                    tradestatus,
                    optionlegid,
                    bidprice,
                    askprice,
                    impliedvolatility,
                    delta,
                    gamma,
                    theta,
                    vega,
                    rho,
                    createdon,
                    createdby,
                    updatedon,
                    updatedby
                ) VALUES (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5,
                    $6,
                    $7,
                    $8,
                    $9,
                    $10,
                    $11,
                    $12,
                    $13,
                    $14,
                    $15,
                    $16,
                    $17,
                    $18,
                    $19
                )
                ON CONFLICT (orderid, tradeid, valuedate, tradetype, daystoexpiry, tradestatus,
                optionlegid) DO UPDATE SET bidprice=EXCLUDED.bidprice, askprice=EXCLUDED.askprice,
                impliedvolatility=EXCLUDED.impliedvolatility, delta=EXCLUDED.delta, gamma=EXCLUDED.gamma,
                theta=EXCLUDED.theta, vega=EXCLUDED.vega, rho=EXCLUDED.rho, createdon=EXCLUDED.createdon,
                createdby=EXCLUDED.createdby, updatedon=EXCLUDED.updatedon, updatedby=EXCLUDED.updatedby;
        """;

    /// <summary>SQL for insert option trade.</summary>
    internal const string InsertOptionTrade =
        """
        INSERT INTO trade.option_trade (
                orderid,
                tradeid,
                tradedate,
                maturitydate,
                tradetype,
                tradestate,
                tradestrategy,
                tradeaction,
                underlyingcontractid,
                underlyingassettype,
                isprimarytrade,
                ishedgetrade,
                createdon,
                createdby,
                updatedon,
                updatedby
            ) VALUES (
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7,
                $8,
                $9,
                $10,
                $11,
                $12,
                $13,
                $14,
                $15,
                $16
            )
            ON CONFLICT (orderid, tradeid) DO UPDATE SET tradedate=EXCLUDED.tradedate,
            maturitydate=EXCLUDED.maturitydate, tradetype=EXCLUDED.tradetype, tradestate=EXCLUDED.tradestate,
            tradestrategy=EXCLUDED.tradestrategy, tradeaction=EXCLUDED.tradeaction,
            underlyingcontractid=EXCLUDED.underlyingcontractid, underlyingassettype=EXCLUDED.underlyingassettype,
            isprimarytrade=EXCLUDED.isprimarytrade, ishedgetrade=EXCLUDED.ishedgetrade, createdon=EXCLUDED.createdon,
            createdby=EXCLUDED.createdby, updatedon=EXCLUDED.updatedon, updatedby=EXCLUDED.updatedby;
        """;

    /// <summary>SQL for insert option trade spread bar data.</summary>
    internal const string InsertOptionTradeSpreadBarData =
        """
        INSERT INTO trade.option_trade_spread_bar_data (
                    orderid,
                    tradeid,
                    tradetype,
                    valuedate,
                    bardate,
                    losslimit,
                    winlimit,
                    forwardspread,
                    netspread
                ) VALUES (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5,
                    $6,
                    $7,
                    $8,
                    $9
                )
                ON CONFLICT (orderid, tradeid, valuedate, tradetype, bardate) DO UPDATE SET losslimit=EXCLUDED.losslimit,
                winlimit=EXCLUDED.winlimit, forwardspread=EXCLUDED.forwardspread, netspread=EXCLUDED.netspread;
        """;

    /// <summary>SQL for insert option trade spread data.</summary>
    internal const string InsertOptionTradeSpreadData =
        """
        INSERT INTO trade.option_trade_spread_data (
            orderid,
            tradeid,
            tradetype,
            valuedate,
            sequenceid,
            losslimit,
            winlimit,
            forwardspread,
            netspread,
            createdon,
            createdby
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7,
            $8,
            $9,
            $10,
            $11
        )
        ON CONFLICT (orderid, tradeid, valuedate, tradetype, sequenceid) DO UPDATE SET losslimit=EXCLUDED.losslimit,
            winlimit=EXCLUDED.winlimit, forwardspread=EXCLUDED.forwardspread, netspread=EXCLUDED.netspread,
            createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert risk completion.</summary>
    internal const string InsertRiskCompletion =
        """
        INSERT INTO trade.risk_management_completion(workflow_id,invocation_id,source_event_id,payload,content_hash)
        VALUES($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING;
        """;

    /// <summary>SQL for insert trade fill.</summary>
    internal const string InsertTradeFill =
        """
        INSERT INTO trade.trade_fill (
                    orderid,
                    tradeid,
                    filldate,
                    fillquantity,
                    createdon,
                    createdby
                ) VALUES (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5,
                    $6
                )
                ON CONFLICT (orderid, tradeid, filldate) DO UPDATE SET fillquantity=EXCLUDED.fillquantity,
                createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert trade fill data.</summary>
    internal const string InsertTradeFillData =
        """
        INSERT INTO trade.trade_fill_data (
                orderid,
                tradeid,
                contractid,
                filldate,
                bidprice,
                askprice,
                commission,
                optionlegaction,
                createdon,
                createdby
            ) VALUES (
                $1,
                $2,
                $3,
                $4,
                $5,
                $6,
                $7,
                $8,
                $9,
                $10
            )
            ON CONFLICT (orderid, tradeid, filldate, contractid) DO UPDATE SET bidprice=EXCLUDED.bidprice,
            askprice=EXCLUDED.askprice, commission=EXCLUDED.commission, optionlegaction=EXCLUDED.optionlegaction,
            createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert trade limit.</summary>
    internal const string InsertTradeLimit =
        """
        INSERT INTO trade.trade_limit (
            tradeid,
            tradetype,
            riskmargin,
            maxprofit,
            maxloss,
            maxreturn,
            maxlosslimit,
            minprofitlimit,
            maxprofitlimit,
            minprofittarget,
            dailyprofittarget,
            createdon,
            createdby,
            updatedon,
            updatedby
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7,
            $8,
            $9,
            $10,
            $11,
            $12,
            $13,
            $14,
            $15
        )
        ON CONFLICT (tradeid, tradetype) DO UPDATE SET riskmargin=EXCLUDED.riskmargin, maxprofit=EXCLUDED.maxprofit,
            maxloss=EXCLUDED.maxloss, maxreturn=EXCLUDED.maxreturn, maxlosslimit=EXCLUDED.maxlosslimit,
            minprofitlimit=EXCLUDED.minprofitlimit, maxprofitlimit=EXCLUDED.maxprofitlimit,
            minprofittarget=EXCLUDED.minprofittarget, dailyprofittarget=EXCLUDED.dailyprofittarget,
            createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby, updatedon=EXCLUDED.updatedon,
            updatedby=EXCLUDED.updatedby;
        """;

    /// <summary>SQL for insert trade live feed.</summary>
    internal const string InsertTradeLiveFeed =
        """
        INSERT INTO trade.trade_live_feed (
            orderid,
            tradeid,
            livefeed
        ) VALUES (
            $1,
            $2,
            $3
        ) ON CONFLICT (orderid, tradeid) DO NOTHING;
        """;

    /// <summary>SQL for insert trade placement signal.</summary>
    internal const string InsertTradePlacementSignal =
        """
        INSERT INTO trade.trade_placement_signal (
            sequenceid,
            contractid,
            valuedate,
            tradeplacementsignal,
            tradeprice,
            createdon,
            createdby
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7
        )
        ON CONFLICT (contractid, valuedate, sequenceid) DO UPDATE SET tradeplacementsignal=EXCLUDED.tradeplacementsignal,
            tradeprice=EXCLUDED.tradeprice, createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert trade plan.</summary>
    internal const string InsertTradePlan =
        """
        INSERT INTO trade.trade_plan (
         sequenceid,
            orderid,
            tradeid,
            tradetype,
            tradedate,
            valuedate,
            maturitydate,
            actiondate,
            actiontype,
            actionsubtype,
            actionstate,
            actionreason,
            tradepnl,
            forwardlossratio,
            lossprobability,
            mscore,
            maxprofit,
            maxloss,
            minprofittarget,
            dailyprofittarget,
            assetprice,
            assetstddev,
            assetmean,
            assetpricechange,
            markettrend,
            marketvolatility,
            marketdirection,
            vixvolatility,
            traderisk,
            fiftydayma,
            fivedayxma,
            putotmprobability,
            callotmprobability,
            shortputgamma,
            shortcallgamma,
            gammarisk,
            netprice,
            forwardprice,
            forwarddelta,
            stoplosslimit,
            trendtype,
            trendstrength,
            rsi,
            rsislope,
            tdi,
            tdistrength,
            createdon,
            createdby
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7,
            $8,
            $9,
            $10,
            $11,
            $12,
            $13,
            $14,
            $15,
            $16,
            $17,
            $18,
            $19,
            $20,
            $21,
            $22,
            $23,
            $24,
            $25,
            $26,
            $27,
            $28,
            $29,
            $30,
            $31,
            $32,
            $33,
            $34,
            $35,
            $36,
            $37,
            $38,
            $39,
            $40,
            $41,
            $42,
            $43,
            $44,
            $45,
            $46,
            $47,
            $48
        )
        ON CONFLICT (orderid, tradeid, valuedate, sequenceid) DO UPDATE SET tradetype=EXCLUDED.tradetype,
            tradedate=EXCLUDED.tradedate, maturitydate=EXCLUDED.maturitydate, actiondate=EXCLUDED.actiondate,
            actiontype=EXCLUDED.actiontype, actionsubtype=EXCLUDED.actionsubtype, actionstate=EXCLUDED.actionstate,
            actionreason=EXCLUDED.actionreason, tradepnl=EXCLUDED.tradepnl, forwardlossratio=EXCLUDED.forwardlossratio,
            lossprobability=EXCLUDED.lossprobability, mscore=EXCLUDED.mscore, maxprofit=EXCLUDED.maxprofit,
            maxloss=EXCLUDED.maxloss, minprofittarget=EXCLUDED.minprofittarget,
            dailyprofittarget=EXCLUDED.dailyprofittarget, assetprice=EXCLUDED.assetprice,
            assetstddev=EXCLUDED.assetstddev, assetmean=EXCLUDED.assetmean, assetpricechange=EXCLUDED.assetpricechange,
            markettrend=EXCLUDED.markettrend, marketvolatility=EXCLUDED.marketvolatility,
            marketdirection=EXCLUDED.marketdirection, vixvolatility=EXCLUDED.vixvolatility, traderisk=EXCLUDED.traderisk,
            fiftydayma=EXCLUDED.fiftydayma, fivedayxma=EXCLUDED.fivedayxma, putotmprobability=EXCLUDED.putotmprobability,
            callotmprobability=EXCLUDED.callotmprobability, shortputgamma=EXCLUDED.shortputgamma,
            shortcallgamma=EXCLUDED.shortcallgamma, gammarisk=EXCLUDED.gammarisk, netprice=EXCLUDED.netprice,
            forwardprice=EXCLUDED.forwardprice, forwarddelta=EXCLUDED.forwarddelta, stoplosslimit=EXCLUDED.stoplosslimit,
            trendtype=EXCLUDED.trendtype, trendstrength=EXCLUDED.trendstrength, rsi=EXCLUDED.rsi,
            rsislope=EXCLUDED.rsislope, tdi=EXCLUDED.tdi, tdistrength=EXCLUDED.tdistrength, createdon=EXCLUDED.createdon,
            createdby=EXCLUDED.createdby;
        """;

    /// <summary>SQL for insert trade plan forward loss limit.</summary>
    internal const string InsertTradePlanForwardLossLimit =
        """
        INSERT INTO trade.trade_plan_forward_loss_limit (orderid, tradeid, tradetype, valuedate, limittype)
        VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT (orderid, tradeid, valuedate, tradetype) DO UPDATE SET limittype=EXCLUDED.limittype;
        """;

    /// <summary>SQL for insert trade plan forward loss ratio.</summary>
    internal const string InsertTradePlanForwardLossRatio =
        """
        INSERT INTO trade.trade_plan_forward_loss_ratio (
         partitionid,
         valuedate,
         forwardlossratio,
         sequenceid
        ) VALUES (
         $1,
         $2,
         $3,
         $4
        ) ON CONFLICT (partitionid, valuedate, forwardlossratio, sequenceid) DO NOTHING;
        """;

    /// <summary>SQL for insert trade position.</summary>
    internal const string InsertTradePosition =
        """
        INSERT INTO trade.trade_position (
                    orderid,
                    tradeid,
                    tradetype,
                    valuedate,
                    daystoexpiry,
                    tradestatus,
                    commission,
                    deltahedge,
                    netspread,
                    tradevalue,
                    tradepnl,
                    assetprice,
                    otmprobability,
                    forwardprice,
                    forwardlossratio,
                    lossprobability,
                    riskfreerate,
                    createdon,
                    createdby,
                    updatedon,
                    updatedby
                ) VALUES (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5,
                    $6,
                    $7,
                    $8,
                    $9,
                    $10,
                    $11,
                    $12,
                    $13,
                    $14,
                    $15,
                    $16,
                    $17,
                    $18,
                    $19,
                    $20,
                    $21
                )
                ON CONFLICT (orderid, tradeid, valuedate, tradestatus, daystoexpiry,
                tradetype) DO UPDATE SET commission=EXCLUDED.commission, deltahedge=EXCLUDED.deltahedge,
                netspread=EXCLUDED.netspread, tradevalue=EXCLUDED.tradevalue, tradepnl=EXCLUDED.tradepnl,
                assetprice=EXCLUDED.assetprice, otmprobability=EXCLUDED.otmprobability,
                forwardprice=EXCLUDED.forwardprice, forwardlossratio=EXCLUDED.forwardlossratio,
                lossprobability=EXCLUDED.lossprobability, riskfreerate=EXCLUDED.riskfreerate,
                createdon=EXCLUDED.createdon, createdby=EXCLUDED.createdby, updatedon=EXCLUDED.updatedon,
                updatedby=EXCLUDED.updatedby;
        """;

    /// <summary>SQL for insert trade position state.</summary>
    internal const string InsertTradePositionState =
        """
        INSERT INTO trade.trade_position_state (
            orderid,
            tradeid,
            tradepositionstate,
            openedon,
            openedby
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5
        ) ON CONFLICT (orderid, tradeid) DO NOTHING;
        """;

    /// <summary>SQL for insert trade type limit.</summary>
    internal const string InsertTradeTypeLimit =
        """
        INSERT INTO trade.trade_type_limit (
                    tradeid,
                    tradetype,
                    maxlosslimit,
                    minprofitlimit,
                    maxprofitlimit
                ) VALUES (
                    $1,
                    $2,
                    $3,
                    $4,
                    $5
                )
                ON CONFLICT (tradeid, tradetype) DO UPDATE SET maxlosslimit=EXCLUDED.maxlosslimit,
                minprofitlimit=EXCLUDED.minprofitlimit, maxprofitlimit=EXCLUDED.maxprofitlimit;
        """;

    /// <summary>SQL for native get active intrinsic time strategy workflow.</summary>
    internal const string NativeGetActiveIntrinsicTimeStrategyWorkflow =
        """
        SELECT workflowentityid, workflowid, contractid, timeframestartvaluedate, timeperiod,
               currentstage, workflowrevision, lasteventid, stateschemaversion, statepayload,
               startedatutc, updatedatutc
        FROM trade.intrinsic_time_strategy_workflow_active
        WHERE workflowentityid = $1;
        """;

    /// <summary>SQL for native get intrinsic time strategy workflow.</summary>
    internal const string NativeGetIntrinsicTimeStrategyWorkflow =
        """
        SELECT workflowid, workflowentityid, workflowdefinitionid, workflowdefinitionversion,
               contractid, timeframestartvaluedate, timeperiod, triggereventid, correlationid,
               status, outcome, currentstage, workflowrevision, lasteventid, stateschemaversion,
               statepayload, stopreasoncode, startedatutc, terminalatutc, updatedatutc
        FROM trade.intrinsic_time_strategy_workflow
        WHERE workflowid = $1;
        """;

    /// <summary>SQL for native get intrinsic time strategy workflow start attempts.</summary>
    internal const string NativeGetIntrinsicTimeStrategyWorkflowStartAttempts =
        """
        SELECT workflowentityid, requestedatutc, requestedworkflowid, decision, activeworkflowid,
               startcommandid, triggereventid, activestage, reasoncode, sourceeventid
        FROM trade.intrinsic_time_strategy_workflow_start_attempt
        WHERE workflowentityid = $1 AND requestedatutc < $2
        ORDER BY requestedatutc DESC, requestedworkflowid DESC
        LIMIT $3;
        """;

    /// <summary>SQL for native get intrinsic time strategy workflow timeline.</summary>
    internal const string NativeGetIntrinsicTimeStrategyWorkflowTimeline =
        """
        SELECT workflowid, eventid, workflowentityid, workflowrevision, stage, eventname,
               eventschemaversion, eventpayload, occurredatutc
        FROM trade.intrinsic_time_strategy_workflow_timeline
        WHERE workflowid = $1 AND eventid > $2
        ORDER BY eventid ASC
        LIMIT $3;
        """;

    /// <summary>SQL for native get intrinsic time strategy workflows by entity.</summary>
    internal const string NativeGetIntrinsicTimeStrategyWorkflowsByEntity =
        """
        SELECT workflowentityid, startedatutc, workflowid, status, outcome, currentstage,
               workflowrevision, terminalatutc, stopreasoncode
        FROM trade.intrinsic_time_strategy_workflow_by_entity
        WHERE workflowentityid = $1 AND startedatutc < $2
        ORDER BY startedatutc DESC, workflowid DESC
        LIMIT $3;
        """;

    /// <summary>SQL for native get intrinsic time strategy workflows by status day.</summary>
    internal const string NativeGetIntrinsicTimeStrategyWorkflowsByStatusDay =
        """
        SELECT workflowentityid, startedatutc, workflowid, status, outcome, currentstage,
               workflowrevision, terminalatutc, stopreasoncode
        FROM trade.intrinsic_time_strategy_workflow_by_status_day
        WHERE status = $1 AND starteddate = $2
        ORDER BY startedatutc DESC, workflowid DESC
        LIMIT $3;
        """;

    /// <summary>SQL for project broker account.</summary>
    internal const string ProjectBrokerAccount =
        """
        INSERT INTO trade.broker_account_read_model (account_alias, revision, account_definition)
        VALUES ($1, $2, $3)
        ON CONFLICT (account_alias, revision) DO UPDATE SET account_definition = EXCLUDED.account_definition;
        """;

    const string FundOrderFillsProjection =
        """
        SELECT o.payload, e.payload,
            (SELECT CASE WHEN COUNT(*) = 1 THEN MIN(t.tradeid) END
             FROM trade.established_trade t
             WHERE t.portfolioid=o.portfolioid AND t.fundid=o.fundid AND t.orderid=o.orderid),
            (SELECT first.updatedatutc FROM trade.trade_order first
             WHERE first.portfolioid=o.portfolioid AND first.fundid=o.fundid AND first.orderid=o.orderid
             ORDER BY first.revision LIMIT 1)
        FROM latest o
        LEFT JOIN trade.order_execution e USING (portfolioid, fundid, orderid)
        """;

    /// <summary>Reads the first page at the initial order and attempt cursor.</summary>
    internal const string ReadFundOrderFillsFirstPage =
        """
        WITH latest AS (
            SELECT DISTINCT ON (orderid) portfolioid, fundid, orderid, payload
            FROM trade.trade_order
            WHERE portfolioid=$1 AND fundid=$2 AND valuedate=$3 AND orderid >= 0
            ORDER BY orderid, revision DESC
        )
        """ + "\n" + FundOrderFillsProjection + "\n" + """
        WHERE (o.orderid, COALESCE(e.executionattemptid, '00000000-0000-0000-0000-000000000000'::uuid))
            > (0, '00000000-0000-0000-0000-000000000000'::uuid)
        ORDER BY o.orderid, e.executionattemptid
        LIMIT $4;
        """;

    // Each later order contributes at least one result through the left join. The cursor
    // order can contribute zero after attempt filtering, so read one extra order key.
    /// <summary>Seeks to the continuation order and bounds the order keys before joining execution attempts.</summary>
    internal const string ReadFundOrderFillsContinuationPage =
        """
        WITH order_keys AS MATERIALIZED (
            SELECT DISTINCT orderid
            FROM trade.trade_order
            WHERE portfolioid=$1 AND fundid=$2 AND valuedate=$3 AND orderid >= $4
            ORDER BY orderid
            LIMIT ($6 + 1)
        ), latest AS (
            SELECT o.portfolioid, o.fundid, k.orderid, o.payload
            FROM order_keys k
            CROSS JOIN LATERAL (
                SELECT portfolioid, fundid, payload
                FROM trade.trade_order
                WHERE portfolioid=$1 AND fundid=$2 AND valuedate=$3 AND orderid=k.orderid
                ORDER BY revision DESC
                LIMIT 1
            ) o
        )
        """ + "\n" + FundOrderFillsProjection + "\n" + """
        WHERE (o.orderid, COALESCE(e.executionattemptid, '00000000-0000-0000-0000-000000000000'::uuid)) > ($4,$5)
        ORDER BY o.orderid, e.executionattemptid
        LIMIT $6;
        """;

    /// <summary>SQL for read pending strategy handoffs.</summary>
    internal const string ReadPendingStrategyHandoffs =
        """
        SELECT payload
        FROM trade.strategy_execution_handoff WHERE phase<6 AND handoff_id>$1 ORDER BY handoff_id LIMIT 32;
        """;

    /// <summary>SQL for read pipeline history template.</summary>
    internal const string ReadPipelineHistoryTemplate =
        """
        SELECT {0},workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,{1}
        FROM {2}
        WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3{3}
        ORDER BY {0} DESC,workflow_id DESC,invocation_id DESC
        LIMIT {4};
        """;

    /// <summary>SQL for read position history.</summary>
    internal const string ReadPositionHistory =
        """
        SELECT asofutc,positionsequence,payload FROM trade.strategy_position_history
        WHERE positionid=$1 AND asofutc>=$2 AND asofutc<=$3 ORDER BY asofutc,positionsequence LIMIT $4;
        """;

    /// <summary>SQL for read position history after.</summary>
    internal const string ReadPositionHistoryAfter =
        """
        SELECT asofutc,positionsequence,payload FROM trade.strategy_position_history
        WHERE positionid=$1 AND asofutc>=$2 AND asofutc<=$3 AND (asofutc,positionsequence)>($4,$5)
        ORDER BY asofutc,positionsequence LIMIT $6;
        """;

    /// <summary>SQL for read recovery routes.</summary>
    internal const string ReadRecoveryRoutes =
        """
        SELECT contractid,portfolioid,fundid,orderid,tradeid,positionid,tradelegid,tradetype,generation
        FROM trade.open_position_route_recovery WHERE shard=0 ORDER BY positionid,tradelegid;
        """;

    /// <summary>SQL for read recovery routes after.</summary>
    internal const string ReadRecoveryRoutesAfter =
        """
        SELECT contractid,portfolioid,fundid,orderid,tradeid,positionid,tradelegid,tradetype,generation
        FROM trade.open_position_route_recovery
        WHERE shard=0 AND (positionid,tradelegid)>($1,$2) ORDER BY positionid,tradelegid LIMIT $3;
        """;

    /// <summary>SQL for read recovery routes page.</summary>
    internal const string ReadRecoveryRoutesPage =
        """
        SELECT contractid,portfolioid,fundid,orderid,tradeid,positionid,tradelegid,tradetype,generation
        FROM trade.open_position_route_recovery WHERE shard=0 ORDER BY positionid,tradelegid LIMIT $1;
        """;

    /// <summary>SQL for read risk completion.</summary>
    internal const string ReadRiskCompletion =
        """
        SELECT source_event_id,payload,content_hash
        FROM trade.risk_management_completion WHERE workflow_id=$1 AND invocation_id=$2;
        """;

    /// <summary>SQL for read risk history.</summary>
    internal const string ReadRiskHistory =
        """
        SELECT payload,evaluated_at_utc,invocation_id
        FROM trade.risk_management_history
        WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3
        ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $4;
        """;

    /// <summary>SQL for read risk history after.</summary>
    internal const string ReadRiskHistoryAfter =
        """
        SELECT payload,evaluated_at_utc,invocation_id
        FROM (
            (SELECT payload,evaluated_at_utc,invocation_id
             FROM trade.risk_management_history
             WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3
                 AND evaluated_at_utc=$4 AND invocation_id>$5
             ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $6)
            UNION ALL
            (SELECT payload,evaluated_at_utc,invocation_id
             FROM trade.risk_management_history
             WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3 AND evaluated_at_utc<$4
             ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $6)
        ) page
        ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $6;
        """;

    /// <summary>SQL for read routes by contract.</summary>
    internal const string ReadRoutesByContract =
        """
        SELECT contractid,portfolioid,fundid,orderid,tradeid,positionid,tradelegid,tradetype,generation
        FROM trade.open_position_route WHERE contractid=$1 ORDER BY positionid,tradelegid;
        """;

    /// <summary>SQL for read spread limits.</summary>
    internal const string ReadSpreadLimits =
        """
        SELECT tradeid,tradetype,maxlosslimit,minprofitlimit,maxprofitlimit
        FROM trade.trade_type_limit ORDER BY tradeid,tradetype;
        """;

    /// <summary>SQL for read spread limits by trade.</summary>
    internal const string ReadSpreadLimitsByTrade =
        """
        SELECT tradeid,tradetype,maxlosslimit,minprofitlimit,maxprofitlimit
        FROM trade.trade_type_limit WHERE tradeid=$1 ORDER BY tradeid,tradetype;
        """;

    /// <summary>SQL for read spread limits by trade and type.</summary>
    internal const string ReadSpreadLimitsByTradeAndType =
        """
        SELECT tradeid,tradetype,maxlosslimit,minprofitlimit,maxprofitlimit
        FROM trade.trade_type_limit WHERE tradeid=$1 AND tradetype=$2 ORDER BY tradeid,tradetype;
        """;

    /// <summary>SQL for read strategy handoff.</summary>
    internal const string ReadStrategyHandoff =
        """
        SELECT payload FROM trade.strategy_execution_handoff WHERE handoff_id=$1;
        """;

    /// <summary>SQL for read strategy handoff authorization.</summary>
    internal const string ReadStrategyHandoffAuthorization =
        """
        SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventpayload,e.commandid,
            e.eventtimestamp::text,e.streamversion
        FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid
        WHERE e.eventversion=$1 AND e.commandid=$2;
        """;

    /// <summary>SQL for read strategy handoffs by workflow.</summary>
    internal const string ReadStrategyHandoffsByWorkflow =
        """
        SELECT payload FROM trade.strategy_execution_handoff WHERE workflow_id=$1 ORDER BY handoff_id;
        """;

    /// <summary>SQL for read trade limits.</summary>
    internal const string ReadTradeLimits =
        """
        SELECT t.tradeid,t.tradetype,t.riskmargin,t.maxprofit,t.maxloss,t.maxreturn,
          COALESCE(s.maxlosslimit,t.maxlosslimit),COALESCE(s.minprofitlimit,t.minprofitlimit),COALESCE(s.maxprofitlimit,
            t.maxprofitlimit),
          t.minprofittarget,t.dailyprofittarget,t.createdon,t.createdby,t.updatedon,t.updatedby
        FROM trade.trade_limit t
        LEFT JOIN trade.trade_type_limit s ON t.tradeid=s.tradeid AND t.tradetype=s.tradetype
        ORDER BY t.tradeid,t.tradetype;
        """;

    /// <summary>SQL for read trade limits by trade.</summary>
    internal const string ReadTradeLimitsByTrade =
        """
        SELECT t.tradeid,t.tradetype,t.riskmargin,t.maxprofit,t.maxloss,t.maxreturn,
          COALESCE(s.maxlosslimit,t.maxlosslimit),COALESCE(s.minprofitlimit,t.minprofitlimit),COALESCE(s.maxprofitlimit,
            t.maxprofitlimit),
          t.minprofittarget,t.dailyprofittarget,t.createdon,t.createdby,t.updatedon,t.updatedby
        FROM trade.trade_limit t
        LEFT JOIN trade.trade_type_limit s ON t.tradeid=s.tradeid AND t.tradetype=s.tradetype
        WHERE t.tradeid=$1 ORDER BY t.tradeid,t.tradetype;
        """;

    /// <summary>Reads and locks trade order rows.</summary>
    internal const string TradeDbContextExtensions_TradeFlowProjectOrderLifecycleAsync_1 =
        """
        SELECT payload
        FROM trade.trade_order WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND revision=$4 FOR UPDATE;
        """;

    /// <summary>Updates trade order rows.</summary>
    internal const string TradeDbContextExtensions_TradeFlowProjectOrderLifecycleAsync_2 =
        """
        UPDATE trade.trade_order
        SET status=$5,payload=$6,updatedatutc=$7
        WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND revision=$4;
        """;

    /// <summary>Updates strategy position history rows.</summary>
    internal const string TradeDbContext_ProjectPositionHistoryAsync_1 =
        """
        UPDATE trade.strategy_position_history SET phase='EndOfDay',payload=$4,projection_sequence=$5
        WHERE positionid=$1 AND asofutc=$2 AND positionsequence=$3
            AND phase='MarkToMarket' AND projection_sequence=$6 RETURNING positionid;
        """;

    /// <summary>Deletes live feed recovery rows.</summary>
    internal const string TradeFeedRecoveryStore_CloseAsync_1 =
        """
        DELETE FROM trade.live_feed_recovery WHERE value_date <= $1;
        """;

    /// <summary>Reads live feed recovery rows.</summary>
    internal const string TradeFeedRecoveryStore_ReadAsync_1 =
        """
        SELECT trade_id, array_agg(contract_id ORDER BY contract_id), MAX(updated_at)
        FROM trade.live_feed_recovery WHERE value_date=$1 GROUP BY trade_id ORDER BY trade_id;
        """;

    /// <summary>Parameter-bound persistence statement.</summary>
    internal const string TradeFeedRecoveryStore_SaveAsync_1 =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1,0));
        """;

    /// <summary>Deletes live feed recovery rows.</summary>
    internal const string TradeFeedRecoveryStore_SaveAsync_2 =
        """
        DELETE FROM trade.live_feed_recovery WHERE trade_id=$1 AND value_date=$2;
        """;

    /// <summary>Inserts live feed recovery rows.</summary>
    internal const string TradeFeedRecoveryStore_SaveAsync_3 =
        """
        INSERT INTO trade.live_feed_recovery (trade_id,value_date,contract_id,updated_at) SELECT $1,$2,unnest($3::text[]),$4;
        """;

    /// <summary>SQL for update option leg data.</summary>
    internal const string UpdateOptionLegData =
        """
        UPDATE trade.option_leg_data
                SET
                    bidprice = $1,
                    askprice = $2,
                    impliedvolatility = $3,
                    delta = $4,
                    gamma = $5,
                    theta = $6,
                    vega = $7,
                    rho = $8,
                    updatedon = $9,
                    updatedby = $10
                WHERE orderid = $11
                AND tradeid = $12
                AND valuedate = $13
                AND optionlegid = $14;
        """;

    /// <summary>SQL for update option leg data status.</summary>
    internal const string UpdateOptionLegDataStatus =
        """
        UPDATE trade.option_leg_data
        SET tradestatus = $1,
            updatedon = $2,
            updatedby = $3
        WHERE tradeid = $4
        AND valuedate = $5
        AND optionlegid = $6;
        """;

    /// <summary>SQL for update option trade.</summary>
    internal const string UpdateOptionTrade =
        """
        UPDATE trade.option_trade
        SET tradedate = $3,
            maturitydate = $4,
            tradetype = $5,
            tradestate = $6,
            tradestrategy = $7,
            tradeaction = $8,
            underlyingcontractid = $9,
            underlyingassettype = $10,
            isprimarytrade = $11,
            ishedgetrade = $12,
            updatedon = $13,
            updatedby = $14
        WHERE orderid = $1
          AND tradeid = $2;
        """;

    /// <summary>SQL for update option trade state.</summary>
    internal const string UpdateOptionTradeState =
        """
        UPDATE trade.option_trade
        SET
            tradestate = $1
        WHERE
            orderid = $2
            AND tradeid = $3;
        """;

    /// <summary>SQL for update trade limit daily profit target.</summary>
    internal const string UpdateTradeLimitDailyProfitTarget =
        """
        UPDATE trade.trade_limit
        SET dailyprofittarget = $1,
            updatedon = $2,
            updatedby = $3
        WHERE tradeid = $4
        AND tradetype = $5;
        """;

    /// <summary>SQL for update trade live feed.</summary>
    internal const string UpdateTradeLiveFeed =
        """
        UPDATE trade.trade_live_feed
        SET livefeed = $1
        WHERE orderid = $2
        AND tradeid = $3;
        """;

    /// <summary>SQL for update trade plan forward loss limit.</summary>
    internal const string UpdateTradePlanForwardLossLimit =
        """
        UPDATE trade.trade_plan_forward_loss_limit
        SET limittype = $1
        WHERE orderid = $2
          AND tradeid = $3
          AND tradetype = $4
          AND valuedate = $5;
        """;

    /// <summary>SQL for update trade position.</summary>
    internal const string UpdateTradePosition =
        """
        UPDATE trade.trade_position
        SET
            commission = $1,
            deltahedge = $2,
            netspread = $3,
            tradevalue = $4,
            tradepnl = $5,
            assetprice = $6,
            otmprobability = $7,
            forwardprice = $8,
            lossprobability = $9,
            riskfreerate = $10,
            updatedon = $11,
            updatedby = $12
        WHERE
            orderid = $13
            AND tradeid = $14
            AND valuedate = $15
            AND tradestatus = $16
            AND daystoexpiry = $17
            AND tradetype = $18;
        """;

    /// <summary>SQL for update trade position status.</summary>
    internal const string UpdateTradePositionStatus =
        """
        UPDATE trade.trade_position
        SET tradestatus = $1,
            updatedon = $2,
            updatedby = $3
        WHERE tradeid = $4
        AND tradetype = $5
        AND valuedate = $6
        AND daystoexpiry = $7
        AND tradestatus = $8;
        """;

    /// <summary>SQL for upsert active intrinsic time strategy workflow.</summary>
    internal const string UpsertActiveIntrinsicTimeStrategyWorkflow =
        """
        INSERT INTO trade.intrinsic_time_strategy_workflow_active (
            workflowentityid, workflowid, contractid, timeframestartvaluedate, timeperiod,
            currentstage, workflowrevision, lasteventid, stateschemaversion, statepayload,
            startedatutc, updatedatutc)
        VALUES ($1, $2, $3, $4, $5,
            $6, $7, $8, $9, $10,
            $11, $12)
            ON CONFLICT (workflowentityid) DO UPDATE SET workflowid=EXCLUDED.workflowid, contractid=EXCLUDED.contractid,
            timeframestartvaluedate=EXCLUDED.timeframestartvaluedate, timeperiod=EXCLUDED.timeperiod,
            currentstage=EXCLUDED.currentstage, workflowrevision=EXCLUDED.workflowrevision,
            lasteventid=EXCLUDED.lasteventid, stateschemaversion=EXCLUDED.stateschemaversion,
            statepayload=EXCLUDED.statepayload, startedatutc=EXCLUDED.startedatutc, updatedatutc=EXCLUDED.updatedatutc;
        """;

    /// <summary>SQL for upsert intrinsic time strategy workflow.</summary>
    internal const string UpsertIntrinsicTimeStrategyWorkflow =
        """
        INSERT INTO trade.intrinsic_time_strategy_workflow (
            workflowid, workflowentityid, workflowdefinitionid, workflowdefinitionversion,
            contractid, timeframestartvaluedate, timeperiod, triggereventid, correlationid,
            status, outcome, currentstage, workflowrevision, lasteventid, stateschemaversion,
            statepayload, stopreasoncode, startedatutc, terminalatutc, updatedatutc)
        VALUES ($1, $2, $3, $4,
            $5, $6, $7, $8, $9,
            $10, $11, $12, $13, $14, $15,
            $16, $17, $18, $19, $20)
            ON CONFLICT (workflowid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid,
            workflowdefinitionid=EXCLUDED.workflowdefinitionid,
            workflowdefinitionversion=EXCLUDED.workflowdefinitionversion, contractid=EXCLUDED.contractid,
            timeframestartvaluedate=EXCLUDED.timeframestartvaluedate, timeperiod=EXCLUDED.timeperiod,
            triggereventid=EXCLUDED.triggereventid, correlationid=EXCLUDED.correlationid, status=EXCLUDED.status,
            outcome=EXCLUDED.outcome, currentstage=EXCLUDED.currentstage, workflowrevision=EXCLUDED.workflowrevision,
            lasteventid=EXCLUDED.lasteventid, stateschemaversion=EXCLUDED.stateschemaversion,
            statepayload=EXCLUDED.statepayload, stopreasoncode=EXCLUDED.stopreasoncode,
            startedatutc=EXCLUDED.startedatutc, terminalatutc=EXCLUDED.terminalatutc, updatedatutc=EXCLUDED.updatedatutc;
        """;

    /// <summary>SQL for upsert intrinsic time strategy workflow by entity.</summary>
    internal const string UpsertIntrinsicTimeStrategyWorkflowByEntity =
        """
        INSERT INTO trade.intrinsic_time_strategy_workflow_by_entity (
            workflowentityid, startedatutc, workflowid, status, outcome, currentstage,
            workflowrevision, terminalatutc, stopreasoncode)
        VALUES ($1, $2, $3, $4, $5, $6,
            $7, $8, $9)
            ON CONFLICT (workflowentityid, startedatutc, workflowid) DO UPDATE SET status=EXCLUDED.status,
            outcome=EXCLUDED.outcome, currentstage=EXCLUDED.currentstage, workflowrevision=EXCLUDED.workflowrevision,
            terminalatutc=EXCLUDED.terminalatutc, stopreasoncode=EXCLUDED.stopreasoncode;
        """;

    /// <summary>SQL for upsert intrinsic time strategy workflow by status day.</summary>
    internal const string UpsertIntrinsicTimeStrategyWorkflowByStatusDay =
        """
        INSERT INTO trade.intrinsic_time_strategy_workflow_by_status_day (
            status, starteddate, startedatutc, workflowid, workflowentityid, outcome,
            currentstage, workflowrevision, terminalatutc, stopreasoncode)
        VALUES ($1, $2, $3, $4, $5, $6,
            $7, $8, $9, $10)
            ON CONFLICT (status, starteddate, startedatutc, workflowid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid, outcome=EXCLUDED.outcome, currentstage=EXCLUDED.currentstage, workflowrevision=EXCLUDED.workflowrevision, terminalatutc=EXCLUDED.terminalatutc, stopreasoncode=EXCLUDED.stopreasoncode;
        """;

    /// <summary>SQL for upsert market condition.</summary>
    internal const string UpsertMarketCondition =
        """
        INSERT INTO trade.market_condition (workflowid, workflowentityid, inputworkflowrevision, commandid, sourceeventid,
            fundid, instrumentroot, targethorizon, parametersetid, parametersetversion, parameterpayloadsha256,
            snapshotid, snapshotsha256, tradeability, conditiontype, direction, phase, strength, confidence,
            primaryreasoncode, resultpayload, resultpayloadsha256, evaluatedatutc, validuntilutc, marketdataasofutc,
            completedatutc, updatedatutc, volatilitybehavior, liquidityquality, dataquality, upstreamalignment,
            evidencepayload, conflictingevidencepayload, blockingreasonspayload, reasonspayload, summarytext)
        VALUES ($1, $2, $3, $4, $5,
            $6, $7, $8, $9, $10, $11,
            $12, $13, $14, $15, $16, $17, $18, $19,
            $20, $21, $22, $23, $24, $25,
            $26, $27, $28, $29, $30, $31,
            $32, $33, $34, $35, $36)
            ON CONFLICT (workflowid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid,
            inputworkflowrevision=EXCLUDED.inputworkflowrevision, commandid=EXCLUDED.commandid,
            sourceeventid=EXCLUDED.sourceeventid, fundid=EXCLUDED.fundid, instrumentroot=EXCLUDED.instrumentroot,
            targethorizon=EXCLUDED.targethorizon, parametersetid=EXCLUDED.parametersetid,
            parametersetversion=EXCLUDED.parametersetversion, parameterpayloadsha256=EXCLUDED.parameterpayloadsha256,
            snapshotid=EXCLUDED.snapshotid, snapshotsha256=EXCLUDED.snapshotsha256, tradeability=EXCLUDED.tradeability,
            conditiontype=EXCLUDED.conditiontype, direction=EXCLUDED.direction, phase=EXCLUDED.phase,
            strength=EXCLUDED.strength, confidence=EXCLUDED.confidence, primaryreasoncode=EXCLUDED.primaryreasoncode,
            resultpayload=EXCLUDED.resultpayload, resultpayloadsha256=EXCLUDED.resultpayloadsha256,
            evaluatedatutc=EXCLUDED.evaluatedatutc, validuntilutc=EXCLUDED.validuntilutc,
            marketdataasofutc=EXCLUDED.marketdataasofutc, completedatutc=EXCLUDED.completedatutc,
            updatedatutc=EXCLUDED.updatedatutc, volatilitybehavior=EXCLUDED.volatilitybehavior,
            liquidityquality=EXCLUDED.liquidityquality, dataquality=EXCLUDED.dataquality,
            upstreamalignment=EXCLUDED.upstreamalignment, evidencepayload=EXCLUDED.evidencepayload,
            conflictingevidencepayload=EXCLUDED.conflictingevidencepayload,
            blockingreasonspayload=EXCLUDED.blockingreasonspayload, reasonspayload=EXCLUDED.reasonspayload,
            summarytext=EXCLUDED.summarytext;
        """;

    /// <summary>SQL for upsert market condition by fund.</summary>
    internal const string UpsertMarketConditionByFund =
        """
        INSERT INTO trade.market_condition_by_fund (fundid, instrumentroot, targethorizon, evaluatedatutc, workflowid,
            workflowentityid, inputworkflowrevision, commandid, sourceeventid, parametersetid, parametersetversion,
            parameterpayloadsha256, snapshotid, snapshotsha256, tradeability, conditiontype, direction, phase,
            strength, confidence, primaryreasoncode, resultpayload, resultpayloadsha256, validuntilutc,
            marketdataasofutc, completedatutc, updatedatutc, volatilitybehavior, liquidityquality, dataquality,
            upstreamalignment, evidencepayload, conflictingevidencepayload, blockingreasonspayload, reasonspayload,
            summarytext)
        VALUES ($1, $2, $3, $4, $5,
            $6, $7, $8, $9, $10,
            $11, $12, $13, $14, $15,
            $16, $17, $18, $19, $20, $21, $22,
            $23, $24, $25, $26, $27,
            $28, $29, $30, $31, $32,
            $33, $34, $35, $36)
            ON CONFLICT (fundid, instrumentroot, targethorizon, evaluatedatutc,
            workflowid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid,
            inputworkflowrevision=EXCLUDED.inputworkflowrevision, commandid=EXCLUDED.commandid,
            sourceeventid=EXCLUDED.sourceeventid, parametersetid=EXCLUDED.parametersetid,
            parametersetversion=EXCLUDED.parametersetversion, parameterpayloadsha256=EXCLUDED.parameterpayloadsha256,
            snapshotid=EXCLUDED.snapshotid, snapshotsha256=EXCLUDED.snapshotsha256, tradeability=EXCLUDED.tradeability,
            conditiontype=EXCLUDED.conditiontype, direction=EXCLUDED.direction, phase=EXCLUDED.phase,
            strength=EXCLUDED.strength, confidence=EXCLUDED.confidence, primaryreasoncode=EXCLUDED.primaryreasoncode,
            resultpayload=EXCLUDED.resultpayload, resultpayloadsha256=EXCLUDED.resultpayloadsha256,
            validuntilutc=EXCLUDED.validuntilutc, marketdataasofutc=EXCLUDED.marketdataasofutc,
            completedatutc=EXCLUDED.completedatutc, updatedatutc=EXCLUDED.updatedatutc,
            volatilitybehavior=EXCLUDED.volatilitybehavior, liquidityquality=EXCLUDED.liquidityquality,
            dataquality=EXCLUDED.dataquality, upstreamalignment=EXCLUDED.upstreamalignment,
            evidencepayload=EXCLUDED.evidencepayload, conflictingevidencepayload=EXCLUDED.conflictingevidencepayload,
            blockingreasonspayload=EXCLUDED.blockingreasonspayload, reasonspayload=EXCLUDED.reasonspayload,
            summarytext=EXCLUDED.summarytext;
        """;

    /// <summary>SQL for upsert regime discovery.</summary>
    internal const string UpsertRegimeDiscovery =
        """
        INSERT INTO trade.regime_discovery (
            workflowid, workflowentityid, inputworkflowrevision, commandid, sourceeventid,
            sourceeventsequence, status, parameterpayloadsha256, signalsnapshotid, resultpayload,
            resultpayloadsha256, failurecode, failuremessage, reasonspayload, schemaversion,
            terminalatutc, updatedatutc)
        VALUES ($1, $2, $3, $4, $5,
            $6, $7, $8, $9, $10,
            $11, $12, $13, $14, $15,
            $16, $17)
            ON CONFLICT (workflowid) DO UPDATE SET workflowentityid=EXCLUDED.workflowentityid,
            inputworkflowrevision=EXCLUDED.inputworkflowrevision, commandid=EXCLUDED.commandid,
            sourceeventid=EXCLUDED.sourceeventid, sourceeventsequence=EXCLUDED.sourceeventsequence,
            status=EXCLUDED.status, parameterpayloadsha256=EXCLUDED.parameterpayloadsha256,
            signalsnapshotid=EXCLUDED.signalsnapshotid, resultpayload=EXCLUDED.resultpayload,
            resultpayloadsha256=EXCLUDED.resultpayloadsha256, failurecode=EXCLUDED.failurecode,
            failuremessage=EXCLUDED.failuremessage, reasonspayload=EXCLUDED.reasonspayload,
            schemaversion=EXCLUDED.schemaversion, terminalatutc=EXCLUDED.terminalatutc, updatedatutc=EXCLUDED.updatedatutc;
        """;

    /// <summary>SQL for upsert strategy handoff.</summary>
    internal const string UpsertStrategyHandoff =
        """
        INSERT INTO trade.strategy_execution_handoff(handoff_id,workflow_id,revision,phase,updated_at,payload)
        VALUES($1,$2,$3,$4,$5,$6) ON CONFLICT(handoff_id) DO UPDATE SET
            revision=EXCLUDED.revision,phase=EXCLUDED.phase,updated_at=EXCLUDED.updated_at,payload=EXCLUDED.payload
        WHERE strategy_execution_handoff.revision < EXCLUDED.revision;
        """;

    /// <summary>SQL for upsert workflow template.</summary>
    internal const string UpsertWorkflowTemplate =
        """
        INSERT INTO trade.{0} ({1})
        VALUES ({2})
        ON CONFLICT ({3}) DO UPDATE SET {4}{5};
        """;
}
