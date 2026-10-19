namespace TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

/// <summary>PostgreSQL trade-plan schema, including scalar risk fields and ordered history indexes.</summary>
public static class TradePlanSchemaSql
{
    public const string Create = """
CREATE SCHEMA IF NOT EXISTS trade_plan;
CREATE TABLE IF NOT EXISTS trade_plan.iron_condor_trade_plan (
    portfolioId integer,
    fundId integer,
    orderId integer,
    tradeId integer,
    positionId uuid,
    valueDate date,
    planRevision bigint,
    calculatedAtUtc timestamptz,
    state text,
    requiresExit boolean,
    contentHash text,
    payload bytea,
    PRIMARY KEY (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision)
);
CREATE TABLE IF NOT EXISTS trade_plan.vertical_spread_trade_plan (
    portfolioId integer,
    fundId integer,
    orderId integer,
    tradeId integer,
    positionId uuid,
    valueDate date,
    planRevision bigint,
    calculatedAtUtc timestamptz,
    state text,
    requiresExit boolean,
    contentHash text,
    payload bytea,
    PRIMARY KEY (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision)
);
CREATE TABLE IF NOT EXISTS trade_plan.futures_trade_plan (
    portfolioId integer,
    fundId integer,
    orderId integer,
    tradeId integer,
    positionId uuid,
    valueDate date,
    planRevision bigint,
    calculatedAtUtc timestamptz,
    state text,
    requiresExit boolean,
    contentHash text,
    payload bytea,
    PRIMARY KEY (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision)
);
CREATE TABLE IF NOT EXISTS trade_plan.position_trade_plan_activity_by_date (
    valueDate date,
    calculatedAtUtc timestamptz,
    portfolioId integer,
    fundId integer,
    orderId integer,
    tradeId integer,
    positionId uuid,
    strategyKind smallint,
    planRevision bigint,
    state smallint,
    requiresExit boolean,
    contentHash text,
    payload bytea,
    PRIMARY KEY (valueDate,calculatedAtUtc,portfolioId,fundId,orderId,tradeId,positionId,planRevision)
);
CREATE TABLE IF NOT EXISTS trade_plan.position_exit_workflow (
    portfolioId integer,
    fundId integer,
    orderId integer,
    tradeId integer,
    positionId uuid,
    valueDate date,
    updatedAtUtc timestamptz,
    exitDecisionId uuid,
    stageRevision bigint,
    strategyKind smallint,
    state smallint,
    sourcePlanEventId uuid,
    payload bytea,
    PRIMARY KEY (portfolioId,fundId,orderId,tradeId,positionId,valueDate,updatedAtUtc,exitDecisionId,stageRevision)
);
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS sequenceId bigint;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS actionDate timestamptz;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS tradeDate date;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS maturityDate date;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS tradeType text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS actionType text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS actionSubType text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS actionState text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS actionReason text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS tradePnl numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardLossRatio double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS lossProbability double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS mScore double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS maxProfit numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS maxLoss numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS minProfitTarget numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS dailyProfitTarget numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS assetPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS assetStdDev double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS assetMean double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS assetPriceChange double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS marketTrend text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS marketVolatility text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS marketDirection text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS vixVolatility text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS volatilityPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS tradeRisk text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS fiftyDayMA double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS fiveDayXMA double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS putOTMProbability double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS callOTMProbability double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS shortPutGamma double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS shortCallGamma double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS gammaRisk text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS netPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardDelta double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS stopLossLimit double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS trendType text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS trendStrength text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS rsi double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS rsiSlope double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS tdi text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS tdiStrength text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS createdOn timestamptz;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS createdBy text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS sourceEventId uuid;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS positionSequence bigint;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS routeGeneration bigint;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS calculationVersion text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS inputStatus text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS snapshotPayload bytea;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS actionDateTime timestamptz;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS dailyPnl numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS dailyLoss numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS dailyLossLimit numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS lossHeadroom numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardDailyPnl numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardLoss numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardLossHeadroom numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS projectedPositionValueChange numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS estimatedExitCommission numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS estimatedExitSlippage numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS estimatedExitCosts numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS putSpreadPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS callSpreadPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS combinedSpreadPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS putForwardPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS callForwardPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS combinedForwardPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS currentPositionValue numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS forwardPositionValue numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS estimatedCloseValue numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS netDelta double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS netGamma double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS netVega double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS netTheta double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS underlyingPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS distanceToShortPut numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS distanceToShortCall numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS timeToExpiryDays double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS scenarioHorizonSeconds bigint;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS scenarioUnderlyingPrice numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS scenarioUnderlyingMove numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS scenarioVolatilityShift double precision;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS scenarioKind text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS pricingModelVersion text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS oldestQuoteAtUtc timestamptz;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS riskValidUntilUtc timestamptz;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS calculationStatus text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS exitRecommended boolean;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS exitReason text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS quantity integer;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS contractMultiplier numeric;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS currency text;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS riskParameterSetId uuid;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS riskParameterSetVersion integer;
ALTER TABLE trade_plan.iron_condor_trade_plan ADD COLUMN IF NOT EXISTS riskParameterSetHash text;
CREATE INDEX IF NOT EXISTS position_trade_plan_activity_recent ON trade_plan.position_trade_plan_activity_by_date (valueDate,calculatedAtUtc DESC,portfolioId DESC,fundId DESC,orderId DESC,tradeId DESC,positionId DESC,planRevision DESC);
""";
    /// <summary>Explicit schema removal for administrative teardown.</summary>
    public const string Drop = "DROP SCHEMA IF EXISTS trade_plan CASCADE;";
}
