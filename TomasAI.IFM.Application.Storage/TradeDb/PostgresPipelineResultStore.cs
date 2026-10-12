using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Pure pipeline materialization from complete accepted source events, using the source owner's transaction.</summary>
public sealed class PostgresPipelineResultStore
{
    /// <summary>Stores the exact market assessment and its profile history atomically.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectAsync(IEnlistedPostgresTransaction db,MarketConditionAssessmentCompletedEvent completed,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(db);
        var r=MarketConditionAssessmentContracts.ReadResult(completed.Result);
        completed.RequireAssessmentProjectionIdentity(r);
        var bytes=MessagePackSerializer.Serialize(completed,cancellationToken:token);var hash=Convert.ToHexString(SHA256.HashData(bytes));
        var saved=await (db.ScalarAsync("""
            INSERT INTO trade.market_condition_assessment(workflow_id,payload,payload_sha256) VALUES($1,$2,$3)
            ON CONFLICT(workflow_id) DO UPDATE SET payload=EXCLUDED.payload
            WHERE trade.market_condition_assessment.payload=EXCLUDED.payload RETURNING workflow_id;
            """,[r.WorkflowId.Value,bytes,hash],token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidDataException("Conflicting accepted market assessment.");
        await (db.ExecuteAsync("""
            INSERT INTO trade.market_condition_assessment_by_profile(market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id,payload,payload_sha256)
            VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT(market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id) DO NOTHING;
            """,[r.MarketProfileId,r.InstrumentRoot,r.TargetHorizon.ToString(),r.EvaluatedAtUtc,r.WorkflowId.Value,bytes,hash],token))
            .ConfigureAwait(false);
    }

    /// <summary>Stores the immutable composer invocation and its exact fund/value-date history atomically.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectAsync(IEnlistedPostgresTransaction db,OrderCompositionFunctionCompletedEvent completed,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(db);
        var r=OrderCompositionContracts.ReadResult(completed.Result);
        if(completed.Id!=r.ResultId || completed.WorkflowId!=r.WorkflowId || completed.EntityId!=r.EntityId
            || completed.InputWorkflowRevision!=r.InputWorkflowRevision || !string.Equals(completed.RequestFingerprint,r.InputSha256,StringComparison.Ordinal))
            throw new InvalidDataException("Composition projection identity mismatch.");
        var bytes=MessagePackBinarySerializer.Shared.Serialize(completed with {EventId=0});
        var saved=await (db.ScalarAsync("""
            INSERT INTO trade.order_composition_invocation(workflow_id,invocation_id,result_hash,input_hash,payload) VALUES($1,$2,$3,$4,$5)
            ON CONFLICT(workflow_id,invocation_id) DO UPDATE SET payload=EXCLUDED.payload
            WHERE trade.order_composition_invocation.payload=EXCLUDED.payload RETURNING invocation_id;
            """,[r.WorkflowId.Value,r.InvocationId,completed.Result.PayloadSha256,r.InputSha256,bytes],token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidDataException("Conflicting accepted composition invocation.");
        await (db.ExecuteAsync("""
            INSERT INTO trade.order_composition_history(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) ON CONFLICT(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id) DO NOTHING;
            """,[r.DecisionContext.PortfolioId,r.DecisionContext.FundId,r.DecisionContext.ValueDate,r.EvaluatedAtUtc,r.InvocationId,
                r.WorkflowId.Value,completed.Id,(short)r.TargetHorizon,(short)r.Outcome,r.Reasons[0],r.ResultId,completed.Result.PayloadSha256],token))
            .ConfigureAwait(false);
    }

    /// <summary>Stores the complete selector decision and history, without rerunning selection or dispatching an order.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectAsync(IEnlistedPostgresTransaction db,TradeSelectionFunctionCompletedEvent completed,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(db);
        var r=TradeSelectionContracts.ReadResult(completed.Result);
        TradeSelectionContracts.Require(completed.WorkflowId==r.WorkflowId && completed.CommandId==r.InvocationId && completed.Id==r.ResultId
            && completed.EntityId==r.EntityId && completed.InputWorkflowRevision==r.InputWorkflowRevision && completed.RequestFingerprint.Length==64,
            "TS.RESULT.INVALID","Projection event identity mismatch.");
        var selected=r.SelectedCandidate;var binding=r.DecisionContext.SelectionBinding;
        var bytes=MessagePackBinarySerializer.Shared.Serialize(completed with {EventId=0});
        var saved=await (db.ScalarAsync("""
            INSERT INTO trade.trade_selection_invocation_event(workflow_id,invocation_id,source_sequence,event_id,portfolio_id,fund_id,target_horizon,lifecycle_status,outcome,occurred_at_utc,reason_code,
            selected_deployment_id,selected_deployment_version,selected_strategy_id,selected_strategy_version,selected_structure_id,selected_structure_version,selected_variant_id,selected_variant_version,
            candidate_set_sha256,binding_sha256,parameter_set_id,parameter_version,parameter_sha256,result_id,result_sha256,result_payload,event_payload)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28)
            ON CONFLICT(workflow_id,invocation_id,source_sequence) DO UPDATE SET event_payload=EXCLUDED.event_payload
            WHERE trade.trade_selection_invocation_event.event_payload=EXCLUDED.event_payload RETURNING invocation_id;
            """,[r.WorkflowId.Value,r.InvocationId,1L,completed.Id,r.PortfolioId,r.FundId,(short)r.DecisionHorizon,(short)2,(short)r.Outcome,r.ProducedAtUtc,r.PrimaryReasonCode,
                selected?.DeploymentKey.Id,selected?.DeploymentKey.Version,selected?.StrategyKey.Id,selected?.StrategyKey.Version,
                selected?.StructureKey.Id,selected?.StructureKey.Version,selected?.VariantKey.Id,selected?.VariantKey.Version,
                TradeSelectionContracts.WireHash(binding.Candidates.Select(x=>x.CandidateHash).ToArray()),binding.PayloadSha256,binding.CommonPolicy.Id,
                binding.CommonPolicy.Version,binding.CommonPolicy.PayloadSha256,r.ResultId,completed.Result.PayloadSha256,MessagePackBinarySerializer.Shared.Serialize(r),bytes],token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidDataException("Conflicting accepted selection invocation.");
        await (db.ExecuteAsync("""
            INSERT INTO trade.trade_selection_history_by_fund_date(portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12) ON CONFLICT(portfolio_id,fund_id,value_date,occurred_at_utc,workflow_id,invocation_id) DO NOTHING;
            """,[r.PortfolioId,r.FundId,DateOnly.FromDateTime(r.ProducedAtUtc),r.ProducedAtUtc,r.WorkflowId.Value,r.InvocationId,completed.Id,
                (short)r.DecisionHorizon,(short)r.Outcome,r.PrimaryReasonCode,r.ResultId,completed.Result.PayloadSha256],token))
            .ConfigureAwait(false);
    }
}
