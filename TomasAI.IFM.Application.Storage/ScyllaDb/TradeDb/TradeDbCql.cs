namespace TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

internal class TradeDbCql
{
    public const string CreateOptionLegDataTable = """
        CREATE TABLE IF NOT EXISTS option_leg_data (
            orderId int,
            tradeId int,
            valueDate date,
            tradeType text,
            daysToExpiry int,
            tradeStatus text,
            optionLegId text,
            bidPrice decimal,
            askPrice decimal,
            impliedVolatility double,
            delta double,
            gamma double,
            theta double,
            vega double,
            rho double,
            createdOn timestamp,
            createdBy text,
            updatedOn timestamp,
            updatedBy text,
            PRIMARY KEY (orderId, tradeId, valueDate, tradeType, daysToExpiry, tradeStatus, optionLegId)
        ) with clustering order by(tradeId desc, valueDate desc, tradeType desc, daysToExpiry desc, tradeStatus asc, optionLegId asc);
    """;

    public const string CreateOptionLegTable = """
     CREATE TABLE IF NOT EXISTS option_leg (
	    orderId int,
        tradeId int,
        contractId text,
        quantity int,
        strikePrice decimal,
        optionLegType text,
        optionLegAction text,
        createdOn timestamp,
        createdBy text,
        updatedOn timestamp,
        updatedBy text,
        PRIMARY KEY (orderId, tradeId, contractId)
    );
""";
    public const string CreateOptionTradeSpreadBarDataTable = """
        CREATE TABLE IF NOT EXISTS option_trade_spread_bar_data (
    orderId int,
    tradeId int,
    valueDate date,
    tradeType text,
    barDate timestamp,
    lossLimit decimal,
    winLimit decimal,
    forwardSpread decimal,
    netSpread decimal,
    PRIMARY KEY (orderId, tradeId, valueDate, tradeType, barDate)
) with clustering order by (tradeid desc, valueDate desc, tradeType asc, barDate desc);
""";
    public const string CreateOptionTradeTable = """
        CREATE TABLE IF NOT EXISTS option_trade (
    orderId int,
    tradeId int,
    tradeStrategy text,
    tradeDate date,
    maturityDate date,
    tradeType text,
    tradeState text,
    tradeAction text,
    underlyingContractId text,
    underlyingAssetType text,
    isPrimaryTrade boolean,
    isHedgeTrade boolean,
    createdOn timestamp,
    createdBy text,
    updatedOn timestamp,
    updatedBy text,
    PRIMARY KEY (orderId, tradeId)
) with clustering order by (tradeId desc);
""";
    public const string CreateTradeFillDataTable = """
        CREATE TABLE IF NOT EXISTS trade_fill_data (
            orderId int,
            tradeId int,
            contractId text,
            fillDate timestamp,
            bidPrice decimal,
            askPrice decimal,
            commission decimal,
            optionLegAction text,
            createdOn timestamp,
            createdBy text,
            PRIMARY KEY (orderId, tradeId, fillDate, contractId)
        ) WITH CLUSTERING ORDER BY (tradeId desc, fillDate desc, contractId asc);
""";

    public const string CreateTradeFillTable = """
        CREATE TABLE IF NOT EXISTS trade_fill (
            orderId int,
            tradeId int,
            fillDate timestamp,
            fillQuantity int,
            createdOn timestamp,
            createdBy text,
            PRIMARY KEY (orderId, tradeId, fillDate)
        ) WITH CLUSTERING ORDER BY (tradeId desc, fillDate desc);
    """;

    public const string CreateTradeLimitTable = """
        CREATE TABLE IF NOT EXISTS trade_limit (
    tradeId int,
    tradeType text,
    riskMargin decimal,
    maxProfit decimal,
    maxLoss decimal,
    maxReturn decimal,
    maxLossLimit decimal,
    minProfitLimit decimal,
    maxProfitLimit decimal,
    minProfitTarget decimal,
    dailyProfitTarget decimal,
    createdOn timestamp,
    createdBy text,
    updatedOn timestamp,
    updatedBy text,
    PRIMARY KEY (tradeId, tradeType)
) WITH CLUSTERING ORDER BY (tradeType ASC);
""";
    public const string CreateTradeLiveFeedTable = """
        CREATE TABLE IF NOT EXISTS trade_live_feed (
    orderId int,
    tradeId int,
    liveFeed boolean,
    PRIMARY KEY ((orderId), tradeId)
) WITH CLUSTERING ORDER BY (tradeId DESC);
""";
    public const string CreateTradeOrderTable = """
        CREATE TABLE IF NOT EXISTS trade_order (
    valueDate date,
    tradeId int,
    orderId int,
    fundId int,
    tradeType text,
    tradeSubType text,
    tradeDate date,
    maturityDate date,
    tradeOrderState text,
    underlyingContractId text,
    underlyingAssetType text,
    orderDescription text,
    orderAction text,
    orderActionType text,
    orderQuantity int,
    orderFilled int,
    orderType text,
    orderPrice decimal,
    orderAmount decimal,
    commission decimal,
    totalAmount decimal,
    tradePnl decimal,
    tradeFillType text,
    createdOn timestamp,
    createdBy text,
    updatedOn timestamp,
    updatedBy text,
    PRIMARY KEY (tradeId, valueDate)
) WITH CLUSTERING ORDER BY (valueDate DESC);
""";
    public const string CreateTradePlacementSignalTable = """
        CREATE TABLE IF NOT EXISTS trade_placement_signal (
    sequenceId bigint,
    contractId text,
    valueDate date,
    tradePlacementSignal text,
    tradePrice decimal,
    createdOn timestamp,
    createdBy text,
    PRIMARY KEY (contractId, valueDate, sequenceId)
) WITH CLUSTERING ORDER BY (valueDate DESC, sequenceId DESC);
""";
    public const string CreateTradePlanForwardLossLimitTable = """
        CREATE TABLE IF NOT EXISTS trade_plan_forward_loss_limit (
    orderId int,
    tradeId int,
    valueDate date,
    tradeType text,
    limitType text,
    PRIMARY KEY (orderId, tradeId, valueDate, tradeType)
) WITH CLUSTERING ORDER BY (tradeId DESC, valueDate DESC, tradeType ASC);
""";
    public const string CreateTradePlanTable = """
        CREATE TABLE IF NOT EXISTS trade_plan (
    orderId int,
    tradeId int,
    valueDate date,
    sequenceId bigint,
    actionDate timestamp,
    tradeDate date,
    maturityDate date,
    tradeType text,
    actionType text,
    actionSubType text,
    actionState text,
    actionReason text,
    tradePnl decimal,
    forwardLossRatio double,
    lossProbability double,
    mScore double,
    maxProfit decimal,
    maxLoss decimal,
    minProfitTarget decimal,
    dailyProfitTarget decimal,
    assetPrice decimal,
    assetStdDev double,
    assetMean double,
    assetPriceChange double,
    marketTrend text,
    marketVolatility text,
    marketDirection text,
    vixVolatility text,
    tradeRisk text,
    fiftyDayMA double,
    fiveDayXMA double,
    putOTMProbability double,
    callOTMProbability double,
    shortPutGamma double,
    shortCallGamma double,
    gammaRisk text,
    netPrice decimal,
    forwardPrice decimal,
    forwardDelta double,
    stopLossLimit double,
    trendType text,
    trendStrength text,
    rsi double,
    rsiSlope double,
    tdi text,
    tdiStrength text,
    createdOn timestamp,
    createdBy text,
    PRIMARY KEY (orderId, tradeId, valueDate, sequenceId)
) WITH CLUSTERING ORDER BY (tradeId DESC, valueDate DESC, sequenceId DESC);
""";
    public const string CreateTradePositionStateTable = """
        CREATE TABLE IF NOT EXISTS trade_position_state (
    orderId int,
    tradeId int,
    tradePositionState text,
    openedOn timestamp,
    openedBy text,
    PRIMARY KEY (orderId, tradeId)
) WITH CLUSTERING ORDER BY (tradeId desc);
""";
    public const string CreateTradePositionTable = """
        CREATE TABLE IF NOT EXISTS trade_position (
    orderId int,
    tradeId int,
    valueDate date,
    tradeType text,
    tradeStatus text,
    daysToExpiry int,
    commission decimal,
    deltaHedge int,
    netSpread decimal,
    tradeValue decimal,
    tradePnl decimal,
    assetPrice decimal,
    otmProbability double,
    forwardPrice decimal,
    forwardLossRatio double,
    lossProbability double,
    riskFreeRate double,
    createdOn timestamp,
    createdBy text,
    updatedOn timestamp,
    updatedBy text,
    PRIMARY KEY (orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType)
);
""";
    public const string CreateTradeTypeLimitTable = """
        CREATE TABLE IF NOT EXISTS trade_type_limit (
    tradeId int,
    tradeType text,
    maxLossLimit decimal,
    minProfitLimit decimal,
    maxProfitLimit decimal,
    PRIMARY KEY (tradeId, tradeType)
) WITH CLUSTERING ORDER BY (tradeType ASC);
""";
    public const string DeleteOptionTrade = """
        delete from option_trade
where orderid = :orderId
and tradeid = :tradeId
""";
    public const string GetOptionTradeSpreadData = """
        SELECT 
    orderId AS "OrderId",
    tradeId AS "TradeId",
    valueDate AS "ValueDate",
    tradeType AS "TradeType",
    sequenceId AS "SequenceId",
    lossLimit AS "LossLimit",
    winLimit AS "WinLimit",
    forwardSpread AS "ForwardSpread",
    netSpread AS "NetSpread",
    createdOn AS "CreatedOn",
    createdBy AS "CreatedBy"
FROM 
    option_trade_spread_data
WHERE
    orderId = :orderId
    AND tradeId = :tradeId
    AND valueDate = :valueDate
    AND tradeType = :tradeType
LIMIT 1;
""";

    public const string GetOptionTradeSpreadDataAll = """
        SELECT
            orderId AS "OrderId",
            tradeId AS "TradeId",
            valueDate AS "ValueDate",
            tradeType AS "TradeType",
            sequenceId AS "SequenceId",
            lossLimit AS "LossLimit",
            winLimit AS "WinLimit",
            forwardSpread AS "ForwardSpread",
            netSpread AS "NetSpread",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy"
        FROM option_trade_spread_data
        """;
        
    public const string GetOptionTrade = """
        SELECT orderId AS "OrderId",
       tradeId AS "TradeId",
       tradeStrategy AS "TradeStrategy",
       tradeDate AS "TradeDate",
       maturityDate AS "MaturityDate",
       tradeType AS "TradeType",
       tradeState AS "TradeState",
       tradeAction AS "TradeAction",
       underlyingContractId AS "UnderlyingContractId",
       underlyingAssetType AS "UnderlyingAssetType",
       isPrimaryTrade AS "IsPrimaryTrade",
       isHedgeTrade AS "IsHedgeTrade",
       createdOn AS "CreatedOn",
       createdBy AS "CreatedBy",
       updatedOn AS "UpdatedOn",
       updatedBy AS "UpdatedBy"
FROM option_trade
WHERE orderId = :orderId 
and tradeId = :tradeId
""";
    public const string GetOptionTrades = """
        SELECT orderId AS "OrderId",
       tradeId AS "TradeId",
       tradeStrategy AS "TradeStrategy",
       tradeDate AS "TradeDate",
       maturityDate AS "MaturityDate",
       tradeType AS "TradeType",
       tradeState AS "TradeState",
       tradeAction AS "TradeAction",
       underlyingContractId AS "UnderlyingContractId",
       underlyingAssetType AS "UnderlyingAssetType",
       isPrimaryTrade AS "IsPrimaryTrade",
       isHedgeTrade AS "IsHedgeTrade",
       createdOn AS "CreatedOn",
       createdBy AS "CreatedBy",
       updatedOn AS "UpdatedOn",
       updatedBy AS "UpdatedBy"
FROM option_trade
WHERE orderId = :orderId;
""";

    public const string GetOptionTradesAll = """
        SELECT orderId AS "OrderId",
           tradeId AS "TradeId",
           tradeStrategy AS "TradeStrategy",
           tradeDate AS "TradeDate",
           maturityDate AS "MaturityDate",
           tradeType AS "TradeType",
           tradeState AS "TradeState",
           tradeAction AS "TradeAction",
           underlyingContractId AS "UnderlyingContractId",
           underlyingAssetType AS "UnderlyingAssetType",
           isPrimaryTrade AS "IsPrimaryTrade",
           isHedgeTrade AS "IsHedgeTrade",
           createdOn AS "CreatedOn",
           createdBy AS "CreatedBy",
           updatedOn AS "UpdatedOn",
           updatedBy AS "UpdatedBy"
        FROM option_trade;
    """;

    public const string InsertOptionTrade = """
        INSERT INTO option_trade (
        orderId,
        tradeId,
        tradeDate,
        maturityDate,
        tradeType,
        tradeState,
        tradeStrategy,
        tradeAction,
        underlyingContractId,
        underlyingAssetType,
        isPrimaryTrade,
        isHedgeTrade,
        createdOn,
        createdBy,
        updatedOn,
        updatedBy
    ) VALUES (
        :orderId,
        :tradeId,
        :tradeDate,
        :maturityDate,
        :tradeType,
        :tradeState,
        :tradeStrategy,
        :tradeAction,
        :underlyingContractId,
        :underlyingAssetType,
        :isPrimaryTrade,
        :isHedgeTrade,
        :createdOn,
        :createdBy,
        :updatedOn,
        :updatedBy
    );
""";
    public const string UpdateOptionTrade = """
        UPDATE option_trade
SET tradeDate = :tradeDate,
    maturityDate = :maturityDate,
    tradeType = :tradeType,
    tradeState = :tradeState,
    tradeStrategy = :tradeStrategy,
    tradeAction = :tradeAction,
    underlyingContractId = :underlyingContractId,
    underlyingAssetType = :underlyingAssetType,
    isPrimaryTrade = :isPrimaryTrade,
    isHedgeTrade = :isHedgeTrade,
    updatedOn = :updatedOn,
    updatedBy = :updatedBy
WHERE orderId = :orderId
  AND tradeId = :tradeId;
""";
    public const string GetOptionTradeSpreadBarData = """
        SELECT 
    orderId AS "OrderId",
    tradeId AS "TradeId",
    tradeType AS "TradeType",
    valueDate AS "ValueDate",
    barDate AS "BarDate",
    lossLimit AS "LossLimit",
    winLimit AS "WinLimit",
    forwardSpread AS "ForwardSpread",
    netSpread AS "NetSpread"
FROM option_trade_spread_bar_data
WHERE orderId = :orderId
  AND tradeId = :tradeId
  AND valueDate = :valueDate
  AND tradeType = :tradeType
  AND barDate >= :startDate
  AND barDate <= :endDate;
""";

    public const string GetOptionTradeSpreadBarDataAll = """
        SELECT 
            orderId AS "OrderId",
            tradeId AS "TradeId",
            tradeType AS "TradeType",
            valueDate AS "ValueDate",
            barDate AS "BarDate",
            lossLimit AS "LossLimit",
            winLimit AS "WinLimit",
            forwardSpread AS "ForwardSpread",
            netSpread AS "NetSpread"
        FROM option_trade_spread_bar_data;
        """;

    public const string GetTradePositions = """
        SELECT 
            orderId AS "OrderId",
            tradeId AS "TradeId",
            tradeType AS "TradeType",
            valueDate AS "ValueDate",
            tradeStatus AS "TradeStatus",
            daysToExpiry AS "DaysToExpiry",
            commission AS "Commission",
            deltaHedge AS "DeltaHedge",
            netSpread AS "NetSpread",
            tradeValue AS "TradeValue",
            tradePnl AS "TradePnl",
            assetPrice AS "AssetPrice",
            otmProbability AS "OTMProbability",
            forwardPrice AS "ForwardPrice",
            forwardLossRatio AS "ForwardLossRatio",
            lossProbability AS "LossProbability",
            riskFreeRate AS "RiskFreeRate",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy",
            updatedOn AS "UpdatedOn",
            updatedBy AS "UpdatedBy"
        FROM trade_position
        WHERE orderId = :orderId
        AND tradeId = :tradeId;
""";

    public const string GetTradePositionsById = """
        SELECT 
            orderId AS "OrderId",
            tradeId AS "TradeId",
            tradeType AS "TradeType",
            valueDate AS "ValueDate",
            tradeStatus AS "TradeStatus",
            daysToExpiry AS "DaysToExpiry",
            commission AS "Commission",
            deltaHedge AS "DeltaHedge",
            netSpread AS "NetSpread",
            tradeValue AS "TradeValue",
            tradePnl AS "TradePnl",
            assetPrice AS "AssetPrice",
            otmProbability AS "OTMProbability",
            forwardPrice AS "ForwardPrice",
            forwardLossRatio AS "ForwardLossRatio",
            lossProbability AS "LossProbability",
            riskFreeRate AS "RiskFreeRate",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy",
            updatedOn AS "UpdatedOn",
            updatedBy AS "UpdatedBy"
        FROM trade_position
        WHERE orderId = :orderId
        AND tradeId = :tradeId
        AND valueDate = :valueDate
        AND tradeStatus = :tradeStatus
        AND daysToExpiry = :daysToExpiry;
""";

    public const string GetTradePositionsAll = """
        SELECT 
            orderId AS "OrderId",
            tradeId AS "TradeId",
            tradeType AS "TradeType",
            valueDate AS "ValueDate",
            daysToExpiry AS "DaysToExpiry",
            tradeStatus AS "TradeStatus",
            commission AS "Commission",
            deltaHedge AS "DeltaHedge",
            netSpread AS "NetSpread",
            tradeValue AS "TradeValue",
            tradePnl AS "TradePnl",
            assetPrice AS "AssetPrice",
            otmProbability AS "OTMProbability",
            forwardPrice AS "ForwardPrice",
            forwardLossRatio AS "ForwardLossRatio",
            lossProbability AS "LossProbability",
            riskFreeRate AS "RiskFreeRate",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy",
            updatedOn AS "UpdatedOn",
            updatedBy AS "UpdatedBy"
        FROM trade_position;
""";


    public const string GetOptionLegs = """
        SELECT 
            orderId AS "OrderId",
            tradeId AS "TradeId",
            contractId AS "ContractId",
            quantity AS "Quantity",
            strikePrice AS "StrikePrice",
            optionLegType AS "OptionLegType",
            optionLegAction AS "OptionLegAction",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy",
            updatedOn AS "UpdatedOn",
            updatedBy AS "UpdatedBy"
        FROM option_leg
        WHERE tradeId = :tradeId;
        """;

    public const string GetOptionLegsAll = """
        SELECT 
            orderId AS "OrderId",
            tradeId AS "TradeId",
            contractId AS "ContractId",
            quantity AS "Quantity",
            strikePrice AS "StrikePrice",
            optionLegType AS "OptionLegType",
            optionLegAction AS "OptionLegAction",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy",
            updatedOn AS "UpdatedOn",
            updatedBy AS "UpdatedBy"
        FROM option_leg
        """;

    public const string GetOptionLegData = """
        SELECT 
            OrderId AS "OrderId",
            TradeId AS "TradeId",
           TradeType AS "TradeType",
           ValueDate AS "ValueDate",
           DaysToExpiry AS "DaysToExpiry",
           TradeStatus AS "TradeStatus",
           OptionLegId AS "OptionLegId",
           BidPrice AS "BidPrice",
           AskPrice AS "AskPrice",
           ImpliedVolatility AS "ImpliedVolatility",
           Delta AS "Delta",
           Gamma AS "Gamma",
           Theta AS "Theta",
           Vega AS "Vega",
           Rho AS "Rho",
           CreatedOn AS "CreatedOn",
           CreatedBy AS "CreatedBy",
           UpdatedOn AS "UpdatedOn",
           UpdatedBy AS "UpdatedBy"
    FROM option_leg_data
    WHERE OrderId = :orderId
    AND TradeId = :tradeId
    AND ValueDate = :valueDate;
    """;

    public const string GetOptionLegDataAll = """
        SELECT  OrderId AS "OrderId",
           TradeId AS "TradeId",
           TradeType AS "TradeType",
           ValueDate AS "ValueDate",
           DaysToExpiry AS "DaysToExpiry",
           TradeStatus AS "TradeStatus",
           OptionLegId AS "OptionLegId",
           BidPrice AS "BidPrice",
           AskPrice AS "AskPrice",
           ImpliedVolatility AS "ImpliedVolatility",
           Delta AS "Delta",
           Gamma AS "Gamma",
           Theta AS "Theta",
           Vega AS "Vega",
           Rho AS "Rho",
           CreatedOn AS "CreatedOn",
           CreatedBy AS "CreatedBy",
           UpdatedOn AS "UpdatedOn",
           UpdatedBy AS "UpdatedBy"
        FROM option_leg_data;
        """;

    public const string GetTradeTypeLimits = """
        SELECT TradeId AS "TradeId",
       TradeType AS "TradeType",
       MaxLossLimit AS "MaxLossLimit",
       MinProfitLimit AS "MinProfitLimit",
       MaxProfitLimit AS "MaxProfitLimit"
FROM trade_type_limit
WHERE TradeId = :tradeId;
""";

    public const string GetTradeTypeLimitAll = """
        SELECT TradeId AS "TradeId",
           TradeType AS "TradeType",
           MaxLossLimit AS "MaxLossLimit",
           MinProfitLimit AS "MinProfitLimit",
           MaxProfitLimit AS "MaxProfitLimit"
        FROM trade_type_limit;
    """;
    public const string GetTradeHistory = """
        SELECT orderId AS "OrderId",
       tradeId AS "TradeId",
       valueDate AS "ValueDate",
       tradeStatus AS "TradeStatus",
       daysToExpiry AS "DaysToExpiry",
       tradeType AS "TradeType",
       SUM(commission) AS "Commission",
       SUM(netSpread) AS "NetSpread",
       SUM(tradePnl) AS "TradePnl"
FROM trade_position 
WHERE orderId = :orderId
GROUP BY orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType
""";
    public const string GetTradePosition = """
        SELECT 
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    TradeType AS "TradeType",
    ValueDate AS "ValueDate",
    DaysToExpiry AS "DaysToExpiry",
    TradeStatus AS "TradeStatus",
    Commission AS "Commission",
    DeltaHedge AS "DeltaHedge",
    NetSpread AS "NetSpread",
    TradeValue AS "TradeValue",
    TradePnl AS "TradePnl",
    AssetPrice AS "AssetPrice",
    OTMProbability AS "OTMProbability",
    ForwardPrice AS "ForwardPrice",
    ForwardLossRatio AS "ForwardLossRatio",
    LossProbability AS "LossProbability",
    RiskFreeRate AS "RiskFreeRate",
    CreatedOn AS "CreatedOn",
    CreatedBy AS "CreatedBy",
    UpdatedOn AS "UpdatedOn",
    UpdatedBy AS "UpdatedBy"
FROM trade_position
WHERE OrderId = :orderId
  AND TradeId = :tradeId
  AND ValueDate = :valueDate
  AND TradeStatus = :tradeStatus
  AND DaysToExpiry = :daysToExpiry
  AND TradeType = :tradeType;
""";
    public const string GetTradeOrders = """
        SELECT 
    fundId AS "FundId",
    orderId AS "OrderId",
    tradeId AS "TradeId",
    valueDate AS "ValueDate",
    tradeType AS "TradeType",
    tradeSubType AS "TradeSubType",
    tradeDate AS "TradeDate",
    maturityDate AS "MaturityDate",
    tradeOrderState AS "TradeOrderState",
    underlyingContractId AS "UnderlyingContractId",
    underlyingAssetType AS "UnderlyingAssetType",
    orderDescription AS "OrderDescription",
    orderAction AS "OrderAction",
    orderActionType AS "OrderActionType",
    orderQuantity AS "OrderQuantity",
    orderType AS "OrderType",
    orderPrice AS "OrderPrice",
    orderAmount AS "OrderAmount",
    commission AS "Commission",
    totalAmount AS "TotalAmount",
    tradePnl AS "TradePnl",
    tradeFillType AS "TradeFillType",
    createdOn AS "CreatedOn",
    createdBy AS "CreatedBy",
    updatedOn AS "UpdatedOn",
    updatedBy AS "UpdatedBy"
FROM trade_order
where valueDate >= :startDate
and valueDate <= :endDate
order by valueDate desc
""";
    public const string GetTradeTypeLimit = """
        SELECT TradeId AS "TradeId",
 TradeType AS "TradeType",
    MaxLossLimit AS "MaxLossLimit",
    MaxProfitLimit as "MaxProfitLimit",
    MinProfitLimit AS "MinProfitLimit"
FROM trade_type_limit
WHERE TradeId = :tradeId 
AND TradeType = :tradeType;
""";
    public const string GetTradeLimit = """
        SELECT TradeId AS "TradeId",
       TradeType AS "TradeType",
       RiskMargin AS "RiskMargin",
       MaxProfit AS "MaxProfit",
       MaxLoss AS "MaxLoss",
       MaxReturn AS "MaxReturn",
       MaxLossLimit AS "MaxLossLimit",
       MinProfitLimit AS "MinProfitLimit",
       MaxProfitLimit AS "MaxProfitLimit",
       MinProfitTarget AS "MinProfitTarget",
       DailyProfitTarget AS "DailyProfitTarget",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy",
       UpdatedOn AS "UpdatedOn",
       UpdatedBy AS "UpdatedBy"
FROM trade_limit
WHERE TradeId = :tradeId;
""";

    public const string GetTradeLimitAll = """
        SELECT TradeId AS "TradeId",
           TradeType AS "TradeType",
           RiskMargin AS "RiskMargin",
           MaxProfit AS "MaxProfit",
           MaxLoss AS "MaxLoss",
           MaxReturn AS "MaxReturn",
           MaxLossLimit AS "MaxLossLimit",
           MinProfitLimit AS "MinProfitLimit",
           MaxProfitLimit AS "MaxProfitLimit",
           MinProfitTarget AS "MinProfitTarget",
           DailyProfitTarget AS "DailyProfitTarget",
           CreatedOn AS "CreatedOn",
           CreatedBy AS "CreatedBy",
           UpdatedOn AS "UpdatedOn",
           UpdatedBy AS "UpdatedBy"
        FROM trade_limit;
    """;

    public const string GetTradeFills = """
        SELECT OrderId AS "OrderId",
               TradeId AS "TradeId",
               FillDate AS "FillDate",
               FillQuantity AS "FillQuantity",
               CreatedOn AS "CreatedOn",
               CreatedBy AS "CreatedBy"
        FROM trade_fill
        WHERE Orderid = :orderId
        AND TradeId = :tradeId;
""";

    public const string GetTradeFillsAll = """
        SELECT OrderId AS "OrderId",
               TradeId AS "TradeId",
               FillDate AS "FillDate",
               FillQuantity AS "FillQuantity",
               CreatedOn AS "CreatedOn",
               CreatedBy AS "CreatedBy"
        FROM trade_fill;
        """;

    public const string GetTradeFillData = """
        SELECT OrderId AS "OrderId",
       TradeId AS "TradeId",
       ContractId AS "ContractId",
       FillDate AS "FillDate",
       BidPrice AS "BidPrice",
       AskPrice AS "AskPrice",
       Commission AS "Commission",
       OptionLegAction AS "OptionLegAction",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy"
FROM trade_fill_data
WHERE OrderId = :orderId 
AND TradeId = :tradeId 
AND FillDate >= :fillDate;
""";

    public const string GetTradePlacementSignal = """
        SELECT 
            contractId AS "ContractId",
            valueDate AS "ValueDate",
            sequenceId AS "SequenceId",
            tradePlacementSignal AS "TradePlacementSignal",
            tradePrice AS "TradePrice",
            createdOn AS "CreatedOn",
            createdBy AS "CreatedBy"
        FROM trade_placement_signal
        WHERE contractId = :contractId
          AND valueDate = :valueDate
          LIMIT 1;
        """;
    public const string GetTradePlans = """
        SELECT SequenceId AS "SequenceId",
       OrderId AS "OrderId",
       TradeId AS "TradeId",
       ValueDate AS "ValueDate",
       ActionDate AS "ActionDate",
       TradeDate AS "TradeDate",
       MaturityDate AS "MaturityDate",
       TradeType AS "TradeType",
       ActionType AS "ActionType",
       ActionSubType AS "ActionSubType",
       ActionState AS "ActionState",
       ActionReason AS "ActionReason",
       TradePnl AS "TradePnl",
       ForwardLossRatio AS "ForwardLossRatio",
       LossProbability AS "LossProbability",
       MScore AS "MScore",
       MaxProfit AS "MaxProfit",
       MaxLoss AS "MaxLoss",
       MinProfitTarget AS "MinProfitTarget",
       DailyProfitTarget AS "DailyProfitTarget",
       AssetPrice AS "AssetPrice",
       AssetStdDev AS "AssetStdDev",
       AssetMean AS "AssetMean",
       AssetPriceChange AS "AssetPriceChange",
       MarketTrend AS "MarketTrend",
       MarketVolatility AS "MarketVolatility",
       MarketDirection AS "MarketDirection",
       VixVolatility AS "VixVolatility",
       TradeRisk AS "TradeRisk",
       FiftyDayMA AS "FiftyDayMA",
       FiveDayXMA AS "FiveDayXMA",
       PutOTMProbability AS "PutOTMProbability",
       CallOTMProbability AS "CallOTMProbability",
       ShortPutGamma AS "ShortPutGamma",
       ShortCallGamma AS "ShortCallGamma",
       GammaRisk AS "GammaRisk",
       NetPrice AS "NetPrice",
       ForwardPrice AS "ForwardPrice",
       ForwardDelta AS "ForwardDelta",
       StopLossLimit AS "StopLossLimit",
       TrendType AS "TrendType",
       TrendStrength AS "TrendStrength",
       RSI AS "RSI",
       RSISlope AS "RSISlope",
       TDI AS "TDI",
       TDIStrength AS "TDIStrength",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy"
FROM trade_plan
WHERE OrderId = :orderId
AND TradeId = :tradeId
AND ValueDate = :valueDate;
""";

    public const string GetLastTradePlans = """
        SELECT SequenceId AS "SequenceId",
       OrderId AS "OrderId",
       TradeId AS "TradeId",
       ValueDate AS "ValueDate",
       ActionDate AS "ActionDate",
       TradeDate AS "TradeDate",
       MaturityDate AS "MaturityDate",
       TradeType AS "TradeType",
       ActionType AS "ActionType",
       ActionSubType AS "ActionSubType",
       ActionState AS "ActionState",
       ActionReason AS "ActionReason",
       TradePnl AS "TradePnl",
       ForwardLossRatio AS "ForwardLossRatio",
       LossProbability AS "LossProbability",
       MScore AS "MScore",
       MaxProfit AS "MaxProfit",
       MaxLoss AS "MaxLoss",
       MinProfitTarget AS "MinProfitTarget",
       DailyProfitTarget AS "DailyProfitTarget",
       AssetPrice AS "AssetPrice",
       AssetStdDev AS "AssetStdDev",
       AssetMean AS "AssetMean",
       AssetPriceChange AS "AssetPriceChange",
       MarketTrend AS "MarketTrend",
       MarketVolatility AS "MarketVolatility",
       MarketDirection AS "MarketDirection",
       VixVolatility AS "VixVolatility",
       TradeRisk AS "TradeRisk",
       FiftyDayMA AS "FiftyDayMA",
       FiveDayXMA AS "FiveDayXMA",
       PutOTMProbability AS "PutOTMProbability",
       CallOTMProbability AS "CallOTMProbability",
       ShortPutGamma AS "ShortPutGamma",
       ShortCallGamma AS "ShortCallGamma",
       GammaRisk AS "GammaRisk",
       NetPrice AS "NetPrice",
       ForwardPrice AS "ForwardPrice",
       ForwardDelta AS "ForwardDelta",
       StopLossLimit AS "StopLossLimit",
       TrendType AS "TrendType",
       TrendStrength AS "TrendStrength",
       RSI AS "RSI",
       RSISlope AS "RSISlope",
       TDI AS "TDI",
       TDIStrength AS "TDIStrength",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy"
FROM trade_plan
WHERE OrderId = :orderId
And TradeId = :tradeId;
""";

    public const string GetTradePlansAll = """
        SELECT SequenceId AS "SequenceId",
           OrderId AS "OrderId",
           TradeId AS "TradeId",
           ValueDate AS "ValueDate",
           ActionDate AS "ActionDate",
           TradeDate AS "TradeDate",
           MaturityDate AS "MaturityDate",
           TradeType AS "TradeType",
           ActionType AS "ActionType",
           ActionSubType AS "ActionSubType",
           ActionState AS "ActionState",
           ActionReason AS "ActionReason",
           TradePnl AS "TradePnl",
           ForwardLossRatio AS "ForwardLossRatio",
           LossProbability AS "LossProbability",
           MScore AS "MScore",
           MaxProfit AS "MaxProfit",
           MaxLoss AS "MaxLoss",
           MinProfitTarget AS "MinProfitTarget",
           DailyProfitTarget AS "DailyProfitTarget",
           AssetPrice AS "AssetPrice",
           AssetStdDev AS "AssetStdDev",
           AssetMean AS "AssetMean",
           AssetPriceChange AS "AssetPriceChange",
           MarketTrend AS "MarketTrend",
           MarketVolatility AS "MarketVolatility",
           MarketDirection AS "MarketDirection",
           VixVolatility AS "VixVolatility",
           TradeRisk AS "TradeRisk",
           FiftyDayMA AS "FiftyDayMA",
           FiveDayXMA AS "FiveDayXMA",
           PutOTMProbability AS "PutOTMProbability",
           CallOTMProbability AS "CallOTMProbability",
           ShortPutGamma AS "ShortPutGamma",
           ShortCallGamma AS "ShortCallGamma",
           GammaRisk AS "GammaRisk",
           NetPrice AS "NetPrice",
           ForwardPrice AS "ForwardPrice",
           ForwardDelta AS "ForwardDelta",
           StopLossLimit AS "StopLossLimit",
           TrendType AS "TrendType",
           TrendStrength AS "TrendStrength",
           RSI AS "RSI",
           RSISlope AS "RSISlope",
           TDI AS "TDI",
           TDIStrength AS "TDIStrength",
           CreatedOn AS "CreatedOn",
           CreatedBy AS "CreatedBy"
    FROM trade_plan;
""";


    public const string GetTradePlansByValueDate = """
        SELECT SequenceId AS "SequenceId",
       OrderId AS "OrderId",
       TradeId AS "TradeId",
       ValueDate AS "ValueDate",
       ActionDate AS "ActionDate",
       TradeDate AS "TradeDate",
       MaturityDate AS "MaturityDate",
       TradeType AS "TradeType",
       ActionType AS "ActionType",
       ActionSubType AS "ActionSubType",
       ActionState AS "ActionState",
       ActionReason AS "ActionReason",
       TradePnl AS "TradePnl",
       ForwardLossRatio AS "ForwardLossRatio",
       LossProbability AS "LossProbability",
       MScore AS "MScore",
       MaxProfit AS "MaxProfit",
       MaxLoss AS "MaxLoss",
       MinProfitTarget AS "MinProfitTarget",
       DailyProfitTarget AS "DailyProfitTarget",
       AssetPrice AS "AssetPrice",
       AssetStdDev AS "AssetStdDev",
       AssetMean AS "AssetMean",
       AssetPriceChange AS "AssetPriceChange",
       MarketTrend AS "MarketTrend",
       MarketVolatility AS "MarketVolatility",
       MarketDirection AS "MarketDirection",
       VixVolatility AS "VixVolatility",
       TradeRisk AS "TradeRisk",
       FiftyDayMA AS "FiftyDayMA",
       FiveDayXMA AS "FiveDayXMA",
       PutOTMProbability AS "PutOTMProbability",
       CallOTMProbability AS "CallOTMProbability",
       ShortPutGamma AS "ShortPutGamma",
       ShortCallGamma AS "ShortCallGamma",
       GammaRisk AS "GammaRisk",
       NetPrice AS "NetPrice",
       ForwardPrice AS "ForwardPrice",
       ForwardDelta AS "ForwardDelta",
       StopLossLimit AS "StopLossLimit",
       TrendType AS "TrendType",
       TrendStrength AS "TrendStrength",
       RSI AS "RSI",
       RSISlope AS "RSISlope",
       TDI AS "TDI",
       TDIStrength AS "TDIStrength",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy"
FROM trade_plan
WHERE OrderId = :orderId
and tradeId = :tradeId
and valueDate = :valueDate;
""";
    public const string GetTradePlanStopLossLimit = """
        SELECT StopLossLimit AS "StopLossLimit"
FROM trade_plan
WHERE OrderId = :orderId AND TradeId = :tradeId
LIMIT 1;
""";
    public const string GetTradePlansByDateRange = """
        SELECT SequenceId AS "SequenceId",
       OrderId AS "OrderId",
       TradeId AS "TradeId",
       ValueDate AS "ValueDate",
       ActionDate AS "ActionDate",
       TradeDate AS "TradeDate",
       MaturityDate AS "MaturityDate",
       TradeType AS "TradeType",
       ActionType AS "ActionType",
       ActionSubType AS "ActionSubType",
       ActionState AS "ActionState",
       ActionReason AS "ActionReason",
       TradePnl AS "TradePnl",
       ForwardLossRatio AS "ForwardLossRatio",
       LossProbability AS "LossProbability",
       MScore AS "MScore",
       MaxProfit AS "MaxProfit",
       MaxLoss AS "MaxLoss",
       MinProfitTarget AS "MinProfitTarget",
       DailyProfitTarget AS "DailyProfitTarget",
       AssetPrice AS "AssetPrice",
       AssetStdDev AS "AssetStdDev",
       AssetMean AS "AssetMean",
       AssetPriceChange AS "AssetPriceChange",
       MarketTrend AS "MarketTrend",
       MarketVolatility AS "MarketVolatility",
       MarketDirection AS "MarketDirection",
       VixVolatility AS "VixVolatility",
       TradeRisk AS "TradeRisk",
       FiftyDayMA AS "FiftyDayMA",
       FiveDayXMA AS "FiveDayXMA",
       PutOTMProbability AS "PutOTMProbability",
       CallOTMProbability AS "CallOTMProbability",
       ShortPutGamma AS "ShortPutGamma",
       ShortCallGamma AS "ShortCallGamma",
       GammaRisk AS "GammaRisk",
       NetPrice AS "NetPrice",
       ForwardPrice AS "ForwardPrice",
       ForwardDelta AS "ForwardDelta",
       StopLossLimit AS "StopLossLimit",
       TrendType AS "TrendType",
       TrendStrength AS "TrendStrength",
       RSI AS "RSI",
       RSISlope AS "RSISlope",
       TDI AS "TDI",
       TDIStrength AS "TDIStrength",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy"
FROM trade_plan
WHERE OrderId = :orderId
and tradeId = :tradeId
and valueDate >= :startDate
and valueDate <= :endDate
""";
    public const string CreateTradePlanForwardLossRatioTable = """
        create table if not exists trade_plan_forward_loss_ratio(
 partitionId int,
 valueDate date,
 forwardLossRatio double,
 sequenceId bigint,
 primary key (partitionId, valueDate, forwardLossRatio, sequenceId)
) with clustering order by (valueDate desc, forwardLossRatio asc, sequenceId desc)
""";
    public const string GetTradePlanForwardLossRatios = """
        select forwardLossRatio as "ForwardLossRatio"
from trade_plan_forward_loss_ratio
where partitionId = 1 
and  valueDate >= :startDate
and valueDate <= :endDate
""";
    public const string GetLastTradePlanForwardLossRatio = """
        select forwardLossRatio as "ForwardLossRatio"
from trade_plan_forward_loss_ratio
where partitionId = 1 
and valueDate = :valueDate
limit 1;
""";
    public const string GetTradePlanForwardLossLimit = """
        SELECT 
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    TradeType AS "TradeType",
    ValueDate AS "ValueDate",
    LimitType AS "LimitType"
FROM trade_plan_forward_loss_limit
WHERE OrderId = :orderId AND TradeId = :tradeId AND ValueDate = :valueDate AND TradeType = :tradeType;
""";
    public const string GetTradeLiveFeed = """
        SELECT 
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    LiveFeed AS "LiveFeed"
FROM trade_live_feed
WHERE OrderId = :orderId
AND TradeId = :tradeId;
""";
    public const string GetTradeOrder = """
        SELECT 
    FundId AS "FundId",
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    ValueDate AS "ValueDate",
    TradeType AS "TradeType",
    TradeSubType AS "TradeSubType",
    TradeDate AS "TradeDate",
    MaturityDate AS "MaturityDate",
    TradeOrderState AS "TradeOrderState",
    UnderlyingContractId AS "UnderlyingContractId",
    UnderlyingAssetType AS "UnderlyingAssetType",
    OrderDescription AS "OrderDescription",
    OrderAction AS "OrderAction",
    OrderActionType AS "OrderActionType",
    OrderQuantity AS "OrderQuantity",
    OrderType AS "OrderType",
    OrderPrice AS "OrderPrice",
    OrderAmount AS "OrderAmount",
    Commission AS "Commission",
    TotalAmount AS "TotalAmount",
    TradePnl AS "TradePnl",
    TradeFillType AS "TradeFillType",
    CreatedOn AS "CreatedOn",
    CreatedBy AS "CreatedBy",
    UpdatedOn AS "UpdatedOn",
    UpdatedBy AS "UpdatedBy"
FROM trade_order
WHERE tradeId = :tradeId AND valueDate = :valueDate;
""";
    public const string GetTradeOrdersByValueDate = """
        SELECT 
    FundId AS "FundId",
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    ValueDate AS "ValueDate",
    TradeType AS "TradeType",
    TradeSubType AS "TradeSubType",
    TradeDate AS "TradeDate",
    MaturityDate AS "MaturityDate",
    TradeOrderState AS "TradeOrderState",
    UnderlyingContractId AS "UnderlyingContractId",
    UnderlyingAssetType AS "UnderlyingAssetType",
    OrderDescription AS "OrderDescription",
    OrderAction AS "OrderAction",
    OrderActionType AS "OrderActionType",
    OrderQuantity AS "OrderQuantity",
    OrderType AS "OrderType",
    OrderPrice AS "OrderPrice",
    OrderAmount AS "OrderAmount",
    Commission AS "Commission",
    TotalAmount AS "TotalAmount",
    TradePnl AS "TradePnl",
    TradeFillType AS "TradeFillType",
    CreatedOn AS "CreatedOn",
    CreatedBy AS "CreatedBy",
    UpdatedOn AS "UpdatedOn",
    UpdatedBy AS "UpdatedBy"
FROM trade_order
WHERE valueDate = :valueDate;
""";
    public const string GetTradeFillDataByTradeId = """
        SELECT FundId AS "FundId",
       OrderId AS "OrderId",
       TradeId AS "TradeId",
       ContractId AS "ContractId",
       FillDate AS "FillDate",
       BidPrice AS "BidPrice",
       AskPrice AS "AskPrice",
       Commission AS "Commission",
       OptionLegAction AS "OptionLegAction",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy"
FROM trade_fill_data
WHERE TradeId = :tradeId;
""";
    public const string GetOptionLeg = """
        SELECT 
    tradeId AS "TradeId",
    contractId AS "ContractId",
    quantity AS "Quantity",
    strikePrice AS "StrikePrice",
    optionLegType AS "OptionLegType",
    optionLegAction AS "OptionLegAction",
    createdOn AS "CreatedOn",
    createdBy AS "CreatedBy",
    updatedOn AS "UpdatedOn",
    updatedBy AS "UpdatedBy"
FROM option_leg
WHERE tradeId = :tradeId;
AND contractId = :contractId
""";
    public const string InsertOptionLeg = """
        INSERT INTO option_leg (
            OrderId,
            TradeId,
            ContractId,
            Quantity,
            StrikePrice,
            OptionLegType,
            OptionLegAction,
            CreatedOn,
            CreatedBy,
            UpdatedOn,
            UpdatedBy
        ) VALUES (
            :orderId,
            :tradeId,
            :contractId,
            :quantity,
            :strikePrice,
            :optionLegType,
            :optionLegAction,
            :createdOn,
            :createdBy,
            :updatedOn,
            :updatedBy
        );
""";

    public const string InsertOptionLegData = """
        INSERT INTO option_leg_data (
            OrderId,
            TradeId,
            TradeType,
            ValueDate,
            DaysToExpiry,
            TradeStatus,
            OptionLegId,
            BidPrice,
            AskPrice,
            ImpliedVolatility,
            Delta,
            Gamma,
            Theta,
            Vega,
            Rho,
            CreatedOn,
            CreatedBy,
            UpdatedOn,
            UpdatedBy
        ) VALUES (
            :orderId,
            :tradeId,
            :tradeType,
            :valueDate,
            :daysToExpiry,
            :tradeStatus,
            :optionLegId,
            :bidPrice,
            :askPrice,
            :impliedVolatility,
            :delta,
            :gamma,
            :theta,
            :vega,
            :rho,
            :createdOn,
            :createdBy,
            :updatedOn,
            :updatedBy
        );
    """;

    public const string UpdateOptionLegData = """
        UPDATE option_leg_data
        SET 
            BidPrice = :bidPrice,
            AskPrice = :askPrice,
            ImpliedVolatility = :impliedVolatility,
            Delta = :delta,
            Gamma = :gamma,
            Theta = :theta,
            Vega = :vega,
            Rho = :rho,
            UpdatedOn = :updatedOn,
            UpdatedBy = :updatedBy
        WHERE OrderId = :orderId
        AND TradeId = :tradeId 
        AND ValueDate = :valueDate
        AND OptionLegId = :optionLegId;
""";
    public const string GetOptionLegDataByOptionLegId = """
        SELECT TradeId AS "TradeId",
       TradeType AS "TradeType",
       ValueDate AS "ValueDate",
       DaysToExpiry AS "DaysToExpiry",
       TradeStatus AS "TradeStatus",
       OptionLegId AS "OptionLegId",
       BidPrice AS "BidPrice",
       AskPrice AS "AskPrice",
       ImpliedVolatility AS "ImpliedVolatility",
       Delta AS "Delta",
       Gamma AS "Gamma",
       Theta AS "Theta",
       Vega AS "Vega",
       Rho AS "Rho",
       CreatedOn AS "CreatedOn",
       CreatedBy AS "CreatedBy",
       UpdatedOn AS "UpdatedOn",
       UpdatedBy AS "UpdatedBy"
FROM option_leg_data
WHERE TradeId = :tradeId
AND ValueDate = :valueDate
and OptionLegId = :optionLegId;
""";
    public const string DeleteTradePosition = """
        delete from trade_position
where orderid = :orderId
and tradeid = :tradeId
""";

    public const string DeleteTradePositionByPrimaryKey = """
        delete from trade_position
        where OrderId = :orderId
      AND TradeId = :tradeId
      AND ValueDate = :valueDate
      AND TradeStatus = :tradeStatus
      AND DaysToExpiry = :daysToExpiry
      AND TradeType = :tradeType;
""";

    public const string DeleteOptionLeg = """
        delete from option_leg
        where OrderId = :orderId
        and TradeId = :tradeId
        """;

    public const string DeleteOptionLegById = """
        delete from option_leg
        where OrderId = :orderId 
        and TradeId = :tradeId
        and ContractId = :contractId
        """;

    public const string DeleteOptionLegData = """
        delete from option_leg_data
        where orderId = :orderId 
        and tradeId = :tradeId;
        """;

    public const string DeleteOptionLegDataById = """
        delete from option_leg_data
        where orderId = :orderId 
        and tradeId = :tradeId
        and valueDate = :valueDate
        and optionlegId = :optionLegId
        """;

    public const string DeleteTradeTypeLimit = """
        delete from trade_type_limit
where tradeid = :tradeId
""";
    public const string DeleteTradeLimit = """
        delete from trade_limit
where tradeid = :tradeId
""";
    public const string DeleteTradeLimitByTradeType = """
        delete from trade_limit
where tradeid = :tradeId
and tradetype = :tradeType
""";

    public const string DeleteTradeFill = """
        delete from trade_fill
        where orderId = :orderId
        and tradeId = :tradeId;
""";

    public const string DeleteTradeFillById = """
        delete from trade_fill
        where orderId = :orderId
        and tradeId = :tradeId
        and fillDate = :fillDate;
""";

    public const string DeleteTradeFillData = """
        delete from trade_fill_data
        where orderId = :orderId
        and tradeId = :tradeId;
        """;

    public const string DeleteTradePlacementSignal = """
        delete from trade_placement_signal 
        where ContractId = :contractId
        and ValueDate = :valueDate;
        """;

    public const string DeleteTradePlacementSignalBySequenceId = """
        delete from trade_placement_signal 
        where ContractId = :contractId
        and ValueDate = :valueDate
        and SequenceId = :sequenceId;
        """;

    public const string DeleteOptionTradeSpreadData = """
        DELETE FROM option_trade_spread_data
WHERE OrderId = :orderId AND TradeId = :tradeId AND ValueDate = :valueDate AND TradeType = :tradeType;
""";
    public const string InsertTradePosition = """
        INSERT INTO trade_position (
            OrderId,
            TradeId,
            TradeType,
            ValueDate,
            DaysToExpiry,
            TradeStatus,
            Commission,
            DeltaHedge,
            NetSpread,
            TradeValue,
            TradePnl,
            AssetPrice,
            OTMProbability,
            ForwardPrice,
            ForwardLossRatio,
            LossProbability,
            RiskFreeRate,
            CreatedOn,
            CreatedBy,
            UpdatedOn,
            UpdatedBy
        ) VALUES (
            :orderId,
            :tradeId,
            :tradeType, 
            :valueDate,
            :daysToExpiry, 
            :tradeStatus,
            :commission,
            :deltaHedge,
            :netSpread,
            :tradeValue,
            :tradePnl,
            :assetPrice,
            :otmProbability,
            :forwardPrice,
            :forwardLossRatio,
            :lossProbability,
            :riskFreeRate,
            :createdOn,
            :createdBy,
            :updatedOn,
            :updatedBy
        );
""";
    public const string InsertTradeLimit = """
        INSERT INTO trade_limit (
    TradeId,
    TradeType,
    RiskMargin,
    MaxProfit,
    MaxLoss,
    MaxReturn,
    MaxLossLimit,
    MinProfitLimit,
    MaxProfitLimit,
    MinProfitTarget,
    DailyProfitTarget,
    CreatedOn,
    CreatedBy,
    UpdatedOn,
    UpdatedBy
) VALUES (
    :tradeid,
    :tradeType,
    :riskMargin,
    :maxProfit,
    :maxLoss,
    :maxReturn,
    :maxLossLimit,
    :minProfitLimit,
    :maxProfitLimit,
    :minProfitTarget,
    :dailyProfitTarget,
    :createdOn,
    :createdBy,
    :updatedOn,
    :updatedBy
);
""";

    public const string InsertTradeTypeLimit = """
        INSERT INTO trade_type_limit (
            TradeId,
            TradeType,
            MaxLossLimit,
            MinProfitLimit,
            MaxProfitLimit
        ) VALUES (
            :tradeId,
            :tradeType,
            :maxLossLimit,
            :minProfitLimit,
            :maxProfitLimit
        );
    """;

    public const string InsertTradeFill = """
        INSERT INTO trade_fill (
            OrderId,
            TradeId,
            FillDate,
            FillQuantity,
            CreatedOn,
            CreatedBy
        ) VALUES (
            :orderId,
            :tradeId,
            :fillDate,
            :fillQuantity,
            :createdOn,
            :createdBy
        );
""";
    public const string InsertTradeFillData = """
        INSERT INTO trade_fill_data (
        OrderId,
        TradeId,
        ContractId,
        FillDate,
        BidPrice,
        AskPrice,
        Commission,
        OptionLegAction,
        CreatedOn,
        CreatedBy
    ) VALUES (
        :orderId,
        :tradeId,
        :contractId,
        :fillDate,
        :bidPrice,
        :askPrice,
        :commission,
        :optionLegAction,
        :createdOn,
        :createdBy
    ) ;
""";
    
    public const string InsertOptionTradeSpreadData = """
        INSERT INTO option_trade_spread_data (
    OrderId,
    TradeId,
    TradeType,
    ValueDate,
    SequenceId,
    LossLimit,
    WinLimit,
    ForwardSpread,
    NetSpread,
    CreatedOn,
    CreatedBy
) VALUES (
    :orderId,
    :tradeId,
    :tradeType,
    :valueDate,
    :sequenceId,
    :lossLimit,
    :winLimit,
    :forwardSpread,
    :netSpread,
    :createdOn,
    :createdBy
);
""";

    public const string InsertOptionTradeSpreadBarData = """
        INSERT INTO option_trade_spread_bar_data (
            OrderId,
            TradeId,
            TradeType,
            ValueDate,
            BarDate,
            LossLimit,
            WinLimit,
            ForwardSpread,
            NetSpread
        ) VALUES (
            :orderId,
            :tradeId,
            :tradeType,
            :valueDate,
            :barDate,
            :lossLimit,
            :winLimit,
            :forwardSpread,
            :netSpread
        );
        """;
    public const string InsertTradeLiveFeed = """
        INSERT INTO trade_live_feed (
    OrderId,
    TradeId,
    LiveFeed
) VALUES (
    :orderId,
    :tradeId,
    :liveFeed
) IF NOT EXISTS;
""";
    public const string InsertTradePositionState = """
        INSERT INTO trade_position_state (
    OrderId,
    TradeId,
    TradePositionState,
    OpenedOn,
    OpenedBy
) VALUES (
    :orderId,
    :tradeId,
    :tradePositionState,
    :openedOn,
    :openedBy
) IF NOT EXISTS;
""";
    public const string DeleteTradeOrder = """
        delete from trade_order
        where fundId = :fundId 
        and orderId = :orderId 
        and tradeId = :tradeId
""";
    public const string InsertTradeOrder = """
        INSERT INTO trade_order (
    FundId,
    OrderId,
    TradeId,
    ValueDate,
    TradeType,
    TradeSubType,
    TradeDate,
    MaturityDate,
    TradeOrderState,
    UnderlyingContractId,
    UnderlyingAssetType,
    OrderDescription,
    OrderAction,
    OrderActionType,
    OrderQuantity,
    OrderType,
    OrderPrice,
    OrderAmount,
    Commission,
    TotalAmount,
    TradePnl,
    TradeFillType,
    CreatedOn,
    CreatedBy,
    UpdatedOn,
    UpdatedBy
) VALUES (
    :fundId,
    :orderId,
    :tradeId,
    :valueDate,
    :tradeType,
    :tradeSubType,
    :tradeDate,
    :maturityDate,
    :tradeOrderState,
    :underlyingContractId,
    :underlyingAssetType,
    :orderDescription,
    :orderAction,
    :orderActionType,
    :orderQuantity,
    :orderType,
    :orderPrice,
    :orderAmount,
    :commission,
    :totalAmount,
    :tradePnl,
    :tradeFillType,
    :createdOn,
    :createdBy,
    :updatedOn,
    :updatedBy
) IF NOT EXISTS;
""";
    public const string UpdateTradePlanForwardLossLimit = """
        UPDATE trade_plan_forward_loss_limit
SET LimitType = :limitType
WHERE OrderId = :orderId 
  AND TradeId = :tradeId
  AND TradeType = :tradeType
  AND ValueDate = :valueDate;
""";
    public const string InsertTradePlacementSignal = """
        INSERT INTO trade_placement_signal (
    SequenceId,
    ContractId,
    ValueDate,
    TradePlacementSignal,
    TradePrice,
    CreatedOn,
    CreatedBy
) VALUES (
    :sequenceId,
    :contractId,
    :valueDate,
    :tradePlacementSignal,
    :tradePrice,
    :createdOn,
    :createdBy
);
""";
    public const string UpdateTradePosition = """
        UPDATE trade_position
SET 
    Commission = :commission,
    DeltaHedge = :deltaHedge,
    NetSpread = :netSpread,
    TradeValue = :tradeValue,
    TradePnl = :tradePnl,
    AssetPrice = :assetPrice,
    OTMProbability = :otmProbability,
    ForwardPrice = :forwardPrice,
    LossProbability = :lossProbability,
    RiskFreeRate = :riskFreeRate,
    UpdatedOn = :updatedOn,
    UpdatedBy = :updatedBy
WHERE 
    OrderId = :orderId 
    AND TradeId = :tradeId 
    AND ValueDate = :valueDate
    AND TradeStatus = :tradeStatus
    AND DaysToExpiry = :daysToExpiry
    AND TradeType = :tradeType;
""";
    public const string UpdateOptionTradeState = """
        UPDATE option_trade
SET 
    TradeState = :tradeState
WHERE 
    OrderId = :orderId
    AND TradeId = :tradeId;
""";
    public const string UpdateTradeLiveFeed = """
        UPDATE trade_live_feed
SET LiveFeed = :liveFeed
WHERE OrderId = :orderId
AND TradeId = :tradeId
""";
    public const string UpdateTradePositionStatus = """
        UPDATE trade_position
SET TradeStatus = :newTradeStatus,
    UpdatedOn = :updatedOn,
    UpdatedBy = :updatedBy
WHERE TradeId = :tradeId
AND TradeType = :tradeType
AND ValueDate = :valueDate
AND DaysToExpiry = :daysToExpiry
AND TradeStatus = :oldTradeStatus
""";
    public const string UpdateOptionLegDataStatus = """
        UPDATE option_leg_data
SET TradeStatus = :newTradeStatus,
    UpdatedOn = :updatedOn,
    UpdatedBy = :updatedBy
WHERE TradeId = :tradeId
AND ValueDate = :valueDate
AND OptionLegId = :optionLegId
""";
    public const string UpdateTradeOrderState = """
        UPDATE trade_order
SET TradeOrderState = :tradeOrderState,
    UpdatedOn = :updatedOn,
    UpdatedBy = :updatedBy
WHERE TradeId = :tradeId
  AND ValueDate = :valueDate;
""";
    public const string UpdateTradeOrderOrderPrice = """
        UPDATE trade_order
SET OrderPrice = :orderPrice,
    UpdatedOn = :updatedOn,
    UpdatedBy = :updatedBy
WHERE TradeId = :tradeId
  AND ValueDate = :valueDate;
""";
    public const string DeleteTradePositionState = """
        DELETE FROM trade_position_state
WHERE OrderId = :orderId
  AND TradeId = :tradeId;
""";
    public const string DeleteOptionTradeSpreadBarData = """
        DELETE FROM option_trade_spread_bar_data
WHERE OrderId = :orderId AND TradeId = :tradeId AND ValueDate = :valueDate AND TradeType = :tradeType;
""";
    public const string DeleteTradeLiveFeed = """
        DELETE FROM trade_live_feed
WHERE OrderId = :orderId AND TradeId = :tradeId;
""";
    public const string DeleteTradeLiveFeeds = """
        DELETE FROM trade_live_feed
WHERE OrderId = :orderId;
""";
    public const string DeleteTradePlanForwardLossLimit = """
        DELETE FROM trade_plan_forward_loss_limit
WHERE OrderId = :orderId AND TradeId = :tradeId AND ValueDate = :valueDate AND TradeType = :tradeType;
""";
    public const string UpdateTradeLimitDailyProfitTarget = """
        UPDATE trade_limit
SET DailyProfitTarget = :dailyProfitTarget,
    UpdatedOn = :updatedOn,
    UpdatedBy = :updatedBy
WHERE TradeId = :tradeId
AND TradeType = :tradeType;
""";
    public const string InsertTradePlanForwardLossLimit = """
        INSERT INTO trade_plan_forward_loss_limit (OrderId, TradeId, TradeType, ValueDate, LimitType)
VALUES (:orderId, :tradeId, :tradeType, :valueDate, :limitType);
""";
    public const string InsertTradePlan = """
        INSERT INTO trade_plan (
 SequenceId,
    OrderId,
    TradeId,
    TradeType,
    TradeDate,
    ValueDate,
    MaturityDate,
    ActionDate,
    ActionType,
    ActionSubType,
    ActionState,
    ActionReason,
    TradePnl,
    ForwardLossRatio,
    LossProbability,
    MScore,
    MaxProfit,
    MaxLoss,
    MinProfitTarget,
    DailyProfitTarget,
    AssetPrice,
    AssetStdDev,
    AssetMean,
    AssetPriceChange,
    MarketTrend,
    MarketVolatility,
    MarketDirection,
    VixVolatility,
    TradeRisk,
    FiftyDayMA,
    FiveDayXMA,
    PutOTMProbability,
    CallOTMProbability,
    ShortPutGamma,
    ShortCallGamma,
    GammaRisk,
    NetPrice,
    ForwardPrice,
    ForwardDelta,
    StopLossLimit,
    TrendType,
    TrendStrength,
    RSI,
    RSISlope,
    TDI,
    TDIStrength,
    CreatedOn,
    CreatedBy
) VALUES (
    :sequenceId,
    :orderId,
    :tradeId,
    :tradeType,
    :tradeDate,
    :valueDate,
    :maturityDate,
    :actionDate,
    :actionType,
    :actionSubType,
    :actionState,
    :actionReason,
    :tradePnl,
    :forwardLossRatio,
    :lossProbability,
    :mscore,
    :maxProfit,
    :maxLoss,
    :minProfitTarget,
    :dailyProfitTarget,
    :assetPrice,
    :assetStdDev,
    :assetMean,
    :assetPriceChange,
    :marketTrend,
    :marketVolatility,
    :marketDirection,
    :vixVolatility,
    :tradeRisk,
    :fiftyDayMA,
    :fiveDayXMA,
    :putOTMProbability,
    :callOTMProbability,
    :shortPutGamma,
    :shortCallGamma,
    :gammaRisk,
    :netPrice,
    :forwardPrice,
    :forwardDelta,
    :stopLossLimit,
    :trendType,
    :trendStrength,
    :rsi,
    :rsiSlope,
    :tdi,
    :tdiStrength,
    :createdOn,
    :createdBy
);
""";

    public const string CreateOptionTradeSpreadDataTable = """
        CREATE TABLE IF NOT EXISTS option_trade_spread_data (
    orderId int,
    tradeId int,
    valueDate date,
    tradeType text,
    sequenceId bigint,
    lossLimit decimal,
    winLimit decimal,
    forwardSpread decimal,
    netSpread decimal,
    createdOn timestamp,
    createdBy text,
    PRIMARY KEY (orderId, tradeId, valueDate, tradeType, sequenceId)
) WITH CLUSTERING ORDER BY (
    tradeId DESC,
    valueDate DESC,
    tradeType ASC,
    sequenceId DESC
);
""";
    public const string InsertTradePlanForwardLossRatio = """
        insert into trade_plan_forward_loss_ratio (
 partitionId,
 valueDate,
 forwardLossRatio,
 sequenceId
) values (
 :partitionId,
 :valueDate,
 :forwardLossRatio,
 :sequenceId
) if not exists;
""";
}
