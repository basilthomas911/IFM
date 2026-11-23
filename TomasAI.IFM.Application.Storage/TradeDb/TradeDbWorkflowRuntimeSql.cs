namespace TomasAI.IFM.Application.Storage.TradeDb;

public static class TradeDbWorkflowRuntimeSql
{
    // Shared only by source writes and their outcome reconciliation, outside decision execution.
    public const string LockDecisionStream = "SELECT pg_advisory_xact_lock(hashtextextended($1, 711371));";
    public const string ReadDecisionOutcome = "SELECT statepayload FROM trade.intrinsic_time_strategy_workflow WHERE workflowid=$1;";
    public const string ReadActiveDecisionRecoveryPage = """
        SELECT workflowid,statepayload FROM trade.intrinsic_time_strategy_workflow
        WHERE status='Running' AND workflowid>$1 ORDER BY workflowid LIMIT 64;
        """;
}
