

namespace TomasAI.IFM.Application.Storage.TradeDb;
internal static class TradeDbSql
{
    public const string DeleteOptionTrade = """
        delete from trade.option_trade
where orderid = $1
and tradeid = $2
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
FROM trade.option_trade_spread_data
WHERE
    orderId = $1
    AND tradeId = $2
    AND valueDate = $3
    AND tradeType = $4
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
        FROM trade.option_trade_spread_data
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
FROM trade.option_trade
WHERE orderId = $1
and tradeId = $2
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
FROM trade.option_trade
WHERE orderId = $1;
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
        FROM trade.option_trade;
    """;
    public const string InsertOptionTrade = """
        INSERT INTO trade.option_trade (
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
    ) ON CONFLICT (orderid, tradeid) DO UPDATE SET tradeDate=EXCLUDED.tradeDate, maturityDate=EXCLUDED.maturityDate, tradeType=EXCLUDED.tradeType, tradeState=EXCLUDED.tradeState, tradeStrategy=EXCLUDED.tradeStrategy, tradeAction=EXCLUDED.tradeAction, underlyingContractId=EXCLUDED.underlyingContractId, underlyingAssetType=EXCLUDED.underlyingAssetType, isPrimaryTrade=EXCLUDED.isPrimaryTrade, isHedgeTrade=EXCLUDED.isHedgeTrade, createdOn=EXCLUDED.createdOn, createdBy=EXCLUDED.createdBy, updatedOn=EXCLUDED.updatedOn, updatedBy=EXCLUDED.updatedBy;
""";
    public const string UpdateOptionTrade = """
        UPDATE trade.option_trade
SET tradeDate = $3,
    maturityDate = $4,
    tradeType = $5,
    tradeState = $6,
    tradeStrategy = $7,
    tradeAction = $8,
    underlyingContractId = $9,
    underlyingAssetType = $10,
    isPrimaryTrade = $11,
    isHedgeTrade = $12,
    updatedOn = $13,
    updatedBy = $14
WHERE orderId = $1
  AND tradeId = $2;
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
FROM trade.option_trade_spread_bar_data
WHERE orderId = $1
  AND tradeId = $2
  AND valueDate = $3
  AND tradeType = $4
  AND barDate >= $5
  AND barDate <= $6;
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
        FROM trade.option_trade_spread_bar_data;
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
        FROM trade.trade_position
        WHERE orderId = $1
        AND tradeId = $2;
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
        FROM trade.trade_position
        WHERE orderId = $1
        AND tradeId = $2
        AND valueDate = $3
        AND tradeStatus = $4
        AND daysToExpiry = $5;
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
        FROM trade.trade_position;
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
        FROM trade.option_leg
        WHERE tradeId = $1;
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
        FROM trade.option_leg
        WHERE orderId = $1
          AND tradeId = $2;
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
        FROM trade.option_leg
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
    FROM trade.option_leg_data
    WHERE OrderId = $1
    AND TradeId = $2
    AND ValueDate = $3;
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
        FROM trade.option_leg_data;
        """;
    public const string GetTradeTypeLimits = """
        SELECT TradeId AS "TradeId",
       TradeType AS "TradeType",
       MaxLossLimit AS "MaxLossLimit",
       MinProfitLimit AS "MinProfitLimit",
       MaxProfitLimit AS "MaxProfitLimit"
FROM trade.trade_type_limit
WHERE TradeId = $1;
""";
    public const string GetTradeTypeLimitAll = """
        SELECT TradeId AS "TradeId",
           TradeType AS "TradeType",
           MaxLossLimit AS "MaxLossLimit",
           MinProfitLimit AS "MinProfitLimit",
           MaxProfitLimit AS "MaxProfitLimit"
        FROM trade.trade_type_limit;
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
FROM trade.trade_position
WHERE orderId = $1
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
FROM trade.trade_position
WHERE OrderId = $1
  AND TradeId = $2
  AND ValueDate = $3
  AND TradeStatus = $4
  AND DaysToExpiry = $5
  AND TradeType = $6;
""";
    public const string GetTradeTypeLimit = """
        SELECT TradeId AS "TradeId",
 TradeType AS "TradeType",
    MaxLossLimit AS "MaxLossLimit",
    MinProfitLimit AS "MinProfitLimit",
    MaxProfitLimit as "MaxProfitLimit"
FROM trade.trade_type_limit
WHERE TradeId = $1
AND TradeType = $2;
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
FROM trade.trade_limit
WHERE TradeId = $1;
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
        FROM trade.trade_limit;
    """;
    public const string GetTradeFills = """
        SELECT OrderId AS "OrderId",
               TradeId AS "TradeId",
               FillDate AS "FillDate",
               FillQuantity AS "FillQuantity",
               CreatedOn AS "CreatedOn",
               CreatedBy AS "CreatedBy"
        FROM trade.trade_fill
        WHERE Orderid = $1
        AND TradeId = $2;
""";
    public const string GetTradeFillsAll = """
        SELECT OrderId AS "OrderId",
               TradeId AS "TradeId",
               FillDate AS "FillDate",
               FillQuantity AS "FillQuantity",
               CreatedOn AS "CreatedOn",
               CreatedBy AS "CreatedBy"
        FROM trade.trade_fill;
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
FROM trade.trade_fill_data
WHERE OrderId = $1
AND TradeId = $2
AND FillDate >= $3;
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
        FROM trade.trade_placement_signal
        WHERE contractId = $1
          AND valueDate = $2
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
FROM trade.trade_plan
WHERE OrderId = $1;
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
FROM trade.trade_plan
WHERE OrderId = $1
And TradeId = $2;
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
    FROM trade.trade_plan;
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
FROM trade.trade_plan
WHERE OrderId = $1
and tradeId = $2
and valueDate = $3;
""";
    public const string GetTradePlanStopLossLimit = """
        SELECT StopLossLimit AS "StopLossLimit"
FROM trade.trade_plan
WHERE OrderId = $1 AND TradeId = $2
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
FROM trade.trade_plan
WHERE OrderId = $1
and tradeId = $2
and valueDate >= $3
and valueDate <= $4
""";
    public const string GetTradePlanForwardLossRatios = """
        select forwardLossRatio as "ForwardLossRatio"
from trade.trade_plan_forward_loss_ratio
where partitionId = 1
and  valueDate >= $1
and valueDate <= $2
""";
    public const string GetLastTradePlanForwardLossRatio = """
        select forwardLossRatio as "ForwardLossRatio", sequenceId as "SequenceId"
from trade.trade_plan_forward_loss_ratio
where partitionId = 1
and valueDate = $1
""";
    public const string GetTradePlanForwardLossLimit = """
        SELECT
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    TradeType AS "TradeType",
    ValueDate AS "ValueDate",
    LimitType AS "LimitType"
FROM trade.trade_plan_forward_loss_limit
WHERE OrderId = $1 AND TradeId = $2 AND ValueDate = $3 AND TradeType = $4;
""";
    public const string GetTradeLiveFeed = """
        SELECT
    OrderId AS "OrderId",
    TradeId AS "TradeId",
    LiveFeed AS "LiveFeed"
FROM trade.trade_live_feed
WHERE OrderId = $1
AND TradeId = $2;
""";
    public const string GetTradeFillDataByTradeId = """
        SELECT
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
FROM trade.trade_fill_data
WHERE TradeId = $1;
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
FROM trade.option_leg
WHERE tradeId = $1
AND contractId = $2
""";
    public const string InsertOptionLeg = """
        INSERT INTO trade.option_leg (
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
        ) ON CONFLICT (tradeid, orderid, contractid) DO UPDATE SET Quantity=EXCLUDED.Quantity, StrikePrice=EXCLUDED.StrikePrice, OptionLegType=EXCLUDED.OptionLegType, OptionLegAction=EXCLUDED.OptionLegAction, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy, UpdatedOn=EXCLUDED.UpdatedOn, UpdatedBy=EXCLUDED.UpdatedBy;
""";
    public const string InsertOptionLegData = """
        INSERT INTO trade.option_leg_data (
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
        ) ON CONFLICT (orderid, tradeid, valuedate, tradetype, daystoexpiry, tradestatus, optionlegid) DO UPDATE SET BidPrice=EXCLUDED.BidPrice, AskPrice=EXCLUDED.AskPrice, ImpliedVolatility=EXCLUDED.ImpliedVolatility, Delta=EXCLUDED.Delta, Gamma=EXCLUDED.Gamma, Theta=EXCLUDED.Theta, Vega=EXCLUDED.Vega, Rho=EXCLUDED.Rho, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy, UpdatedOn=EXCLUDED.UpdatedOn, UpdatedBy=EXCLUDED.UpdatedBy;
""";
    public const string UpdateOptionLegData = """
        UPDATE trade.option_leg_data
        SET
            BidPrice = $1,
            AskPrice = $2,
            ImpliedVolatility = $3,
            Delta = $4,
            Gamma = $5,
            Theta = $6,
            Vega = $7,
            Rho = $8,
            UpdatedOn = $9,
            UpdatedBy = $10
        WHERE OrderId = $11
        AND TradeId = $12
        AND ValueDate = $13
        AND OptionLegId = $14;
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
FROM trade.option_leg_data
WHERE TradeId = $1
AND ValueDate = $2
and OptionLegId = $3;
""";
    public const string DeleteTradePosition = """
        delete from trade.trade_position
where orderid = $1
and tradeid = $2
""";
    public const string DeleteTradePositionByPrimaryKey = """
        delete from trade.trade_position
        where OrderId = $1
      AND TradeId = $2
      AND ValueDate = $3
      AND TradeStatus = $4
      AND DaysToExpiry = $5
      AND TradeType = $6;
""";
    public const string DeleteOptionLeg = """
        delete from trade.option_leg
        where OrderId = $1
        and TradeId = $2
        """;
    public const string DeleteOptionLegById = """
        delete from trade.option_leg
        where OrderId = $1
        and TradeId = $2
        and ContractId = $3
        """;
    public const string DeleteOptionLegData = """
        delete from trade.option_leg_data
        where orderId = $1
        and tradeId = $2;
        """;
    public const string DeleteOptionLegDataById = """
        delete from trade.option_leg_data
        where orderId = $1
        and tradeId = $2
        and valueDate = $3
        and optionlegId = $4
        """;
    public const string DeleteTradeTypeLimit = """
        delete from trade.trade_type_limit
where tradeid = $1
""";
    public const string DeleteTradeLimit = """
        delete from trade.trade_limit
where tradeid = $1
""";
    public const string DeleteTradeLimitByTradeType = """
        delete from trade.trade_limit
where tradeid = $1
and tradetype = $2
""";
    public const string DeleteTradeFill = """
        delete from trade.trade_fill
        where orderId = $1
        and tradeId = $2;
""";
    public const string DeleteTradeFillById = """
        delete from trade.trade_fill
        where orderId = $1
        and tradeId = $2
        and fillDate = $3;
""";
    public const string DeleteTradeFillData = """
        delete from trade.trade_fill_data
        where orderId = $1
        and tradeId = $2;
        """;
    public const string DeleteTradePlacementSignal = """
        delete from trade.trade_placement_signal
        where ContractId = $1
        and ValueDate = $2;
        """;
    public const string DeleteTradePlacementSignalBySequenceId = """
        delete from trade.trade_placement_signal
        where ContractId = $1
        and ValueDate = $2
        and SequenceId = $3;
        """;
    public const string DeleteOptionTradeSpreadData = """
        DELETE FROM trade.option_trade_spread_data
WHERE OrderId = $1 AND TradeId = $2 AND ValueDate = $3 AND TradeType = $4;
""";
    public const string InsertTradePosition = """
        INSERT INTO trade.trade_position (
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
        ) ON CONFLICT (orderid, tradeid, valuedate, tradestatus, daystoexpiry, tradetype) DO UPDATE SET Commission=EXCLUDED.Commission, DeltaHedge=EXCLUDED.DeltaHedge, NetSpread=EXCLUDED.NetSpread, TradeValue=EXCLUDED.TradeValue, TradePnl=EXCLUDED.TradePnl, AssetPrice=EXCLUDED.AssetPrice, OTMProbability=EXCLUDED.OTMProbability, ForwardPrice=EXCLUDED.ForwardPrice, ForwardLossRatio=EXCLUDED.ForwardLossRatio, LossProbability=EXCLUDED.LossProbability, RiskFreeRate=EXCLUDED.RiskFreeRate, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy, UpdatedOn=EXCLUDED.UpdatedOn, UpdatedBy=EXCLUDED.UpdatedBy;
""";
    public const string InsertTradeLimit = """
        INSERT INTO trade.trade_limit (
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
) ON CONFLICT (tradeid, tradetype) DO UPDATE SET RiskMargin=EXCLUDED.RiskMargin, MaxProfit=EXCLUDED.MaxProfit, MaxLoss=EXCLUDED.MaxLoss, MaxReturn=EXCLUDED.MaxReturn, MaxLossLimit=EXCLUDED.MaxLossLimit, MinProfitLimit=EXCLUDED.MinProfitLimit, MaxProfitLimit=EXCLUDED.MaxProfitLimit, MinProfitTarget=EXCLUDED.MinProfitTarget, DailyProfitTarget=EXCLUDED.DailyProfitTarget, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy, UpdatedOn=EXCLUDED.UpdatedOn, UpdatedBy=EXCLUDED.UpdatedBy;
""";
    public const string InsertTradeTypeLimit = """
        INSERT INTO trade.trade_type_limit (
            TradeId,
            TradeType,
            MaxLossLimit,
            MinProfitLimit,
            MaxProfitLimit
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5
        ) ON CONFLICT (tradeid, tradetype) DO UPDATE SET MaxLossLimit=EXCLUDED.MaxLossLimit, MinProfitLimit=EXCLUDED.MinProfitLimit, MaxProfitLimit=EXCLUDED.MaxProfitLimit;
""";
    public const string InsertTradeFill = """
        INSERT INTO trade.trade_fill (
            OrderId,
            TradeId,
            FillDate,
            FillQuantity,
            CreatedOn,
            CreatedBy
        ) VALUES (
            $1,
            $2,
            $3,
            $4,
            $5,
            $6
        ) ON CONFLICT (orderid, tradeid, filldate) DO UPDATE SET FillQuantity=EXCLUDED.FillQuantity, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy;
""";
    public const string InsertTradeFillData = """
        INSERT INTO trade.trade_fill_data (
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
    )  ON CONFLICT (orderid, tradeid, filldate, contractid) DO UPDATE SET BidPrice=EXCLUDED.BidPrice, AskPrice=EXCLUDED.AskPrice, Commission=EXCLUDED.Commission, OptionLegAction=EXCLUDED.OptionLegAction, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy;
""";
    public const string InsertOptionTradeSpreadData = """
        INSERT INTO trade.option_trade_spread_data (
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
) ON CONFLICT (orderid, tradeid, valuedate, tradetype, sequenceid) DO UPDATE SET LossLimit=EXCLUDED.LossLimit, WinLimit=EXCLUDED.WinLimit, ForwardSpread=EXCLUDED.ForwardSpread, NetSpread=EXCLUDED.NetSpread, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy;
""";
    public const string InsertOptionTradeSpreadBarData = """
        INSERT INTO trade.option_trade_spread_bar_data (
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
            $1,
            $2,
            $3,
            $4,
            $5,
            $6,
            $7,
            $8,
            $9
        ) ON CONFLICT (orderid, tradeid, valuedate, tradetype, bardate) DO UPDATE SET LossLimit=EXCLUDED.LossLimit, WinLimit=EXCLUDED.WinLimit, ForwardSpread=EXCLUDED.ForwardSpread, NetSpread=EXCLUDED.NetSpread;
""";
    public const string InsertTradeLiveFeed = """
        INSERT INTO trade.trade_live_feed (
    OrderId,
    TradeId,
    LiveFeed
) VALUES (
    $1,
    $2,
    $3
) ON CONFLICT (orderid, tradeid) DO NOTHING;
""";
    public const string InsertTradePositionState = """
        INSERT INTO trade.trade_position_state (
    OrderId,
    TradeId,
    TradePositionState,
    OpenedOn,
    OpenedBy
) VALUES (
    $1,
    $2,
    $3,
    $4,
    $5
) ON CONFLICT (orderid, tradeid) DO NOTHING;
""";
    public const string UpdateTradePlanForwardLossLimit = """
        UPDATE trade.trade_plan_forward_loss_limit
SET LimitType = $1
WHERE OrderId = $2
  AND TradeId = $3
  AND TradeType = $4
  AND ValueDate = $5;
""";
    public const string InsertTradePlacementSignal = """
        INSERT INTO trade.trade_placement_signal (
    SequenceId,
    ContractId,
    ValueDate,
    TradePlacementSignal,
    TradePrice,
    CreatedOn,
    CreatedBy
) VALUES (
    $1,
    $2,
    $3,
    $4,
    $5,
    $6,
    $7
) ON CONFLICT (contractid, valuedate, sequenceid) DO UPDATE SET TradePlacementSignal=EXCLUDED.TradePlacementSignal, TradePrice=EXCLUDED.TradePrice, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy;
""";
    public const string UpdateTradePosition = """
        UPDATE trade.trade_position
SET
    Commission = $1,
    DeltaHedge = $2,
    NetSpread = $3,
    TradeValue = $4,
    TradePnl = $5,
    AssetPrice = $6,
    OTMProbability = $7,
    ForwardPrice = $8,
    LossProbability = $9,
    RiskFreeRate = $10,
    UpdatedOn = $11,
    UpdatedBy = $12
WHERE
    OrderId = $13
    AND TradeId = $14
    AND ValueDate = $15
    AND TradeStatus = $16
    AND DaysToExpiry = $17
    AND TradeType = $18;
""";
    public const string UpdateOptionTradeState = """
        UPDATE trade.option_trade
SET
    TradeState = $1
WHERE
    OrderId = $2
    AND TradeId = $3;
""";
    public const string UpdateTradeLiveFeed = """
        UPDATE trade.trade_live_feed
SET LiveFeed = $1
WHERE OrderId = $2
AND TradeId = $3
""";
    public const string UpdateTradePositionStatus = """
        UPDATE trade.trade_position
SET TradeStatus = $1,
    UpdatedOn = $2,
    UpdatedBy = $3
WHERE TradeId = $4
AND TradeType = $5
AND ValueDate = $6
AND DaysToExpiry = $7
AND TradeStatus = $8
""";
    public const string UpdateOptionLegDataStatus = """
        UPDATE trade.option_leg_data
SET TradeStatus = $1,
    UpdatedOn = $2,
    UpdatedBy = $3
WHERE TradeId = $4
AND ValueDate = $5
AND OptionLegId = $6
""";
    public const string DeleteTradePositionState = """
        DELETE FROM trade.trade_position_state
WHERE OrderId = $1
  AND TradeId = $2;
""";
    public const string DeleteOptionTradeSpreadBarData = """
        DELETE FROM trade.option_trade_spread_bar_data
WHERE OrderId = $1 AND TradeId = $2 AND ValueDate = $3 AND TradeType = $4;
""";
    public const string DeleteTradeLiveFeed = """
        DELETE FROM trade.trade_live_feed
WHERE OrderId = $1 AND TradeId = $2;
""";
    public const string DeleteTradeLiveFeeds = """
        DELETE FROM trade.trade_live_feed
WHERE OrderId = $1;
""";
    public const string DeleteTradePlanForwardLossLimit = """
        DELETE FROM trade.trade_plan_forward_loss_limit
WHERE OrderId = $1 AND TradeId = $2 AND ValueDate = $3 AND TradeType = $4;
""";
    public const string UpdateTradeLimitDailyProfitTarget = """
        UPDATE trade.trade_limit
SET DailyProfitTarget = $1,
    UpdatedOn = $2,
    UpdatedBy = $3
WHERE TradeId = $4
AND TradeType = $5;
""";
    public const string InsertTradePlanForwardLossLimit = """
        INSERT INTO trade.trade_plan_forward_loss_limit (OrderId, TradeId, TradeType, ValueDate, LimitType)
VALUES ($1, $2, $3, $4, $5) ON CONFLICT (orderid, tradeid, valuedate, tradetype) DO UPDATE SET LimitType=EXCLUDED.LimitType;
""";
    public const string InsertTradePlan = """
        INSERT INTO trade.trade_plan (
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
) ON CONFLICT (orderid, tradeid, valuedate, sequenceid) DO UPDATE SET TradeType=EXCLUDED.TradeType, TradeDate=EXCLUDED.TradeDate, MaturityDate=EXCLUDED.MaturityDate, ActionDate=EXCLUDED.ActionDate, ActionType=EXCLUDED.ActionType, ActionSubType=EXCLUDED.ActionSubType, ActionState=EXCLUDED.ActionState, ActionReason=EXCLUDED.ActionReason, TradePnl=EXCLUDED.TradePnl, ForwardLossRatio=EXCLUDED.ForwardLossRatio, LossProbability=EXCLUDED.LossProbability, MScore=EXCLUDED.MScore, MaxProfit=EXCLUDED.MaxProfit, MaxLoss=EXCLUDED.MaxLoss, MinProfitTarget=EXCLUDED.MinProfitTarget, DailyProfitTarget=EXCLUDED.DailyProfitTarget, AssetPrice=EXCLUDED.AssetPrice, AssetStdDev=EXCLUDED.AssetStdDev, AssetMean=EXCLUDED.AssetMean, AssetPriceChange=EXCLUDED.AssetPriceChange, MarketTrend=EXCLUDED.MarketTrend, MarketVolatility=EXCLUDED.MarketVolatility, MarketDirection=EXCLUDED.MarketDirection, VixVolatility=EXCLUDED.VixVolatility, TradeRisk=EXCLUDED.TradeRisk, FiftyDayMA=EXCLUDED.FiftyDayMA, FiveDayXMA=EXCLUDED.FiveDayXMA, PutOTMProbability=EXCLUDED.PutOTMProbability, CallOTMProbability=EXCLUDED.CallOTMProbability, ShortPutGamma=EXCLUDED.ShortPutGamma, ShortCallGamma=EXCLUDED.ShortCallGamma, GammaRisk=EXCLUDED.GammaRisk, NetPrice=EXCLUDED.NetPrice, ForwardPrice=EXCLUDED.ForwardPrice, ForwardDelta=EXCLUDED.ForwardDelta, StopLossLimit=EXCLUDED.StopLossLimit, TrendType=EXCLUDED.TrendType, TrendStrength=EXCLUDED.TrendStrength, RSI=EXCLUDED.RSI, RSISlope=EXCLUDED.RSISlope, TDI=EXCLUDED.TDI, TDIStrength=EXCLUDED.TDIStrength, CreatedOn=EXCLUDED.CreatedOn, CreatedBy=EXCLUDED.CreatedBy;
""";
    public const string InsertTradePlanForwardLossRatio = """
        insert into trade.trade_plan_forward_loss_ratio (
 partitionId,
 valueDate,
 forwardLossRatio,
 sequenceId
) values (
 $1,
 $2,
 $3,
 $4
) ON CONFLICT (partitionid, valuedate, forwardlossratio, sequenceid) DO NOTHING;
""";
    public const string GetIntrinsicTimeStrategyWorkflow = """
SELECT workflowId, workflowEntityId, workflowDefinitionId, workflowDefinitionVersion,
       contractId, timeFrameStartValueDate, timePeriod, triggerEventId, correlationId,
       status, outcome, currentStage, workflowRevision, lastEventId, stateSchemaVersion,
       statePayload, stopReasonCode, startedAtUtc, terminalAtUtc, updatedAtUtc
FROM trade.intrinsic_time_strategy_workflow
WHERE workflowId = $1;
""";
    public const string GetRegimeDiscovery = """
SELECT workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
       sourceEventSequence, status, parameterPayloadSha256, signalSnapshotId, resultPayload,
       resultPayloadSha256, failureCode, failureMessage, reasonsPayload, schemaVersion,
       terminalAtUtc, updatedAtUtc
FROM trade.regime_discovery
WHERE workflowId = $1;
""";
    public const string GetMarketCondition = """
SELECT workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
       fundId, instrumentRoot, targetHorizon, parameterSetId, parameterSetVersion,
       parameterPayloadSha256, snapshotId, snapshotSha256, tradeability, conditionType,
       direction, phase, strength, confidence, primaryReasonCode, resultPayload,
       resultPayloadSha256, evaluatedAtUtc, validUntilUtc, marketDataAsOfUtc, completedAtUtc, updatedAtUtc,
       volatilityBehavior, liquidityQuality, dataQuality, upstreamAlignment, evidencePayload,
       conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload, summaryText
FROM trade.market_condition WHERE workflowId = $1;
""";
    public const string GetMarketConditionHistory = """
SELECT workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
       fundId, instrumentRoot, targetHorizon, parameterSetId, parameterSetVersion,
       parameterPayloadSha256, snapshotId, snapshotSha256, tradeability, conditionType,
       direction, phase, strength, confidence, primaryReasonCode, resultPayload,
       resultPayloadSha256, evaluatedAtUtc, validUntilUtc, marketDataAsOfUtc, completedAtUtc, updatedAtUtc,
       volatilityBehavior, liquidityQuality, dataQuality, upstreamAlignment, evidencePayload,
       conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload, summaryText
FROM trade.market_condition_by_fund
WHERE fundId = $1 AND instrumentRoot = $2 AND targetHorizon = $3
  AND evaluatedAtUtc < $4
LIMIT $5;
""";
    public const string UpsertMarketCondition = """
INSERT INTO trade.market_condition (workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
    fundId, instrumentRoot, targetHorizon, parameterSetId, parameterSetVersion, parameterPayloadSha256,
    snapshotId, snapshotSha256, tradeability, conditionType, direction, phase, strength, confidence,
    primaryReasonCode, resultPayload, resultPayloadSha256, evaluatedAtUtc, validUntilUtc, marketDataAsOfUtc,
    completedAtUtc, updatedAtUtc, volatilityBehavior, liquidityQuality, dataQuality, upstreamAlignment,
    evidencePayload, conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload, summaryText)
VALUES ($1, $2, $3, $4, $5,
    $6, $7, $8, $9, $10, $11,
    $12, $13, $14, $15, $16, $17, $18, $19,
    $20, $21, $22, $23, $24, $25,
    $26, $27, $28, $29, $30, $31,
    $32, $33, $34, $35, $36) ON CONFLICT (workflowid) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId, inputWorkflowRevision=EXCLUDED.inputWorkflowRevision, commandId=EXCLUDED.commandId, sourceEventId=EXCLUDED.sourceEventId, fundId=EXCLUDED.fundId, instrumentRoot=EXCLUDED.instrumentRoot, targetHorizon=EXCLUDED.targetHorizon, parameterSetId=EXCLUDED.parameterSetId, parameterSetVersion=EXCLUDED.parameterSetVersion, parameterPayloadSha256=EXCLUDED.parameterPayloadSha256, snapshotId=EXCLUDED.snapshotId, snapshotSha256=EXCLUDED.snapshotSha256, tradeability=EXCLUDED.tradeability, conditionType=EXCLUDED.conditionType, direction=EXCLUDED.direction, phase=EXCLUDED.phase, strength=EXCLUDED.strength, confidence=EXCLUDED.confidence, primaryReasonCode=EXCLUDED.primaryReasonCode, resultPayload=EXCLUDED.resultPayload, resultPayloadSha256=EXCLUDED.resultPayloadSha256, evaluatedAtUtc=EXCLUDED.evaluatedAtUtc, validUntilUtc=EXCLUDED.validUntilUtc, marketDataAsOfUtc=EXCLUDED.marketDataAsOfUtc, completedAtUtc=EXCLUDED.completedAtUtc, updatedAtUtc=EXCLUDED.updatedAtUtc, volatilityBehavior=EXCLUDED.volatilityBehavior, liquidityQuality=EXCLUDED.liquidityQuality, dataQuality=EXCLUDED.dataQuality, upstreamAlignment=EXCLUDED.upstreamAlignment, evidencePayload=EXCLUDED.evidencePayload, conflictingEvidencePayload=EXCLUDED.conflictingEvidencePayload, blockingReasonsPayload=EXCLUDED.blockingReasonsPayload, reasonsPayload=EXCLUDED.reasonsPayload, summaryText=EXCLUDED.summaryText;
""";
    public const string UpsertMarketConditionByFund = """
INSERT INTO trade.market_condition_by_fund (fundId, instrumentRoot, targetHorizon, evaluatedAtUtc, workflowId,
    workflowEntityId, inputWorkflowRevision, commandId, sourceEventId, parameterSetId, parameterSetVersion,
    parameterPayloadSha256, snapshotId, snapshotSha256, tradeability, conditionType, direction, phase,
    strength, confidence, primaryReasonCode, resultPayload, resultPayloadSha256, validUntilUtc,
    marketDataAsOfUtc, completedAtUtc, updatedAtUtc, volatilityBehavior, liquidityQuality, dataQuality,
    upstreamAlignment, evidencePayload, conflictingEvidencePayload, blockingReasonsPayload, reasonsPayload,
    summaryText)
VALUES ($1, $2, $3, $4, $5,
    $6, $7, $8, $9, $10,
    $11, $12, $13, $14, $15,
    $16, $17, $18, $19, $20, $21, $22,
    $23, $24, $25, $26, $27,
    $28, $29, $30, $31, $32,
    $33, $34, $35, $36) ON CONFLICT (fundid, instrumentroot, targethorizon, evaluatedatutc, workflowid) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId, inputWorkflowRevision=EXCLUDED.inputWorkflowRevision, commandId=EXCLUDED.commandId, sourceEventId=EXCLUDED.sourceEventId, parameterSetId=EXCLUDED.parameterSetId, parameterSetVersion=EXCLUDED.parameterSetVersion, parameterPayloadSha256=EXCLUDED.parameterPayloadSha256, snapshotId=EXCLUDED.snapshotId, snapshotSha256=EXCLUDED.snapshotSha256, tradeability=EXCLUDED.tradeability, conditionType=EXCLUDED.conditionType, direction=EXCLUDED.direction, phase=EXCLUDED.phase, strength=EXCLUDED.strength, confidence=EXCLUDED.confidence, primaryReasonCode=EXCLUDED.primaryReasonCode, resultPayload=EXCLUDED.resultPayload, resultPayloadSha256=EXCLUDED.resultPayloadSha256, validUntilUtc=EXCLUDED.validUntilUtc, marketDataAsOfUtc=EXCLUDED.marketDataAsOfUtc, completedAtUtc=EXCLUDED.completedAtUtc, updatedAtUtc=EXCLUDED.updatedAtUtc, volatilityBehavior=EXCLUDED.volatilityBehavior, liquidityQuality=EXCLUDED.liquidityQuality, dataQuality=EXCLUDED.dataQuality, upstreamAlignment=EXCLUDED.upstreamAlignment, evidencePayload=EXCLUDED.evidencePayload, conflictingEvidencePayload=EXCLUDED.conflictingEvidencePayload, blockingReasonsPayload=EXCLUDED.blockingReasonsPayload, reasonsPayload=EXCLUDED.reasonsPayload, summaryText=EXCLUDED.summaryText;
""";
    public const string UpsertRegimeDiscovery = """
INSERT INTO trade.regime_discovery (
    workflowId, workflowEntityId, inputWorkflowRevision, commandId, sourceEventId,
    sourceEventSequence, status, parameterPayloadSha256, signalSnapshotId, resultPayload,
    resultPayloadSha256, failureCode, failureMessage, reasonsPayload, schemaVersion,
    terminalAtUtc, updatedAtUtc)
VALUES ($1, $2, $3, $4, $5,
    $6, $7, $8, $9, $10,
    $11, $12, $13, $14, $15,
    $16, $17) ON CONFLICT (workflowid) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId, inputWorkflowRevision=EXCLUDED.inputWorkflowRevision, commandId=EXCLUDED.commandId, sourceEventId=EXCLUDED.sourceEventId, sourceEventSequence=EXCLUDED.sourceEventSequence, status=EXCLUDED.status, parameterPayloadSha256=EXCLUDED.parameterPayloadSha256, signalSnapshotId=EXCLUDED.signalSnapshotId, resultPayload=EXCLUDED.resultPayload, resultPayloadSha256=EXCLUDED.resultPayloadSha256, failureCode=EXCLUDED.failureCode, failureMessage=EXCLUDED.failureMessage, reasonsPayload=EXCLUDED.reasonsPayload, schemaVersion=EXCLUDED.schemaVersion, terminalAtUtc=EXCLUDED.terminalAtUtc, updatedAtUtc=EXCLUDED.updatedAtUtc;
""";
    public const string GetActiveIntrinsicTimeStrategyWorkflow = """
SELECT workflowEntityId, workflowId, contractId, timeFrameStartValueDate, timePeriod,
       currentStage, workflowRevision, lastEventId, stateSchemaVersion, statePayload,
       startedAtUtc, updatedAtUtc
FROM trade.intrinsic_time_strategy_workflow_active
WHERE workflowEntityId = $1;
""";
    public const string GetIntrinsicTimeStrategyWorkflowStartAttempts = """
SELECT workflowEntityId, requestedAtUtc, requestedWorkflowId, decision, activeWorkflowId,
       startCommandId, triggerEventId, activeStage, reasonCode, sourceEventId
FROM trade.intrinsic_time_strategy_workflow_start_attempt
WHERE workflowEntityId = $1 AND requestedAtUtc < $2
LIMIT $3;
""";
    public const string GetIntrinsicTimeStrategyWorkflowTimeline = """
SELECT workflowId, eventId, workflowEntityId, workflowRevision, stage, eventName,
       eventSchemaVersion, eventPayload, occurredAtUtc
FROM trade.intrinsic_time_strategy_workflow_timeline
WHERE workflowId = $1 AND eventId > $2
LIMIT $3;
""";
    public const string GetIntrinsicTimeStrategyWorkflowsByEntity = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM trade.intrinsic_time_strategy_workflow_by_entity
WHERE workflowEntityId = $1 AND startedAtUtc < $2
LIMIT $3;
""";
    public const string GetIntrinsicTimeStrategyWorkflowsByStatusDay = """
SELECT workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
       workflowRevision, terminalAtUtc, stopReasonCode
FROM trade.intrinsic_time_strategy_workflow_by_status_day
WHERE status = $1 AND startedDate = $2
LIMIT $3;
""";
    public const string UpsertIntrinsicTimeStrategyWorkflow = """
INSERT INTO trade.intrinsic_time_strategy_workflow (
    workflowId, workflowEntityId, workflowDefinitionId, workflowDefinitionVersion,
    contractId, timeFrameStartValueDate, timePeriod, triggerEventId, correlationId,
    status, outcome, currentStage, workflowRevision, lastEventId, stateSchemaVersion,
    statePayload, stopReasonCode, startedAtUtc, terminalAtUtc, updatedAtUtc)
VALUES ($1, $2, $3, $4,
    $5, $6, $7, $8, $9,
    $10, $11, $12, $13, $14, $15,
    $16, $17, $18, $19, $20) ON CONFLICT (workflowid) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId, workflowDefinitionId=EXCLUDED.workflowDefinitionId, workflowDefinitionVersion=EXCLUDED.workflowDefinitionVersion, contractId=EXCLUDED.contractId, timeFrameStartValueDate=EXCLUDED.timeFrameStartValueDate, timePeriod=EXCLUDED.timePeriod, triggerEventId=EXCLUDED.triggerEventId, correlationId=EXCLUDED.correlationId, status=EXCLUDED.status, outcome=EXCLUDED.outcome, currentStage=EXCLUDED.currentStage, workflowRevision=EXCLUDED.workflowRevision, lastEventId=EXCLUDED.lastEventId, stateSchemaVersion=EXCLUDED.stateSchemaVersion, statePayload=EXCLUDED.statePayload, stopReasonCode=EXCLUDED.stopReasonCode, startedAtUtc=EXCLUDED.startedAtUtc, terminalAtUtc=EXCLUDED.terminalAtUtc, updatedAtUtc=EXCLUDED.updatedAtUtc;
""";
    public const string UpsertActiveIntrinsicTimeStrategyWorkflow = """
INSERT INTO trade.intrinsic_time_strategy_workflow_active (
    workflowEntityId, workflowId, contractId, timeFrameStartValueDate, timePeriod,
    currentStage, workflowRevision, lastEventId, stateSchemaVersion, statePayload,
    startedAtUtc, updatedAtUtc)
VALUES ($1, $2, $3, $4, $5,
    $6, $7, $8, $9, $10,
    $11, $12) ON CONFLICT (workflowentityid) DO UPDATE SET workflowId=EXCLUDED.workflowId, contractId=EXCLUDED.contractId, timeFrameStartValueDate=EXCLUDED.timeFrameStartValueDate, timePeriod=EXCLUDED.timePeriod, currentStage=EXCLUDED.currentStage, workflowRevision=EXCLUDED.workflowRevision, lastEventId=EXCLUDED.lastEventId, stateSchemaVersion=EXCLUDED.stateSchemaVersion, statePayload=EXCLUDED.statePayload, startedAtUtc=EXCLUDED.startedAtUtc, updatedAtUtc=EXCLUDED.updatedAtUtc;
""";
    public const string DeleteActiveIntrinsicTimeStrategyWorkflow = """
DELETE FROM trade.intrinsic_time_strategy_workflow_active
WHERE workflowEntityId = $1;
""";
    public const string InsertIntrinsicTimeStrategyWorkflowStartAttempt = """
INSERT INTO trade.intrinsic_time_strategy_workflow_start_attempt (
    workflowEntityId, requestedAtUtc, requestedWorkflowId, decision, activeWorkflowId,
    startCommandId, triggerEventId, activeStage, reasonCode, sourceEventId)
VALUES ($1, $2, $3, $4, $5,
    $6, $7, $8, $9, $10) ON CONFLICT (workflowentityid, requestedatutc, requestedworkflowid) DO UPDATE SET decision=EXCLUDED.decision, activeWorkflowId=EXCLUDED.activeWorkflowId, startCommandId=EXCLUDED.startCommandId, triggerEventId=EXCLUDED.triggerEventId, activeStage=EXCLUDED.activeStage, reasonCode=EXCLUDED.reasonCode, sourceEventId=EXCLUDED.sourceEventId;
""";
    public const string InsertIntrinsicTimeStrategyWorkflowTimeline = """
INSERT INTO trade.intrinsic_time_strategy_workflow_timeline (
    workflowId, eventId, workflowEntityId, workflowRevision, stage, eventName,
    eventSchemaVersion, eventPayload, occurredAtUtc)
VALUES ($1, $2, $3, $4, $5, $6,
    $7, $8, $9) ON CONFLICT (workflowid, eventid) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId, workflowRevision=EXCLUDED.workflowRevision, stage=EXCLUDED.stage, eventName=EXCLUDED.eventName, eventSchemaVersion=EXCLUDED.eventSchemaVersion, eventPayload=EXCLUDED.eventPayload, occurredAtUtc=EXCLUDED.occurredAtUtc;
""";
    public const string UpsertIntrinsicTimeStrategyWorkflowByEntity = """
INSERT INTO trade.intrinsic_time_strategy_workflow_by_entity (
    workflowEntityId, startedAtUtc, workflowId, status, outcome, currentStage,
    workflowRevision, terminalAtUtc, stopReasonCode)
VALUES ($1, $2, $3, $4, $5, $6,
    $7, $8, $9) ON CONFLICT (workflowentityid, startedatutc, workflowid) DO UPDATE SET status=EXCLUDED.status, outcome=EXCLUDED.outcome, currentStage=EXCLUDED.currentStage, workflowRevision=EXCLUDED.workflowRevision, terminalAtUtc=EXCLUDED.terminalAtUtc, stopReasonCode=EXCLUDED.stopReasonCode;
""";
    public const string UpsertIntrinsicTimeStrategyWorkflowByStatusDay = """
INSERT INTO trade.intrinsic_time_strategy_workflow_by_status_day (
    status, startedDate, startedAtUtc, workflowId, workflowEntityId, outcome,
    currentStage, workflowRevision, terminalAtUtc, stopReasonCode)
VALUES ($1, $2, $3, $4, $5, $6,
    $7, $8, $9, $10) ON CONFLICT (status, starteddate, startedatutc, workflowid) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId, outcome=EXCLUDED.outcome, currentStage=EXCLUDED.currentStage, workflowRevision=EXCLUDED.workflowRevision, terminalAtUtc=EXCLUDED.terminalAtUtc, stopReasonCode=EXCLUDED.stopReasonCode;
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
    internal const string ContextStatement2 = "SELECT payload,content_hash FROM trade.risk_management_invocation WHERE workflow_id=$1 AND invocation_id=$2 ORDER BY revision DESC LIMIT 1;";

    internal const string ContextStatement4 = "SELECT payload FROM trade.trade_order WHERE portfolioId=$1 AND fundId=$2 AND orderId=$3 ORDER BY revision DESC LIMIT 1;";
    internal const string ContextStatement5 = "SELECT payload FROM trade.established_trade_history WHERE portfolioId=$1 AND fundId=$2 AND strategyKind=$3 AND establishedAtUtc>=$4 AND establishedAtUtc<=$5;";
    internal const string HelperStatement6 = "SELECT payload,payload_sha256 FROM trade.market_condition_assessment_by_profile WHERE market_profile_id=$1 AND instrument_root=$2 AND target_horizon=$3 AND evaluated_at_utc<$4 ORDER BY evaluated_at_utc DESC,workflow_id LIMIT $5;";
    internal const string HelperStatement7 = "WITH inserted AS (INSERT INTO trade.risk_management_invocation (workflow_id,invocation_id,revision,payload,content_hash) VALUES ($1,$2,$3,$4,$5) ON CONFLICT(workflow_id,invocation_id,revision) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM inserted);";
    internal const string HelperStatement8 = "SELECT content_hash FROM trade.risk_management_invocation WHERE workflow_id=$1 AND invocation_id=$2 AND revision=$3;";
    internal const string HelperStatement9 = "WITH inserted AS (INSERT INTO trade.risk_management_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload) VALUES ($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id) DO NOTHING RETURNING 1) SELECT EXISTS(SELECT 1 FROM inserted);";
    internal const string HelperStatement10 = "WITH changed AS (UPDATE trade.risk_management_history SET revision=$1,payload=$2 WHERE portfolio_id=$3 AND fund_id=$4 AND value_date=$5 AND evaluated_at_utc=$6 AND invocation_id=$7 AND revision<$8 RETURNING 1) SELECT EXISTS(SELECT 1 FROM changed);";
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
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT (contractid, positionid, tradelegid) DO UPDATE SET portfolioId=EXCLUDED.portfolioId, fundId=EXCLUDED.fundId, orderId=EXCLUDED.orderId, tradeId=EXCLUDED.tradeId, tradeType=EXCLUDED.tradeType, generation=EXCLUDED.generation;
""";
    internal const string HelperStatement23 = """
                INSERT INTO trade.open_position_route_recovery(shard,positionId,tradeLegId,contractId,portfolioId,fundId,orderId,tradeId,tradeType,generation)
                VALUES(0,$1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT (shard, positionid, tradelegid) DO UPDATE SET contractId=EXCLUDED.contractId, portfolioId=EXCLUDED.portfolioId, fundId=EXCLUDED.fundId, orderId=EXCLUDED.orderId, tradeId=EXCLUDED.tradeId, tradeType=EXCLUDED.tradeType, generation=EXCLUDED.generation;
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
