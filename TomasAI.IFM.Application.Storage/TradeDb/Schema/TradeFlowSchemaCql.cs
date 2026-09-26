namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;

/// <summary>Defines the authoritative trade-lifecycle projection tables.</summary>
public static class TradeFlowSchemaCql
{
    public const string TradeOrder = """
        CREATE TABLE IF NOT EXISTS trade_order (
            portfolioId int, fundId int, orderId int, revision int,
            status text, valueDate date, updatedAtUtc timestamp,
            definitionHash text, payload blob,
            PRIMARY KEY ((portfolioId, fundId), orderId, revision)
        ) WITH CLUSTERING ORDER BY (orderId DESC, revision DESC);
        """;

    public const string OrderExecution = """
        CREATE TABLE IF NOT EXISTS order_execution (
            portfolioId int, fundId int, orderId int, executionAttemptId uuid,
            status text, startedAtUtc timestamp, completedAtUtc timestamp, payload blob,
            PRIMARY KEY ((portfolioId, fundId, orderId), executionAttemptId)
        );
        """;

    public const string ExecutionFill = """
        CREATE TABLE IF NOT EXISTS order_execution_fill (
            executionAttemptId uuid, executionFillId uuid, componentId uuid,
            tradeLegId uuid, contractId text, filledAtUtc timestamp, payload blob,
            PRIMARY KEY ((executionAttemptId), filledAtUtc, executionFillId)
        ) WITH CLUSTERING ORDER BY (filledAtUtc ASC, executionFillId ASC);
        """;

    public const string EstablishedTrade = """
        CREATE TABLE IF NOT EXISTS established_trade (
            portfolioId int, fundId int, orderId int, tradeId int,
            assetFamily text, strategyKind text, establishedAtUtc timestamp,
            evidenceRevision int, payload blob,
            PRIMARY KEY ((portfolioId, fundId), orderId, tradeId)
        );
        """;

    public const string EstablishedTradeHistory = """
        CREATE TABLE IF NOT EXISTS established_trade_history (
            portfolioId int, fundId int, strategyKind text, establishedAtUtc timestamp,
            orderId int, tradeId int, evidenceRevision int, payload blob,
            PRIMARY KEY ((portfolioId, fundId, strategyKind), establishedAtUtc, orderId, tradeId, evidenceRevision)
        ) WITH CLUSTERING ORDER BY (establishedAtUtc DESC, orderId ASC, tradeId ASC, evidenceRevision DESC);
        """;

    public const string PositionCurrent = """
        CREATE TABLE IF NOT EXISTS strategy_position_current (
            portfolioId int, fundId int, orderId int, tradeId int, positionId uuid,
            strategyKind text, positionSequence bigint, routeGeneration bigint,
            isOpen boolean, asOfUtc timestamp, payload blob,
            PRIMARY KEY ((portfolioId, fundId), orderId, tradeId, positionId)
        );
        """;

    public const string PositionHistory = """
        CREATE TABLE IF NOT EXISTS strategy_position_history (
            positionId uuid, asOfUtc timestamp, positionSequence bigint, phase text, payload blob,
            PRIMARY KEY ((positionId), asOfUtc, positionSequence)
        ) WITH CLUSTERING ORDER BY (asOfUtc DESC, positionSequence DESC);
        """;

    public const string OpenPositionRoute = """
        CREATE TABLE IF NOT EXISTS open_position_route (
            contractId text, portfolioId int, fundId int, orderId int, tradeId int,
            positionId uuid, tradeLegId uuid, tradeType text, generation bigint,
            PRIMARY KEY ((contractId), positionId, tradeLegId)
        );
        """;

    public const string OpenPositionRouteRecovery = """
        CREATE TABLE IF NOT EXISTS open_position_route_recovery (
            shard tinyint, positionId uuid, tradeLegId uuid, contractId text,
            portfolioId int, fundId int, orderId int, tradeId int, tradeType text,
            generation bigint,
            PRIMARY KEY ((shard), positionId, tradeLegId)
        );
        """;

}
