using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb.Schema;

public sealed class TradePlanSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<TradePlanSchemaDb>(
        settings[TradePlanDbContext.TradePlanDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("iron_condor_trade_plan", TradePlanSchemaCql.IronCondor, "DROP TABLE IF EXISTS iron_condor_trade_plan;"),
        new("iron_condor_trade_plan_sequenceId", "ALTER TABLE iron_condor_trade_plan ADD sequenceId bigint;", "ALTER TABLE iron_condor_trade_plan DROP sequenceId;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_actionDate", "ALTER TABLE iron_condor_trade_plan ADD actionDate timestamp;", "ALTER TABLE iron_condor_trade_plan DROP actionDate;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_tradeDate", "ALTER TABLE iron_condor_trade_plan ADD tradeDate date;", "ALTER TABLE iron_condor_trade_plan DROP tradeDate;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_maturityDate", "ALTER TABLE iron_condor_trade_plan ADD maturityDate date;", "ALTER TABLE iron_condor_trade_plan DROP maturityDate;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_tradeType", "ALTER TABLE iron_condor_trade_plan ADD tradeType text;", "ALTER TABLE iron_condor_trade_plan DROP tradeType;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_actionType", "ALTER TABLE iron_condor_trade_plan ADD actionType text;", "ALTER TABLE iron_condor_trade_plan DROP actionType;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_actionSubType", "ALTER TABLE iron_condor_trade_plan ADD actionSubType text;", "ALTER TABLE iron_condor_trade_plan DROP actionSubType;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_actionState", "ALTER TABLE iron_condor_trade_plan ADD actionState text;", "ALTER TABLE iron_condor_trade_plan DROP actionState;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_actionReason", "ALTER TABLE iron_condor_trade_plan ADD actionReason text;", "ALTER TABLE iron_condor_trade_plan DROP actionReason;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_tradePnl", "ALTER TABLE iron_condor_trade_plan ADD tradePnl decimal;", "ALTER TABLE iron_condor_trade_plan DROP tradePnl;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_forwardLossRatio", "ALTER TABLE iron_condor_trade_plan ADD forwardLossRatio double;", "ALTER TABLE iron_condor_trade_plan DROP forwardLossRatio;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_lossProbability", "ALTER TABLE iron_condor_trade_plan ADD lossProbability double;", "ALTER TABLE iron_condor_trade_plan DROP lossProbability;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_mScore", "ALTER TABLE iron_condor_trade_plan ADD mScore double;", "ALTER TABLE iron_condor_trade_plan DROP mScore;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_maxProfit", "ALTER TABLE iron_condor_trade_plan ADD maxProfit decimal;", "ALTER TABLE iron_condor_trade_plan DROP maxProfit;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_maxLoss", "ALTER TABLE iron_condor_trade_plan ADD maxLoss decimal;", "ALTER TABLE iron_condor_trade_plan DROP maxLoss;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_minProfitTarget", "ALTER TABLE iron_condor_trade_plan ADD minProfitTarget decimal;", "ALTER TABLE iron_condor_trade_plan DROP minProfitTarget;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_dailyProfitTarget", "ALTER TABLE iron_condor_trade_plan ADD dailyProfitTarget decimal;", "ALTER TABLE iron_condor_trade_plan DROP dailyProfitTarget;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_assetPrice", "ALTER TABLE iron_condor_trade_plan ADD assetPrice decimal;", "ALTER TABLE iron_condor_trade_plan DROP assetPrice;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_assetStdDev", "ALTER TABLE iron_condor_trade_plan ADD assetStdDev double;", "ALTER TABLE iron_condor_trade_plan DROP assetStdDev;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_assetMean", "ALTER TABLE iron_condor_trade_plan ADD assetMean double;", "ALTER TABLE iron_condor_trade_plan DROP assetMean;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_assetPriceChange", "ALTER TABLE iron_condor_trade_plan ADD assetPriceChange double;", "ALTER TABLE iron_condor_trade_plan DROP assetPriceChange;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_marketTrend", "ALTER TABLE iron_condor_trade_plan ADD marketTrend text;", "ALTER TABLE iron_condor_trade_plan DROP marketTrend;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_marketVolatility", "ALTER TABLE iron_condor_trade_plan ADD marketVolatility text;", "ALTER TABLE iron_condor_trade_plan DROP marketVolatility;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_marketDirection", "ALTER TABLE iron_condor_trade_plan ADD marketDirection text;", "ALTER TABLE iron_condor_trade_plan DROP marketDirection;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_vixVolatility", "ALTER TABLE iron_condor_trade_plan ADD vixVolatility text;", "ALTER TABLE iron_condor_trade_plan DROP vixVolatility;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_tradeRisk", "ALTER TABLE iron_condor_trade_plan ADD tradeRisk text;", "ALTER TABLE iron_condor_trade_plan DROP tradeRisk;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_fiftyDayMA", "ALTER TABLE iron_condor_trade_plan ADD fiftyDayMA double;", "ALTER TABLE iron_condor_trade_plan DROP fiftyDayMA;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_fiveDayXMA", "ALTER TABLE iron_condor_trade_plan ADD fiveDayXMA double;", "ALTER TABLE iron_condor_trade_plan DROP fiveDayXMA;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_putOTMProbability", "ALTER TABLE iron_condor_trade_plan ADD putOTMProbability double;", "ALTER TABLE iron_condor_trade_plan DROP putOTMProbability;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_callOTMProbability", "ALTER TABLE iron_condor_trade_plan ADD callOTMProbability double;", "ALTER TABLE iron_condor_trade_plan DROP callOTMProbability;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_shortPutGamma", "ALTER TABLE iron_condor_trade_plan ADD shortPutGamma double;", "ALTER TABLE iron_condor_trade_plan DROP shortPutGamma;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_shortCallGamma", "ALTER TABLE iron_condor_trade_plan ADD shortCallGamma double;", "ALTER TABLE iron_condor_trade_plan DROP shortCallGamma;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_gammaRisk", "ALTER TABLE iron_condor_trade_plan ADD gammaRisk text;", "ALTER TABLE iron_condor_trade_plan DROP gammaRisk;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_netPrice", "ALTER TABLE iron_condor_trade_plan ADD netPrice decimal;", "ALTER TABLE iron_condor_trade_plan DROP netPrice;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_forwardPrice", "ALTER TABLE iron_condor_trade_plan ADD forwardPrice decimal;", "ALTER TABLE iron_condor_trade_plan DROP forwardPrice;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_forwardDelta", "ALTER TABLE iron_condor_trade_plan ADD forwardDelta double;", "ALTER TABLE iron_condor_trade_plan DROP forwardDelta;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_stopLossLimit", "ALTER TABLE iron_condor_trade_plan ADD stopLossLimit double;", "ALTER TABLE iron_condor_trade_plan DROP stopLossLimit;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_trendType", "ALTER TABLE iron_condor_trade_plan ADD trendType text;", "ALTER TABLE iron_condor_trade_plan DROP trendType;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_trendStrength", "ALTER TABLE iron_condor_trade_plan ADD trendStrength text;", "ALTER TABLE iron_condor_trade_plan DROP trendStrength;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_rsi", "ALTER TABLE iron_condor_trade_plan ADD rsi double;", "ALTER TABLE iron_condor_trade_plan DROP rsi;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_rsiSlope", "ALTER TABLE iron_condor_trade_plan ADD rsiSlope double;", "ALTER TABLE iron_condor_trade_plan DROP rsiSlope;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_tdi", "ALTER TABLE iron_condor_trade_plan ADD tdi text;", "ALTER TABLE iron_condor_trade_plan DROP tdi;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_tdiStrength", "ALTER TABLE iron_condor_trade_plan ADD tdiStrength text;", "ALTER TABLE iron_condor_trade_plan DROP tdiStrength;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_createdOn", "ALTER TABLE iron_condor_trade_plan ADD createdOn timestamp;", "ALTER TABLE iron_condor_trade_plan DROP createdOn;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_createdBy", "ALTER TABLE iron_condor_trade_plan ADD createdBy text;", "ALTER TABLE iron_condor_trade_plan DROP createdBy;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_sourceEventId", "ALTER TABLE iron_condor_trade_plan ADD sourceEventId uuid;", "ALTER TABLE iron_condor_trade_plan DROP sourceEventId;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_positionSequence", "ALTER TABLE iron_condor_trade_plan ADD positionSequence bigint;", "ALTER TABLE iron_condor_trade_plan DROP positionSequence;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_routeGeneration", "ALTER TABLE iron_condor_trade_plan ADD routeGeneration bigint;", "ALTER TABLE iron_condor_trade_plan DROP routeGeneration;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_calculationVersion", "ALTER TABLE iron_condor_trade_plan ADD calculationVersion text;", "ALTER TABLE iron_condor_trade_plan DROP calculationVersion;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_inputStatus", "ALTER TABLE iron_condor_trade_plan ADD inputStatus text;", "ALTER TABLE iron_condor_trade_plan DROP inputStatus;", ["conflicts with an existing column", "already exists"]),
        new("iron_condor_trade_plan_snapshotPayload", "ALTER TABLE iron_condor_trade_plan ADD snapshotPayload blob;", "ALTER TABLE iron_condor_trade_plan DROP snapshotPayload;", ["conflicts with an existing column", "already exists"]),
        new("vertical_spread_trade_plan", TradePlanSchemaCql.VerticalSpread, "DROP TABLE IF EXISTS vertical_spread_trade_plan;"),
        new("futures_trade_plan", TradePlanSchemaCql.Futures, "DROP TABLE IF EXISTS futures_trade_plan;"),
        new("position_trade_plan_activity_by_date", TradePlanSchemaCql.ActivityByDate,
            "DROP TABLE IF EXISTS position_trade_plan_activity_by_date;"),
        new("position_exit_workflow", TradePlanSchemaCql.ExitWorkflow,
            "DROP TABLE IF EXISTS position_exit_workflow;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
