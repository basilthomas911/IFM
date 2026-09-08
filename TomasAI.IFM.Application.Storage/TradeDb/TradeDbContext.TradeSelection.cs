using TomasAI.IFM.Framework.Serialization;
using MessagePack;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial interface ITradeDbContext
{
    Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default);
    Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default);
    Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default);
}
public partial class TradeDbContext
{
    public async Task UpsertTradeSelectionAsync(TradeSelectionFunctionCompletedEvent completed, CancellationToken cancellationToken = default)
    {
        var r = TradeSelectionContracts.ReadResult(completed.Result);
        TradeSelectionContracts.Require(completed.WorkflowId == r.WorkflowId && completed.CommandId == r.InvocationId && completed.Id == r.ResultId && completed.EntityId == r.EntityId
            && completed.InputWorkflowRevision == r.InputWorkflowRevision && completed.RequestFingerprint.Length == 64, "TS.RESULT.INVALID", "Projection event identity mismatch.");
        var selected = r.SelectedCandidate;
        var binding = r.DecisionContext.SelectionBinding;
        var bytes = MessagePackBinarySerializer.Shared.Serialize(completed with { EventId = 0 });
        object[] values = [r.WorkflowId.Value,r.InvocationId,1L,completed.Id,r.PortfolioId,r.FundId,(short)r.DecisionHorizon,(sbyte)2,(sbyte)r.Outcome,r.ProducedAtUtc,r.PrimaryReasonCode,
            selected?.DeploymentKey.Id!,selected?.DeploymentKey.Version!,selected?.StrategyKey.Id!,selected?.StrategyKey.Version!,selected?.StructureKey.Id!,selected?.StructureKey.Version!,selected?.VariantKey.Id!,selected?.VariantKey.Version!,
            TradeSelectionContracts.WireHash(binding.Candidates.Select(x=>x.CandidateHash).ToArray()),binding.PayloadSha256,binding.CommonPolicy.Id,binding.CommonPolicy.Version,binding.CommonPolicy.PayloadSha256,r.ResultId,completed.Result.PayloadSha256,MessagePackBinarySerializer.Shared.Serialize(r)!,bytes];
        var inserted = await _dbFactory.TradeDb.Use("TradeSelection.Insert", "INSERT INTO trade_selection_invocation_event (workflow_id,invocation_id,source_sequence,event_id,portfolio_id,fund_id,target_horizon,lifecycle_status,outcome,occurred_at_utc,reason_code,selected_deployment_id,selected_deployment_version,selected_strategy_id,selected_strategy_version,selected_structure_id,selected_structure_version,selected_variant_id,selected_variant_version,candidate_set_sha256,binding_sha256,parameter_set_id,parameter_version,parameter_sha256,result_id,result_sha256,result_payload,event_payload) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new SelectionValues(values)).ExecuteSingleAsync(row => row.GetBool(0), cancellationToken).ConfigureAwait(false);
        if (!inserted)
        {
            var existing = await GetTradeSelectionInvocationAsync(r.WorkflowId, r.InvocationId, cancellationToken).ConfigureAwait(false);
            if (existing is null || !TradeSelectionContracts.SameCompletion(existing, completed))
                throw new InvalidOperationException("Conflicting selector projection for the same invocation.");
        }
        await _dbFactory.TradeDb.Use("TradeSelection.HistoryInsert", "INSERT INTO trade_selection_history_by_fund_date (portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256) VALUES (?,?,?,?,?,?,?,?,?,?,?,?);")
            .SetParameters(new SelectionValues([r.PortfolioId, r.FundId, DateOnly.FromDateTime(r.ProducedAtUtc), r.ProducedAtUtc, r.WorkflowId.Value, r.InvocationId, completed.Id, (short)r.DecisionHorizon, (sbyte)r.Outcome, r.PrimaryReasonCode, r.ResultId, completed.Result.PayloadSha256]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    public Task<TradeSelectionFunctionCompletedEvent?> GetTradeSelectionInvocationAsync(StrategyWorkflowId workflowId, Guid invocationId, CancellationToken cancellationToken = default)
    {
        if (workflowId.Value == Guid.Empty || invocationId == Guid.Empty) throw new ArgumentException("Exact workflow and invocation identities are required.");
        return _dbFactory.TradeDb.Use("TradeSelection.Exact", "SELECT event_payload,result_sha256 FROM trade_selection_invocation_event WHERE workflow_id=? AND invocation_id=? AND source_sequence=1;")
            .SetParameters(new SelectionValues([workflowId.Value, invocationId])).ExecuteSingleAsync(row =>
            {
                var completed = MessagePackBinarySerializer.Shared.Deserialize<TradeSelectionFunctionCompletedEvent>(row.GetBytes(0));
                var r = TradeSelectionContracts.ReadResult(completed.Result);
                if (r.WorkflowId != workflowId || r.InvocationId != invocationId || completed.Result.PayloadSha256 != row.GetString(1)) throw new InvalidOperationException("Stored selector evidence identity/hash mismatch.");
                return completed;
            }, cancellationToken);
    }
    public Task<QueryPage<TradeSelectionHistoryRow>> GetTradeSelectionHistoryAsync(int portfolioId, int fundId, DateOnly valueDate, int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        if (portfolioId <= 0 || fundId <= 0 || valueDate == default || pageSize is < 1 or > 200) throw new ArgumentException("Invalid history scope or page size.");
        return _dbFactory.TradeDb.Use("TradeSelection.HistoryPage", "SELECT occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256 FROM trade_selection_history_by_fund_date WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new SelectionValues([portfolioId, fundId, valueDate])).ExecutePageAsync(row => new TradeSelectionHistoryRow(portfolioId, fundId, valueDate, row.GetDateTime(0), row.GetGuid(1), row.GetGuid(2), row.GetGuid(3), row.GetShort(4), (byte)row.GetEnum<SelectionOutcome>(5), row.GetString(6), row.GetGuid(7), row.GetString(8)), pageSize, pagingState, cancellationToken);
    }
    readonly record struct SelectionValues(object[] Values) : IBindValue { public object Bind() => Values; }
}
