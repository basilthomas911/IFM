using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial interface ITradeDbContext
{
    Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default);
    Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default);
    Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate,
        int pageSize, byte[]? pagingState = null, CancellationToken token = default);
}

public partial class TradeDbContext
{
    public async Task UpsertOrderCompositionAsync(OrderCompositionFunctionCompletedEvent completed, CancellationToken token = default)
    {
        var result = OrderCompositionContracts.ReadResult(completed.Result);
        if (completed.Id != result.ResultId || completed.WorkflowId != result.WorkflowId || completed.EntityId != result.EntityId
            || completed.InputWorkflowRevision != result.InputWorkflowRevision || completed.RequestFingerprint != result.InputSha256)
            throw new InvalidDataException("Composition projection identity mismatch.");
        var body = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
        var inserted = await _dbFactory.TradeDb.Use("OrderComposition.Insert", "INSERT INTO order_composition_invocation (workflow_id,invocation_id,result_hash,input_hash,payload) VALUES (?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new CompositionValues([result.WorkflowId.Value, result.InvocationId, completed.Result.PayloadSha256, result.InputSha256, body]))
            .ExecuteSingleAsync(row => row.GetBool(0), token).ConfigureAwait(false);
        if (!inserted)
        {
            var old = await GetOrderCompositionInvocationAsync(result.WorkflowId, result.InvocationId, token).ConfigureAwait(false);
            if (old is null || !OrderCompositionContracts.SameCompletion(old, completed))
                throw new InvalidOperationException("Conflicting composition invocation.");
        }
        // The typed decision retains exact authority even when no candidate is available.
        int portfolioId = result.DecisionContext.PortfolioId;
        int fundId = result.DecisionContext.FundId;
        await _dbFactory.TradeDb.Use("OrderComposition.HistoryInsert", "INSERT INTO order_composition_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
            .SetParameters(new CompositionValues([portfolioId, fundId, result.DecisionContext.ValueDate, result.EvaluatedAtUtc,
                result.InvocationId, result.WorkflowId.Value, completed.Id, (short)result.TargetHorizon, (sbyte)result.Outcome,
                result.Reasons[0], result.ResultId, completed.Result.PayloadSha256])).ExecuteCommandAsync(token).ConfigureAwait(false);
    }
    public Task<OrderCompositionFunctionCompletedEvent?> GetOrderCompositionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken token = default)
    {
        if (workflowId.Value == Guid.Empty || invocationId == Guid.Empty) throw new ArgumentException("Exact workflow and invocation required.");
        return _dbFactory.TradeDb.Use("OrderComposition.Exact", "SELECT payload,result_hash,input_hash FROM order_composition_invocation WHERE workflow_id=? AND invocation_id=?;")
            .SetParameters(new CompositionValues([workflowId.Value, invocationId])).ExecuteSingleAsync(row =>
            {
                var completed = MessagePackBinarySerializer.Shared.Deserialize<OrderCompositionFunctionCompletedEvent>(row.GetBytes(0));
                var result = OrderCompositionContracts.ReadResult(completed.Result);
                if (result.WorkflowId != workflowId || result.InvocationId != invocationId || completed.Result.PayloadSha256 != row.GetString(1)
                    || result.InputSha256 != row.GetString(2)) throw new InvalidDataException("Stored composition evidence mismatch.");
                return completed;
            }, token);
    }
    public Task<QueryPage<OrderCompositionHistoryRow>> GetOrderCompositionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate,
        int pageSize, byte[]? pagingState = null, CancellationToken token = default)
    {
        if (portfolioId <= 0 || fundId <= 0 || valueDate == default || pageSize is < 1 or > 100) throw new ArgumentException("Invalid composition history scope.");
        return _dbFactory.TradeDb.Use("OrderComposition.History", "SELECT evaluated_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash FROM order_composition_history WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new CompositionValues([portfolioId, fundId, valueDate])).ExecutePageAsync(row => new OrderCompositionHistoryRow(portfolioId, fundId,
                valueDate, row.GetDateTime(0), row.GetGuid(1), row.GetGuid(2), row.GetGuid(3), row.GetShort(4),
                (byte)row.GetEnum<CompositionOutcome>(5), row.GetString(6), row.GetGuid(7), row.GetString(8)), pageSize, pagingState, token);
    }
    readonly record struct CompositionValues(object[] Values) : IBindValue { public object Bind() => Values; }
}
