

namespace TomasAI.IFM.Application.Storage.TradeDb;
internal static class TradeDbSql
{
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
    public const string GetOptionLegsByOrderAndTrade = """
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
        WHERE orderId = :orderId
          AND tradeId = :tradeId;
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
    public const string GetTradeTypeLimit = """
        SELECT TradeId AS "TradeId",
 TradeType AS "TradeType",
    MaxLossLimit AS "MaxLossLimit",
    MinProfitLimit AS "MinProfitLimit",
    MaxProfitLimit as "MaxProfitLimit"
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
WHERE OrderId = :orderId;
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
    public const string GetTradePlanForwardLossRatios = """
        select forwardLossRatio as "ForwardLossRatio"
from trade_plan_forward_loss_ratio
where partitionId = 1 
and  valueDate >= :startDate
and valueDate <= :endDate
""";
    public const string GetLastTradePlanForwardLossRatio = """
        select forwardLossRatio as "ForwardLossRatio", sequenceId as "SequenceId"
from trade_plan_forward_loss_ratio
where partitionId = 1 
and valueDate = :valueDate
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
    public const string GetIntrinsicTimeStrategyWorkflow = """
SELECT workflowId, workflowEntityId, workflowDefinitionId, workflowDefinitionVersion,
       contractId, timeFrameStartValueDate, timePeriod, triggerEventId, correlationId,
       status, outcome, currentStage, workflowRevision, lastEventId, stateSchemaVersion,
       statePayload, stopReasonCode, startedAtUtc, terminalAtUtc, updatedAtUtc
FROM intrinsic_time_strategy_workflow
WHERE workflowId = :workflowId;
""";
    public const string GetRegimeDiscovery = """
SELECT workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
       sourceEventSequence, status, parameterPayloadSha256, signalSnapshotId, resultPayload,
       resultPayloadSha256, failureCode, failureMessage, reasonsPayload, schemaVersion,
       terminalAtUtc, updatedAtUtc
FROM regime_discovery
WHERE workflowId = :workflowId;
""";
    public const string GetMarketCondition = """
SELECT workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
       fundId, instrumentRoot, targetHorizon, parameterSetId, parameterSetVersion,
       parameterPayloadSha256, snapshotId, snapshotSha256, tradeability, conditionType,
       direction, phase, strength, confidence, primaryReasonCode, resultPayload,
       resultPayloadSha256, evaluatedAtUtc, validUntilUtc, marketDataAsOfUtc, completedAtUtc, updatedAtUtc,
       volatilityBehavior, liquidityQuality, dataQuality, upstreamAlignment, evidencePayload,
       conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload, summaryText
FROM market_condition WHERE workflowId = :workflowId;
""";
    public const string GetMarketConditionHistory = """
SELECT workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
       fundId, instrumentRoot, targetHorizon, parameterSetId, parameterSetVersion,
       parameterPayloadSha256, snapshotId, snapshotSha256, tradeability, conditionType,
       direction, phase, strength, confidence, primaryReasonCode, resultPayload,
       resultPayloadSha256, evaluatedAtUtc, validUntilUtc, marketDataAsOfUtc, completedAtUtc, updatedAtUtc,
       volatilityBehavior, liquidityQuality, dataQuality, upstreamAlignment, evidencePayload,
       conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload, summaryText
FROM market_condition_by_fund
WHERE fundId = :fundId AND instrumentRoot = :instrumentRoot AND targetHorizon = :targetHorizon
  AND evaluatedAtUtc < :beforeUtc
LIMIT :pageSize;
""";
    public const string UpsertMarketCondition = """
INSERT INTO market_condition (workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
    fundId, instrumentRoot, targetHorizon, parameterSetId, parameterSetVersion, parameterPayloadSha256,
    snapshotId, snapshotSha256, tradeability, conditionType, direction, phase, strength, confidence,
    primaryReasonCode, resultPayload, resultPayloadSha256, evaluatedAtUtc, validUntilUtc, marketDataAsOfUtc,
    completedAtUtc, updatedAtUtc, volatilityBehavior, liquidityQuality, dataQuality, upstreamAlignment,
    evidencePayload, conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload, summaryText)
VALUES (:workflowId, :workflowEntityId, :inputWorkflowRevision, :commandId, :sourceEventId,
    :fundId, :instrumentRoot, :targetHorizon, :parameterSetId, :parameterSetVersion, :parameterPayloadSha256,
    :snapshotId, :snapshotSha256, :tradeability, :conditionType, :direction, :phase, :strength, :confidence,
    :primaryReasonCode, :resultPayload, :resultPayloadSha256, :evaluatedAtUtc, :validUntilUtc, :marketDataAsOfUtc,
    :completedAtUtc, :updatedAtUtc, :volatilityBehavior, :liquidityQuality, :dataQuality, :upstreamAlignment,
    :evidencePayload, :conflictingEvidencePayload, :blockingReasonsPayload, :reasonsPayload, :summaryText);
""";
    public const string UpsertMarketConditionByFund = """
INSERT INTO market_condition_by_fund (fundId, instrumentRoot, targetHorizon, evaluatedAtUtc, workflowId,
    workflowEntityId, inputWorkflowRevision, commandId, sourceEventId, parameterSetId, parameterSetVersion,
    parameterPayloadSha256, snapshotId, snapshotSha256, tradeability, conditionType, direction, phase,
    strength, confidence, primaryReasonCode, resultPayload, resultPayloadSha256, validUntilUtc,
    marketDataAsOfUtc, completedAtUtc, updatedAtUtc, volatilityBehavior, liquidityQuality, dataQuality,
    upstreamAlignment, evidencePayload, conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload,
    summaryText)
VALUES (:fundId, :instrumentRoot, :targetHorizon, :evaluatedAtUtc, :workflowId,
    :workflowEntityId, :inputWorkflowRevision, :commandId, :sourceEventId, :parameterSetId,
    :parameterSetVersion, :parameterPayloadSha256, :snapshotId, :snapshotSha256, :tradeability,
    :conditionType, :direction, :phase, :strength, :confidence, :primaryReasonCode, :resultPayload,
    :resultPayloadSha256, :validUntilUtc, :marketDataAsOfUtc, :completedAtUtc, :updatedAtUtc,
    :volatilityBehavior, :liquidityQuality, :dataQuality, :upstreamAlignment, :evidencePayload,
    :conflictingEvidencePayload, :blockingReasonsPayload, :reasonsPayload, :summaryText);
""";
    public const string UpsertRegimeDiscovery = """
INSERT INTO regime_discovery (
    workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
    sourceEventSequence, status, parameterPayloadSha256, signalSnapshotId, resultPayload,
    resultPayloadSha256, failureCode, failureMessage, reasonsPayload, schemaVersion,
    terminalAtUtc, updatedAtUtc)
VALUES (:workflowId, :workflowEntityId, :inputWorkflowRevision, :commandId, :sourceEventId,
    :sourceEventSequence, :status, :parameterPayloadSha256, :signalSnapshotId, :resultPayload,
    :resultPayloadSha256, :failureCode, :failureMessage, :reasonsPayload, :schemaVersion,
    :terminalAtUtc, :updatedAtUtc);
""";
    public const string GetActiveIntrinsicTimeStrategyWorkflow = """
SELECT workflowEntityId, workflowId, contractId, timeFrameStartValueDate, timePeriod,
       currentStage, workflowRevision, lastEventId, stateSchemaVersion, statePayload,
       startedAtUtc, updatedAtUtc
FROM intrinsic_time_strategy_workflow_active
WHERE workflowEntityId = :workflowEntityId;
""";
    public const string GetIntrinsicTimeStrategyWorkflowStartAttempts = """
SELECT workflowEntityId, requestedAtUtc, requestedWorkflowId, decision, activeWorkflowId,
       startCommandId, triggerEventId, activeStage, reasonCode, sourceEventId
FROM intrinsic_time_strategy_workflow_start_attempt
WHERE workflowEntityId = :workflowEntityId AND requestedAtUtc < :beforeUtc
LIMIT :pageSize;
""";
    public const string GetIntrinsicTimeStrategyWorkflowTimeline = """
SELECT workflowId, eventId, workflowEntityId, workflowRevision, stage, eventName,
       eventSchemaVersion, eventPayload, occurredAtUtc
FROM intrinsic_time_strategy_workflow_timeline
WHERE workflowId = :workflowId AND eventId > :afterEventId
LIMIT :pageSize;
""";
    public const string GetIntrinsicTimeStrategyWorkflowsByEntity = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM intrinsic_time_strategy_workflow_by_entity
WHERE workflowEntityId = :workflowEntityId AND startedAtUtc < :beforeUtc
LIMIT :pageSize;
""";
    public const string GetIntrinsicTimeStrategyWorkflowsByStatusDay = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM intrinsic_time_strategy_workflow_by_status_day
WHERE status = :status AND startedDate = :startedDate
LIMIT :pageSize;
""";
    public const string UpsertIntrinsicTimeStrategyWorkflow = """
INSERT INTO intrinsic_time_strategy_workflow (
    workflowId, workflowEntityId, workflowDefinitionId, workflowDefinitionVersion,
    contractId, timeFrameStartValueDate, timePeriod, triggerEventId, correlationId,
    status, outcome, currentStage, workflowRevision, lastEventId, stateSchemaVersion,
    statePayload, stopReasonCode, startedAtUtc, terminalAtUtc, updatedAtUtc)
VALUES (:workflowId, :workflowEntityId, :workflowDefinitionId, :workflowDefinitionVersion,
    :contractId, :timeFrameStartValueDate, :timePeriod, :triggerEventId, :correlationId,
    :status, :outcome, :currentStage, :workflowRevision, :lastEventId, :stateSchemaVersion,
    :statePayload, :stopReasonCode, :startedAtUtc, :terminalAtUtc, :updatedAtUtc);
""";
    public const string UpsertActiveIntrinsicTimeStrategyWorkflow = """
INSERT INTO intrinsic_time_strategy_workflow_active (
    workflowEntityId, workflowId, contractId, timeFrameStartValueDate, timePeriod,
    currentStage, workflowRevision, lastEventId, stateSchemaVersion, statePayload,
    startedAtUtc, updatedAtUtc)
VALUES (:workflowEntityId, :workflowId, :contractId, :timeFrameStartValueDate, :timePeriod,
    :currentStage, :workflowRevision, :lastEventId, :stateSchemaVersion, :statePayload,
    :startedAtUtc, :updatedAtUtc);
""";
    public const string DeleteActiveIntrinsicTimeStrategyWorkflow = """
DELETE FROM intrinsic_time_strategy_workflow_active
WHERE workflowEntityId = :workflowEntityId;
""";
    public const string InsertIntrinsicTimeStrategyWorkflowStartAttempt = """
INSERT INTO intrinsic_time_strategy_workflow_start_attempt (
    workflowEntityId, requestedAtUtc, requestedWorkflowId, decision, activeWorkflowId,
    startCommandId, triggerEventId, activeStage, reasonCode, sourceEventId)
VALUES (:workflowEntityId, :requestedAtUtc, :requestedWorkflowId, :decision, :activeWorkflowId,
    :startCommandId, :triggerEventId, :activeStage, :reasonCode, :sourceEventId);
""";
    public const string InsertIntrinsicTimeStrategyWorkflowTimeline = """
INSERT INTO intrinsic_time_strategy_workflow_timeline (
    workflowId, eventId, workflowEntityId, workflowRevision, stage, eventName,
    eventSchemaVersion, eventPayload, occurredAtUtc)
VALUES (:workflowId, :eventId, :workflowEntityId, :workflowRevision, :stage, :eventName,
    :eventSchemaVersion, :eventPayload, :occurredAtUtc);
""";
    public const string UpsertIntrinsicTimeStrategyWorkflowByEntity = """
INSERT INTO intrinsic_time_strategy_workflow_by_entity (
    workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
    workflowRevision, terminalAtUtc, stopReasonCode)
VALUES (:workflowEntityId, :startedAtUtc, :workflowId, :status, :outcome, :currentStage,
    :workflowRevision, :terminalAtUtc, :stopReasonCode);
""";
    public const string UpsertIntrinsicTimeStrategyWorkflowByStatusDay = """
INSERT INTO intrinsic_time_strategy_workflow_by_status_day (
    status, startedDate, startedAtUtc, workflowId, workflowEntityId, outcome,
    currentStage, workflowRevision, terminalAtUtc, stopReasonCode)
VALUES (:status, :startedDate, :startedAtUtc, :workflowId, :workflowEntityId, :outcome,
    :currentStage, :workflowRevision, :terminalAtUtc, :stopReasonCode);
""";
    internal const string NativeGetIntrinsicTimeStrategyWorkflow = """
SELECT workflowId, workflowEntityId, workflowDefinitionId, workflowDefinitionVersion,
       contractId, timeFrameStartValueDate, timePeriod, triggerEventId, correlationId,
       status, outcome, currentStage, workflowRevision, lastEventId, stateSchemaVersion,
       statePayload, stopReasonCode, startedAtUtc, terminalAtUtc, updatedAtUtc
FROM trade.intrinsic_time_strategy_workflow
WHERE workflowId = $1;
""";
    internal const string NativeGetActiveIntrinsicTimeStrategyWorkflow = """
SELECT workflowEntityId, workflowId, contractId, timeFrameStartValueDate, timePeriod,
       currentStage, workflowRevision, lastEventId, stateSchemaVersion, statePayload,
       startedAtUtc, updatedAtUtc
FROM trade.intrinsic_time_strategy_workflow_active
WHERE workflowEntityId = $1;
""";
    internal const string NativeGetIntrinsicTimeStrategyWorkflowStartAttempts = """
SELECT workflowEntityId, requestedAtUtc, requestedWorkflowId, decision, activeWorkflowId,
       startCommandId, triggerEventId, activeStage, reasonCode, sourceEventId
FROM trade.intrinsic_time_strategy_workflow_start_attempt
WHERE workflowEntityId = $1 AND requestedAtUtc < $2
ORDER BY requestedAtUtc DESC, requestedWorkflowId DESC
LIMIT $3;
""";
    internal const string NativeGetIntrinsicTimeStrategyWorkflowTimeline = """
SELECT workflowId, eventId, workflowEntityId, workflowRevision, stage, eventName,
       eventSchemaVersion, eventPayload, occurredAtUtc
FROM trade.intrinsic_time_strategy_workflow_timeline
WHERE workflowId = $1 AND eventId > $2
ORDER BY eventId ASC
LIMIT $3;
""";
    internal const string NativeGetIntrinsicTimeStrategyWorkflowsByEntity = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM trade.intrinsic_time_strategy_workflow_by_entity
WHERE workflowEntityId = $1 AND startedAtUtc < $2
ORDER BY startedAtUtc DESC, workflowId DESC
LIMIT $3;
""";
    internal const string NativeGetIntrinsicTimeStrategyWorkflowsByStatusDay = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM trade.intrinsic_time_strategy_workflow_by_status_day
WHERE status = $1 AND startedDate = $2
ORDER BY startedAtUtc DESC, workflowId DESC
LIMIT $3;
""";
    internal const string ContextStatement1 = "SELECT payload,payload_sha256 FROM trade.market_condition_assessment WHERE workflow_id=$1;";
    internal const string ContextStatement2 = "SELECT payload,content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? ORDER BY revision DESC LIMIT 1;";

    internal const string ContextStatement4 = "SELECT payload FROM trade.trade_order WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 ORDER BY revision DESC LIMIT 1;";
    internal const string ContextStatement5 = "SELECT payload FROM established_trade_history WHERE portfolioId=? AND fundId=? AND strategyKind=? AND establishedAtUtc>=? AND establishedAtUtc<=?;";
    internal const string HelperStatement6 = "SELECT payload,payload_sha256 FROM trade.market_condition_assessment_by_profile WHERE market_profile_id=$1 AND instrument_root=$2 AND target_horizon=$3 AND evaluated_at_utc<$4 ORDER BY evaluated_at_utc DESC,workflow_id LIMIT $5;";
    internal const string HelperStatement7 = "INSERT INTO risk_management_invocation (workflow_id,invocation_id,revision,payload,content_hash) VALUES (?,?,?,?,?) IF NOT EXISTS;";
    internal const string HelperStatement8 = "SELECT content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? AND revision=?;";
    internal const string HelperStatement9 = "INSERT INTO risk_management_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload) VALUES (?,?,?,?,?,?,?) IF NOT EXISTS;";
    internal const string HelperStatement10 = "UPDATE risk_management_history SET revision=?,payload=? WHERE portfolio_id=? AND fund_id=? AND value_date=? AND evaluated_at_utc=? AND invocation_id=? IF revision<?;";
    internal const string HelperStatement11 = """
            INSERT INTO trade.trade_order(portfolioId,fundId,orderId,revision,status,valueDate,updatedAtUtc,definitionHash,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT(portfolioId,fundId,orderId,revision) DO UPDATE SET payload=EXCLUDED.payload,status=EXCLUDED.status,
              valueDate=EXCLUDED.valueDate,definitionHash=EXCLUDED.definitionHash,updatedAtUtc=EXCLUDED.updatedAtUtc
              WHERE trade.trade_order.payload=EXCLUDED.payload AND trade.trade_order.updatedAtUtc=EXCLUDED.updatedAtUtc RETURNING revision;
            """;
    internal const string HelperStatement12 = """
            INSERT INTO trade.order_execution(portfolioId,fundId,orderId,executionAttemptId,status,startedAtUtc,completedAtUtc,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT(portfolioId,fundId,orderId,executionAttemptId) DO UPDATE SET status=EXCLUDED.status,
              startedAtUtc=EXCLUDED.startedAtUtc,completedAtUtc=EXCLUDED.completedAtUtc,payload=EXCLUDED.payload;
            """;
    internal const string HelperStatement13 = """
                INSERT INTO trade.order_execution_fill(executionAttemptId,executionFillId,componentId,tradeLegId,contractId,filledAtUtc,payload)
                VALUES($1,$2,$3,$4,$5,$6,$7)
                ON CONFLICT(executionAttemptId,filledAtUtc,executionFillId) DO UPDATE SET componentId=EXCLUDED.componentId,
                  tradeLegId=EXCLUDED.tradeLegId,contractId=EXCLUDED.contractId,payload=EXCLUDED.payload;
                """;
    internal const string HelperStatement14 = """
            INSERT INTO trade.established_trade_history(portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8)
            ON CONFLICT(portfolioId,fundId,strategyKind,establishedAtUtc,orderId,tradeId,evidenceRevision)
            DO UPDATE SET payload=EXCLUDED.payload
              WHERE trade.established_trade_history.payload=EXCLUDED.payload
            RETURNING evidenceRevision;
            """;
    internal const string HelperStatement15 = """
            INSERT INTO trade.established_trade(portfolioId,fundId,orderId,tradeId,assetFamily,strategyKind,establishedAtUtc,evidenceRevision,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT(portfolioId,fundId,orderId,tradeId) DO UPDATE SET assetFamily=EXCLUDED.assetFamily,strategyKind=EXCLUDED.strategyKind,
              establishedAtUtc=EXCLUDED.establishedAtUtc,evidenceRevision=EXCLUDED.evidenceRevision,payload=EXCLUDED.payload
              WHERE trade.established_trade.evidenceRevision<EXCLUDED.evidenceRevision OR
                (trade.established_trade.evidenceRevision=EXCLUDED.evidenceRevision AND trade.established_trade.payload=EXCLUDED.payload)
            RETURNING evidenceRevision;
            """;
    internal const string HelperStatement16 = "SELECT evidenceRevision FROM trade.established_trade WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4;";
    internal const string HelperStatement17 = """
            INSERT INTO trade.strategy_position_current(portfolioId,fundId,orderId,tradeId,positionId,strategyKind,positionSequence,routeGeneration,isOpen,asOfUtc,payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
            ON CONFLICT(portfolioId,fundId,orderId,tradeId,positionId) DO UPDATE SET strategyKind=EXCLUDED.strategyKind,
              positionSequence=EXCLUDED.positionSequence,routeGeneration=EXCLUDED.routeGeneration,isOpen=EXCLUDED.isOpen,
              asOfUtc=EXCLUDED.asOfUtc,payload=EXCLUDED.payload
              WHERE trade.strategy_position_current.positionSequence<EXCLUDED.positionSequence OR
                (trade.strategy_position_current.positionSequence=EXCLUDED.positionSequence AND trade.strategy_position_current.payload=EXCLUDED.payload)
              RETURNING positionSequence;
            """;
    internal const string HelperStatement18 = "SELECT positionSequence FROM trade.strategy_position_current WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4 AND positionId=$5;";
    internal const string HelperStatement19 = """
            INSERT INTO trade.strategy_position_history(positionId,asOfUtc,positionSequence,phase,payload,projection_sequence)
            VALUES($1,$2,$3,$4,$5,$6)
            ON CONFLICT(positionId,asOfUtc,positionSequence) DO UPDATE SET phase=EXCLUDED.phase,payload=EXCLUDED.payload,
              projection_sequence=EXCLUDED.projection_sequence
              WHERE trade.strategy_position_history.projection_sequence<=EXCLUDED.projection_sequence;
            """;
    internal const string HelperStatement20 = "DELETE FROM trade.open_position_route WHERE positionId=$1;";
    internal const string HelperStatement21 = "DELETE FROM trade.open_position_route_recovery WHERE positionId=$1;";
    internal const string HelperStatement22 = """
                INSERT INTO trade.open_position_route(contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9);
                """;
    internal const string HelperStatement23 = """
                INSERT INTO trade.open_position_route_recovery(shard,positionId,tradeLegId,contractId,portfolioId,fundId,orderId,tradeId,tradeType,generation)
                VALUES(0,$1,$2,$3,$4,$5,$6,$7,$8,$9);
                """;
    internal const string InsertInitialMonitoringLimit = """
        INSERT INTO trade.trade_limit (tradeId,tradeType,riskMargin,maxProfit,maxLoss,maxReturn,maxLossLimit,minProfitLimit,maxProfitLimit,minProfitTarget,dailyProfitTarget,createdOn,createdBy,updatedOn,updatedBy)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15) ON CONFLICT(tradeId,tradeType) DO NOTHING;
        """;
    internal const string InsertInitialSpreadLimit = """
        INSERT INTO trade.trade_type_limit(tradeId,tradeType,maxLossLimit,minProfitLimit,maxProfitLimit)
        VALUES($1,$2,$3,$4,$5) ON CONFLICT(tradeId,tradeType) DO NOTHING;
        """;
    internal const string HelperStatement24 = """
            INSERT INTO trade.trade_limit (tradeId,tradeType,riskMargin,maxProfit,maxLoss,maxReturn,maxLossLimit,minProfitLimit,maxProfitLimit,minProfitTarget,dailyProfitTarget,createdOn,createdBy,updatedOn,updatedBy)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)
            ON CONFLICT(tradeId,tradeType) DO UPDATE SET riskMargin=EXCLUDED.riskMargin,maxProfit=EXCLUDED.maxProfit,maxLoss=EXCLUDED.maxLoss,
                maxReturn=EXCLUDED.maxReturn,maxLossLimit=EXCLUDED.maxLossLimit,minProfitLimit=EXCLUDED.minProfitLimit,maxProfitLimit=EXCLUDED.maxProfitLimit,
                minProfitTarget=EXCLUDED.minProfitTarget,dailyProfitTarget=EXCLUDED.dailyProfitTarget,createdOn=EXCLUDED.createdOn,createdBy=EXCLUDED.createdBy,
                updatedOn=EXCLUDED.updatedOn,updatedBy=EXCLUDED.updatedBy;
            """;
    internal const string HelperStatement25 = """
                INSERT INTO trade.trade_type_limit(tradeId,tradeType,maxLossLimit,minProfitLimit,maxProfitLimit) VALUES($1,$2,$3,$4,$5)
                ON CONFLICT(tradeId,tradeType) DO UPDATE SET maxLossLimit=EXCLUDED.maxLossLimit,minProfitLimit=EXCLUDED.minProfitLimit,maxProfitLimit=EXCLUDED.maxProfitLimit;
                """;
    internal const string HelperStatement26 = """
            SELECT asOfUtc,positionSequence,payload FROM trade.strategy_position_history
            WHERE positionId=$1 AND asOfUtc>=$2 AND asOfUtc<=$3
            """;
    internal const string HelperStatement27 = "SELECT payload FROM trade.strategy_position_current WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4 AND positionId=$5;";
    internal const string HelperStatement28 = "SELECT ";
    internal const string HelperStatement29 = "SELECT ";
    internal const string HelperStatement30 = "SELECT ";
    internal const string HelperStatement31 = "SELECT payload FROM trade.order_execution WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND executionAttemptId=$4;";
    internal const string HelperStatement32 = "SELECT payload FROM trade.established_trade WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4;";
    internal const string HelperStatement33 = """
            SELECT t.tradeId,t.tradeType,t.riskMargin,t.maxProfit,t.maxLoss,t.maxReturn,
              COALESCE(s.maxLossLimit,t.maxLossLimit),COALESCE(s.minProfitLimit,t.minProfitLimit),COALESCE(s.maxProfitLimit,t.maxProfitLimit),
              t.minProfitTarget,t.dailyProfitTarget,t.createdOn,t.createdBy,t.updatedOn,t.updatedBy
            FROM trade.trade_limit t LEFT JOIN trade.trade_type_limit s ON t.tradeId=s.tradeId AND t.tradeType=s.tradeType
            """;
    internal const string HelperStatement34 = "SELECT tradeId,tradeType,maxLossLimit,minProfitLimit,maxProfitLimit FROM trade.trade_type_limit";
    internal const string HelperStatement35 = """
            INSERT INTO trade.market_condition_assessment(workflow_id,payload,payload_sha256) VALUES($1,$2,$3)
            ON CONFLICT(workflow_id) DO UPDATE SET payload=EXCLUDED.payload
            WHERE trade.market_condition_assessment.payload=EXCLUDED.payload RETURNING workflow_id;
            """;
    internal const string HelperStatement36 = """
            INSERT INTO trade.market_condition_assessment_by_profile(market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id,payload,payload_sha256)
            VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id) DO NOTHING;
            """;
    internal const string HelperStatement37 = """
            INSERT INTO trade.order_composition_invocation(workflow_id,invocation_id,result_hash,input_hash,payload) VALUES($1,$2,$3,$4,$5)
            ON CONFLICT(workflow_id,invocation_id) DO UPDATE SET payload=EXCLUDED.payload
            WHERE trade.order_composition_invocation.payload=EXCLUDED.payload RETURNING invocation_id;
            """;
    internal const string HelperStatement38 = """
            INSERT INTO trade.order_composition_history(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) ON CONFLICT(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id) DO NOTHING;
            """;
    internal const string HelperStatement39 = """
            INSERT INTO trade.trade_selection_invocation_event(workflow_id,invocation_id,source_sequence,event_id,portfolio_id,fund_id,target_horizon,lifecycle_status,outcome,occurred_at_utc,reason_code,
            selected_deployment_id,selected_deployment_version,selected_strategy_id,selected_strategy_version,selected_structure_id,selected_structure_version,selected_variant_id,selected_variant_version,
            candidate_set_sha256,binding_sha256,parameter_set_id,parameter_version,parameter_sha256,result_id,result_sha256,result_payload,event_payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28)
            ON CONFLICT(workflow_id,invocation_id,source_sequence) DO UPDATE SET event_payload=EXCLUDED.event_payload
            WHERE trade.trade_selection_invocation_event.event_payload=EXCLUDED.event_payload RETURNING invocation_id;
            """;
    internal const string HelperStatement40 = """
            INSERT INTO trade.trade_selection_history_by_fund_date(portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) ON CONFLICT(portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id) DO NOTHING;
            """;
    internal const string HelperStatement41 = "SELECT payload,result_hash,input_hash FROM trade.order_composition_invocation WHERE workflow_id=$1 AND invocation_id=$2;";
    internal const string HelperStatement42 = "SELECT event_payload,result_sha256 FROM trade.trade_selection_invocation_event WHERE workflow_id=$1 AND invocation_id=$2 AND source_sequence=1;";
    internal const string HelperStatement43 = """
            INSERT INTO trade.regime_discovery_invocation(workflow_id,command_id,source_event_id,source_payload)
            VALUES($1,$2,$3,$4)
            ON CONFLICT(workflow_id,command_id) DO UPDATE SET source_payload=EXCLUDED.source_payload
            WHERE regime_discovery_invocation.source_payload=EXCLUDED.source_payload RETURNING command_id;
            """;
    internal const string HelperStatement44 = """
            INSERT INTO trade.regime_discovery(workflowId,workflowEntityId,inputWorkflowRevision,commandId,sourceEventId,sourceEventSequence,status,
                parameterPayloadSha256,signalSnapshotId,resultPayload,resultPayloadSha256,failureCode,failureMessage,reasonsPayload,schemaVersion,terminalAtUtc,updatedAtUtc)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)
            ON CONFLICT(workflowId) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId,inputWorkflowRevision=EXCLUDED.inputWorkflowRevision,
                commandId=EXCLUDED.commandId,sourceEventId=EXCLUDED.sourceEventId,sourceEventSequence=EXCLUDED.sourceEventSequence,status=EXCLUDED.status,
                parameterPayloadSha256=EXCLUDED.parameterPayloadSha256,signalSnapshotId=EXCLUDED.signalSnapshotId,resultPayload=EXCLUDED.resultPayload,
                resultPayloadSha256=EXCLUDED.resultPayloadSha256,failureCode=EXCLUDED.failureCode,failureMessage=EXCLUDED.failureMessage,
                reasonsPayload=EXCLUDED.reasonsPayload,schemaVersion=EXCLUDED.schemaVersion,terminalAtUtc=EXCLUDED.terminalAtUtc,updatedAtUtc=EXCLUDED.updatedAtUtc
            WHERE regime_discovery.inputWorkflowRevision < EXCLUDED.inputWorkflowRevision
               OR (regime_discovery.inputWorkflowRevision = EXCLUDED.inputWorkflowRevision AND regime_discovery.commandId = EXCLUDED.commandId
                   AND regime_discovery.resultPayload = EXCLUDED.resultPayload);
            """;
    internal const string HelperStatement45 = """
            SELECT workflowId,workflowEntityId,inputWorkflowRevision,commandId,sourceEventId,sourceEventSequence,status,parameterPayloadSha256,
                signalSnapshotId,resultPayload,resultPayloadSha256,failureCode,failureMessage,reasonsPayload,schemaVersion,terminalAtUtc,updatedAtUtc
            FROM trade.regime_discovery WHERE workflowId=$1;
            """;
    internal const string HelperStatement46 = "DELETE FROM trade.intrinsic_time_strategy_workflow_active WHERE workflowEntityId=$1 AND lastEventId<=$2;";
    internal const string HelperStatement47 = "INSERT INTO trade.risk_management_invocation(workflow_id,invocation_id,revision,payload,content_hash) VALUES($1,$2,$3,$4,$5) ON CONFLICT(workflow_id,invocation_id,revision) DO UPDATE SET content_hash=trade.risk_management_invocation.content_hash WHERE trade.risk_management_invocation.content_hash=EXCLUDED.content_hash RETURNING content_hash;";
    internal const string HelperStatement48 = "INSERT INTO trade.";

internal static string PositionHistory(bool continued) => HelperStatement26 + (continued ? " AND (asOfUtc,positionSequence)>($4,$5) ORDER BY asOfUtc,positionSequence LIMIT $6;" : " ORDER BY asOfUtc,positionSequence LIMIT $4;");
internal static string RoutePage(bool continued) => "SELECT " + RouteColumns + " FROM trade.open_position_route_recovery WHERE shard=0" + (continued ? " AND (positionId,tradeLegId)>($1,$2) ORDER BY positionId,tradeLegId LIMIT $3;" : " ORDER BY positionId,tradeLegId LIMIT $1;");
const string RouteColumns = "contractId,portfolioId,fundId,orderId,tradeId,positionId,tradeLegId,tradeType,generation";
internal static string Routes(bool contract) => "SELECT " + RouteColumns + (contract ? " FROM trade.open_position_route WHERE contractId=$1 ORDER BY positionId,tradeLegId;" : " FROM trade.open_position_route_recovery WHERE shard=0 ORDER BY positionId,tradeLegId;");
internal static string Limits(bool trade) => HelperStatement33 + (trade ? " WHERE t.tradeId=$1" : "") + " ORDER BY t.tradeId,t.tradeType;";
internal static string SpreadLimits(bool trade, bool type) => HelperStatement34 + (trade ? " WHERE tradeId=$1" : "") + (type ? " AND tradeType=$2" : "") + " ORDER BY tradeId,tradeType;";
internal static string PipelineHistory(bool composition, bool continued) {
            // Table and column names below are fixed application literals; callers supply only parameter values.
            var table = composition ? "trade.order_composition_history" : "trade.trade_selection_history_by_fund_date";
            var at = composition ? "evaluated_at_utc" : "occurred_at_utc";
            var hash = composition ? "result_hash" : "result_sha256";
            var continuation = !continued ? string.Empty : $" AND ({at},workflow_id,invocation_id)<($4,$5,$6)";
            var limit = !continued ? "$4" : "$7";
            return $"SELECT {at},workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,{hash} FROM {table} WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3{continuation} ORDER BY {at} DESC,workflow_id DESC,invocation_id DESC LIMIT {limit};";

}
internal static string WorkflowUpsert(string table, string columnList, string keyList, int valueCount, string? revision) {
            var columns = columnList.Split(',');
            var keys = keyList.Split(',');
            var assignments = string.Join(',', columns.Except(keys, StringComparer.Ordinal).Select(name => name + "=EXCLUDED." + name));
            var sql = "INSERT INTO trade." + table + "(" + columnList + ") VALUES(" + string.Join(',', Enumerable.Range(1, valueCount).Select(i => "$" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))) + ") ON CONFLICT(" + keyList + ") DO UPDATE SET " + assignments;
            if (revision is not null)
                sql += " WHERE trade." + table + "." + revision + "<=EXCLUDED." + revision;

 return sql + ";";
}
internal static string RiskHistory(bool continued)=>"SELECT payload,evaluated_at_utc,invocation_id FROM trade.risk_management_history WHERE portfolio_id=$1 AND fund_id=$2 AND value_date=$3"+(continued?" AND (evaluated_at_utc<$4 OR (evaluated_at_utc=$4 AND invocation_id>$5)) ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $6;":" ORDER BY evaluated_at_utc DESC,invocation_id ASC LIMIT $4;");

}
