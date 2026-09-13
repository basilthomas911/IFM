namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;

/// <summary>Additive v3 lifecycle projections. Legacy tables remain unchanged and readable.</summary>
public static class TradeFlowSchemaCql
{
    public const string TradeOrder = """
        CREATE TABLE IF NOT EXISTS trade_order_v3 (
            portfolioId int, fundId int, orderId int, revision int,
            status text, valueDate date, updatedAtUtc timestamp,
            definitionHash text, payload blob,
            PRIMARY KEY ((portfolioId, fundId), orderId, revision)
        ) WITH CLUSTERING ORDER BY (orderId DESC, revision DESC);
        """;

    public const string OrderExecution = """
        CREATE TABLE IF NOT EXISTS order_execution_v1 (
            portfolioId int, fundId int, orderId int, executionAttemptId uuid,
            status text, startedAtUtc timestamp, completedAtUtc timestamp, payload blob,
            PRIMARY KEY ((portfolioId, fundId, orderId), executionAttemptId)
        );
        """;

    public const string ExecutionFill = """
        CREATE TABLE IF NOT EXISTS order_execution_fill_v1 (
            executionAttemptId uuid, executionFillId uuid, componentId uuid,
            tradeLegId uuid, marketInstrumentId bigint, filledAtUtc timestamp, payload blob,
            PRIMARY KEY ((executionAttemptId), filledAtUtc, executionFillId)
        ) WITH CLUSTERING ORDER BY (filledAtUtc ASC, executionFillId ASC);
        """;

    public const string ExecutionFillByContract = """
        CREATE TABLE IF NOT EXISTS order_execution_fill_v2 (
            executionAttemptId uuid, executionFillId uuid, componentId uuid,
            tradeLegId uuid, contractId text, filledAtUtc timestamp, payload blob,
            PRIMARY KEY ((executionAttemptId), filledAtUtc, executionFillId)
        ) WITH CLUSTERING ORDER BY (filledAtUtc ASC, executionFillId ASC);
        """;

    public const string EstablishedTrade = """
        CREATE TABLE IF NOT EXISTS established_trade_v1 (
            portfolioId int, fundId int, orderId int, tradeId int,
            assetFamily text, strategyKind text, establishedAtUtc timestamp,
            evidenceRevision int, payload blob,
            PRIMARY KEY ((portfolioId, fundId), orderId, tradeId)
        );
        """;

    public const string EstablishedTradeHistory = """
        CREATE TABLE IF NOT EXISTS established_trade_history_v2 (
            portfolioId int, fundId int, strategyKind text, establishedAtUtc timestamp,
            orderId int, tradeId int, evidenceRevision int, payload blob,
            PRIMARY KEY ((portfolioId, fundId, strategyKind), establishedAtUtc, orderId, tradeId, evidenceRevision)
        ) WITH CLUSTERING ORDER BY (establishedAtUtc DESC, orderId ASC, tradeId ASC, evidenceRevision DESC);
        """;

    public const string PositionCurrent = """
        CREATE TABLE IF NOT EXISTS strategy_position_current_v1 (
            portfolioId int, fundId int, orderId int, tradeId int, positionId uuid,
            strategyKind text, positionSequence bigint, routeGeneration bigint,
            isOpen boolean, asOfUtc timestamp, payload blob,
            PRIMARY KEY ((portfolioId, fundId), orderId, tradeId, positionId)
        );
        """;

    public const string PositionHistory = """
        CREATE TABLE IF NOT EXISTS strategy_position_history_v1 (
            positionId uuid, asOfUtc timestamp, positionSequence bigint, phase text, payload blob,
            PRIMARY KEY ((positionId), asOfUtc, positionSequence)
        ) WITH CLUSTERING ORDER BY (asOfUtc DESC, positionSequence DESC);
        """;

    public const string OpenPositionRoute = """
        CREATE TABLE IF NOT EXISTS open_position_route_v1 (
            marketInstrumentId bigint, portfolioId int, fundId int, orderId int, tradeId int,
            positionId uuid, tradeLegId uuid, strategyKind text, positionActor text,
            positionActorThreadId text, generation bigint,
            PRIMARY KEY ((marketInstrumentId), positionId, tradeLegId)
        );
        """;

    public const string OpenPositionRouteRecovery = """
        CREATE TABLE IF NOT EXISTS open_position_route_recovery_v1 (
            shard tinyint, positionId uuid, tradeLegId uuid, marketInstrumentId bigint,
            portfolioId int, fundId int, orderId int, tradeId int, strategyKind text,
            positionActor text, positionActorThreadId text, generation bigint,
            PRIMARY KEY ((shard), positionId, tradeLegId)
        );
        """;

    public const string OpenPositionRouteByContract = """
        CREATE TABLE IF NOT EXISTS open_position_route_v2 (
            contractId text, portfolioId int, fundId int, orderId int, tradeId int,
            positionId uuid, tradeLegId uuid, tradeType text, generation bigint,
            PRIMARY KEY ((contractId), positionId, tradeLegId)
        );
        """;

    public const string OpenPositionRouteRecoveryByContract = """
        CREATE TABLE IF NOT EXISTS open_position_route_recovery_v2 (
            shard tinyint, positionId uuid, tradeLegId uuid, contractId text,
            portfolioId int, fundId int, orderId int, tradeId int, tradeType text,
            generation bigint,
            PRIMARY KEY ((shard), positionId, tradeLegId)
        );
        """;

}
