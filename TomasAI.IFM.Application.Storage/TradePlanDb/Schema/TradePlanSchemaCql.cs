namespace TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

public static class TradePlanSchemaCql
{
    public const string IronCondor = """
        CREATE TABLE IF NOT EXISTS iron_condor_trade_plan (
            portfolioId int, fundId int, orderId int, tradeId int, positionId uuid, valueDate date,
            planRevision bigint, calculatedAtUtc timestamp, state text, requiresExit boolean,
            contentHash text, payload blob,
            PRIMARY KEY ((portfolioId,fundId,orderId,tradeId,positionId,valueDate),planRevision)
        ) WITH CLUSTERING ORDER BY (planRevision DESC);
        """;

    public const string VerticalSpread = """
        CREATE TABLE IF NOT EXISTS vertical_spread_trade_plan (
            portfolioId int, fundId int, orderId int, tradeId int, positionId uuid, valueDate date,
            planRevision bigint, calculatedAtUtc timestamp, state text, requiresExit boolean,
            contentHash text, payload blob,
            PRIMARY KEY ((portfolioId,fundId,orderId,tradeId,positionId,valueDate),planRevision)
        ) WITH CLUSTERING ORDER BY (planRevision DESC);
        """;

    public const string Futures = """
        CREATE TABLE IF NOT EXISTS futures_trade_plan (
            portfolioId int, fundId int, orderId int, tradeId int, positionId uuid, valueDate date,
            planRevision bigint, calculatedAtUtc timestamp, state text, requiresExit boolean,
            contentHash text, payload blob,
            PRIMARY KEY ((portfolioId,fundId,orderId,tradeId,positionId,valueDate),planRevision)
        ) WITH CLUSTERING ORDER BY (planRevision DESC);
        """;

    public const string ActivityByDate = """
        CREATE TABLE IF NOT EXISTS position_trade_plan_activity_by_date (
            valueDate date, calculatedAtUtc timestamp, portfolioId int, fundId int, orderId int,
            tradeId int, positionId uuid, strategyKind tinyint, planRevision bigint,
            state tinyint, requiresExit boolean, contentHash text, payload blob,
            PRIMARY KEY ((valueDate),calculatedAtUtc,portfolioId,fundId,orderId,tradeId,positionId,planRevision)
        ) WITH CLUSTERING ORDER BY (calculatedAtUtc DESC);
        """;

    public const string ExitWorkflow = """
        CREATE TABLE IF NOT EXISTS position_exit_workflow (
            portfolioId int, fundId int, orderId int, tradeId int, positionId uuid,
            valueDate date, updatedAtUtc timestamp, exitDecisionId uuid, stageRevision bigint,
            strategyKind tinyint, state tinyint, sourcePlanEventId uuid, payload blob,
            PRIMARY KEY ((portfolioId,fundId,orderId,tradeId,positionId,valueDate),
                updatedAtUtc,exitDecisionId,stageRevision)
        ) WITH CLUSTERING ORDER BY (updatedAtUtc DESC,exitDecisionId DESC,stageRevision DESC);
        """;
}
