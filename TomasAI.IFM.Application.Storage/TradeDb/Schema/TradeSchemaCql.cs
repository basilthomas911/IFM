namespace TomasAI.IFM.Application.Storage.TradeDb.Schema;

internal static class TradeSchemaCql
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

    public const string CreateTradePlanForwardLossRatioTable = """
    create table if not exists trade_plan_forward_loss_ratio(
    partitionId int,
    valueDate date,
    forwardLossRatio double,
    sequenceId bigint,
    primary key (partitionId, valueDate, forwardLossRatio, sequenceId)
    ) with clustering order by (valueDate desc, forwardLossRatio asc, sequenceId desc)
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

    public const string CreateIntrinsicTimeStrategyWorkflowTable = """
    CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow (
        workflowId uuid PRIMARY KEY,
        workflowEntityId text,
        workflowDefinitionId text,
        workflowDefinitionVersion int,
        contractId text,
        timeFrameStartValueDate date,
        timePeriod text,
        triggerEventId uuid,
        correlationId uuid,
        status text,
        outcome text,
        currentStage text,
        workflowRevision bigint,
        lastEventId bigint,
        stateSchemaVersion int,
        statePayload blob,
        stopReasonCode text,
        startedAtUtc timestamp,
        terminalAtUtc timestamp,
        updatedAtUtc timestamp
    );
    """;

    public const string CreateIntrinsicTimeStrategyWorkflowActiveByEntityTable = """
    CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_active (
        workflowEntityId text PRIMARY KEY,
        workflowId uuid,
        contractId text,
        timeFrameStartValueDate date,
        timePeriod text,
        currentStage text,
        workflowRevision bigint,
        lastEventId bigint,
        stateSchemaVersion int,
        statePayload blob,
        startedAtUtc timestamp,
        updatedAtUtc timestamp
    );
    """;

    public const string CreateIntrinsicTimeStrategyWorkflowStartAttemptByEntityTable = """
    CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_start_attempt (
        workflowEntityId text,
        requestedAtUtc timestamp,
        requestedWorkflowId uuid,
        decision text,
        activeWorkflowId uuid,
        startCommandId uuid,
        triggerEventId uuid,
        activeStage text,
        reasonCode text,
        sourceEventId bigint,
        PRIMARY KEY (workflowEntityId, requestedAtUtc, requestedWorkflowId)
    ) WITH CLUSTERING ORDER BY (requestedAtUtc DESC, requestedWorkflowId DESC);
    """;

    public const string CreateIntrinsicTimeStrategyWorkflowTimelineTable = """
    CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_timeline (
        workflowId uuid,
        eventId bigint,
        workflowEntityId text,
        workflowRevision bigint,
        stage text,
        eventName text,
        eventSchemaVersion int,
        eventPayload blob,
        occurredAtUtc timestamp,
        PRIMARY KEY (workflowId, eventId)
    ) WITH CLUSTERING ORDER BY (eventId ASC);
    """;

    public const string CreateIntrinsicTimeStrategyWorkflowByEntityTable = """
    CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_by_entity (
        workflowEntityId text,
        startedAtUtc timestamp,
        workflowId uuid,
        status text,
        outcome text,
        currentStage text,
        workflowRevision bigint,
        terminalAtUtc timestamp,
        stopReasonCode text,
        PRIMARY KEY (workflowEntityId, startedAtUtc, workflowId)
    ) WITH CLUSTERING ORDER BY (startedAtUtc DESC, workflowId DESC);
    """;

    public const string CreateIntrinsicTimeStrategyWorkflowByStatusDayTable = """
    CREATE TABLE IF NOT EXISTS intrinsic_time_strategy_workflow_by_status_day (
        status text,
        startedDate date,
        startedAtUtc timestamp,
        workflowId uuid,
        workflowEntityId text,
        outcome text,
        currentStage text,
        workflowRevision bigint,
        terminalAtUtc timestamp,
        stopReasonCode text,
        PRIMARY KEY ((status, startedDate), startedAtUtc, workflowId)
    ) WITH CLUSTERING ORDER BY (startedAtUtc DESC, workflowId DESC);
    """;
}
