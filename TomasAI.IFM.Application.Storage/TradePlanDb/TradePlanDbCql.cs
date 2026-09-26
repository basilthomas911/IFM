namespace TomasAI.IFM.Application.Storage.TradePlanDb;

public static class TradePlanDbCql
{
    public const string SelectExactPlan =
        "SELECT contentHash FROM {0} WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=? AND planRevision=?;";
    public const string InsertPlan =
        "INSERT INTO {0} (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision,calculatedAtUtc,state,requiresExit,contentHash,payload) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);";
    public const string SelectCurrentPlan =
        "SELECT payload FROM {0} WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=? LIMIT 1;";
    public const string SelectPlanHistory =
        "SELECT payload FROM {0} WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=?;";
    public const string InsertActivity =
        "INSERT INTO position_trade_plan_activity_by_date_v1 (valueDate,calculatedAtUtc,portfolioId,fundId,orderId,tradeId,positionId,strategyKind,planRevision,state,requiresExit,contentHash,payload) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?);";
    public const string SelectActivity =
        "SELECT payload FROM position_trade_plan_activity_by_date_v1 WHERE valueDate=?;";
    public const string InsertExitWorkflow =
        "INSERT INTO position_exit_workflow_v1 (portfolioId,fundId,orderId,tradeId,positionId,valueDate,updatedAtUtc,exitDecisionId,stageRevision,strategyKind,state,sourcePlanEventId,payload) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?);";
    public const string SelectExactExitWorkflow =
        "SELECT payload FROM position_exit_workflow_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=? AND updatedAtUtc=? AND exitDecisionId=? AND stageRevision=?;";
    public const string SelectCurrentExitWorkflow =
        "SELECT payload FROM position_exit_workflow_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=? LIMIT 1;";
    public const string SelectExitWorkflowTimeline =
        "SELECT payload FROM position_exit_workflow_v1 WHERE portfolioId=? AND fundId=? AND orderId=? AND tradeId=? AND positionId=? AND valueDate=?;";
}
