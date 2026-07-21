namespace TomasAI.IFM.Domain.Trade.Queries.Model;

internal static class TradeDbCql
{
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
        WHERE TradeId = :tradeId
        """;

    public const string GetTradeTypeLimit = """
        SELECT TradeId AS "TradeId",
               TradeType AS "TradeType",
               MaxLossLimit AS "MaxLossLimit",
               MinProfitLimit AS "MinProfitLimit",
               MaxProfitLimit AS "MaxProfitLimit"
        FROM trade_type_limit
        WHERE TradeId = :tradeId
          AND TradeType = :tradeType
        """;

    public const string GetTradePosition = """
        SELECT orderId AS "OrderId",
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
          AND daysToExpiry = :daysToExpiry
          AND tradeType = :tradeType
        """;

    public const string GetTradePositions = """
        SELECT orderId AS "OrderId",
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
        """;

    public const string GetTradePositionsById = """
        SELECT orderId AS "OrderId",
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
          AND daysToExpiry = :daysToExpiry
        """;

    public const string GetOptionLegs = """
        SELECT orderId AS "OrderId",
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
        WHERE tradeId = :tradeId
        """;
}
