using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Materializes recorded workflow and risk-history facts on the source event transaction without actor calls.</summary>
public static class PostgresWorkflowSnapshotStore
{
    /// <summary>Writes the complete workflow read unit, including risk history when the accepted snapshot contains it.</summary>
    public static async Task ProjectAsync(IEnlistedPostgresTransaction transaction, WorkflowStrategyStateUpdatedEvent source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        var state = source.State;
        if (state.EntityId != source.EntityId || state.WorkflowId != source.WorkflowId || state.WorkflowRevision != source.WorkflowRevision || source.EventId <= 0)
            throw new ArgumentException("Committed workflow source identity and state must match.", nameof(source));
        var payload = MessagePackSerializer.Serialize(state, cancellationToken: token);
        await WriteDetailAsync(transaction, source, payload, token)
            .ConfigureAwait(false);
        await WriteHistoryAsync(transaction, source, token)
            .ConfigureAwait(false);
        await WriteTimelineAsync(transaction, source, token)
            .ConfigureAwait(false);
        await WriteActiveAsync(transaction, source, payload, token)
            .ConfigureAwait(false);
        await WriteRiskAsync(transaction, source, token)
            .ConfigureAwait(false);
    }

    /// <summary>Builds the hot active read value solely from the accepted workflow state.</summary>
    public static ActiveIntrinsicTimeStrategyWorkflowReadModel? Active(WorkflowStrategyStateUpdatedEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = source.State;
        var iti = state.EntityId.ItiSignalEntityId;
        return state.Status == WorkflowStrategyMachineStatus.Started
            ? new(state.EntityId.Format(), state.WorkflowId, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod, state.CurrentStage,
                state.WorkflowRevision, source.EventId, 2, MessagePackSerializer.Serialize(state), state.StartedAtUtc, state.UpdatedAtUtc)
            : null;
    }

    static Task<int> WriteDetailAsync(IEnlistedPostgresTransaction db, WorkflowStrategyStateUpdatedEvent source, byte[] payload, CancellationToken token)
    {
        var state = source.State;
        var entity = state.EntityId;
        var iti = entity.ItiSignalEntityId;
        return UpsertAsync(db, "intrinsic_time_strategy_workflow", "workflowId,workflowEntityId,workflowDefinitionId,workflowDefinitionVersion,contractId,timeFrameStartValueDate,timePeriod,triggerEventId,correlationId,status,outcome,currentStage,workflowRevision,lastEventId,stateSchemaVersion,statePayload,stopReasonCode,startedAtUtc,terminalAtUtc,updatedAtUtc", "workflowId",
            [state.WorkflowId.Value, entity.Format(), entity.WorkflowDefinitionId, state.WorkflowDefinitionVersion, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod.ToString(), state.TriggerEventId, state.CorrelationId, Status(state).ToString(), Outcome(state).ToString(), state.CurrentStage.ToString(), state.WorkflowRevision, source.EventId, 2, payload, state.StopReasonCode, state.StartedAtUtc, state.TerminalAtUtc, state.UpdatedAtUtc], token, "lastEventId");
    }

    static async Task WriteHistoryAsync(IEnlistedPostgresTransaction db, WorkflowStrategyStateUpdatedEvent source, CancellationToken token)
    {
        var state = source.State;
        var status = Status(state).ToString();
        var outcome = Outcome(state).ToString();
        await UpsertAsync(db, "intrinsic_time_strategy_workflow_by_entity", "workflowEntityId,startedAtUtc,workflowId,status,outcome,currentStage,workflowRevision,terminalAtUtc,stopReasonCode", "workflowEntityId,startedAtUtc,workflowId",
            [state.EntityId.Format(), state.StartedAtUtc, state.WorkflowId.Value, status, outcome, state.CurrentStage.ToString(), state.WorkflowRevision, state.TerminalAtUtc, state.StopReasonCode], token, "workflowRevision")
                .ConfigureAwait(false);
        await UpsertAsync(db, "intrinsic_time_strategy_workflow_by_status_day", "status,startedDate,startedAtUtc,workflowId,workflowEntityId,outcome,currentStage,workflowRevision,terminalAtUtc,stopReasonCode", "status,startedDate,startedAtUtc,workflowId",
            [status, DateOnly.FromDateTime(state.StartedAtUtc), state.StartedAtUtc, state.WorkflowId.Value, state.EntityId.Format(), outcome, state.CurrentStage.ToString(), state.WorkflowRevision, state.TerminalAtUtc, state.StopReasonCode], token, "workflowRevision")
                .ConfigureAwait(false);
    }

    static async Task WriteTimelineAsync(IEnlistedPostgresTransaction db, WorkflowStrategyStateUpdatedEvent source, CancellationToken token)
    {
        var state = source.State;
        await UpsertAsync(db, "intrinsic_time_strategy_workflow_timeline", "workflowId,eventId,workflowEntityId,workflowRevision,stage,eventName,eventSchemaVersion,eventPayload,occurredAtUtc", "workflowId,eventId",
            [source.WorkflowId.Value, source.EventId, source.EntityId.Format(), source.WorkflowRevision, state.CurrentStage.ToString(), source.EventName, 2, MessagePackSerializer.Serialize(source, cancellationToken: token), source.UpdatedAtUtc], token)
                .ConfigureAwait(false);
        if (state.Status == WorkflowStrategyMachineStatus.Started && source.PreviousStatus != WorkflowStrategyMachineStatus.Started)
            await UpsertAsync(db, "intrinsic_time_strategy_workflow_start_attempt", "workflowEntityId,requestedAtUtc,requestedWorkflowId,decision,activeWorkflowId,startCommandId,triggerEventId,activeStage,reasonCode,sourceEventId", "workflowEntityId,requestedAtUtc,requestedWorkflowId",
                [source.EntityId.Format(), state.StartedAtUtc, source.WorkflowId.Value, StrategyWorkflowStartDecision.Accepted.ToString(), source.WorkflowId.Value, source.CommandId, state.TriggerEventId, state.CurrentStage.ToString(), string.Empty, source.EventId], token)
                    .ConfigureAwait(false);
    }

    static async Task WriteActiveAsync(IEnlistedPostgresTransaction db, WorkflowStrategyStateUpdatedEvent source, byte[] payload, CancellationToken token)
    {
        var state = source.State;
        if (state.Status != WorkflowStrategyMachineStatus.Started)
        {
            await db.ExecuteAsync("DELETE FROM trade.intrinsic_time_strategy_workflow_active WHERE workflowEntityId=$1 AND lastEventId<=$2;", [source.EntityId.Format(), source.EventId], token)
                .ConfigureAwait(false);
            return;
        }
        var iti = source.EntityId.ItiSignalEntityId;
        await UpsertAsync(db, "intrinsic_time_strategy_workflow_active", "workflowEntityId,workflowId,contractId,timeFrameStartValueDate,timePeriod,currentStage,workflowRevision,lastEventId,stateSchemaVersion,statePayload,startedAtUtc,updatedAtUtc", "workflowEntityId",
            [source.EntityId.Format(), source.WorkflowId.Value, iti.ContractId, iti.TimeFrameStartValueDate, iti.TimePeriod.ToString(), state.CurrentStage.ToString(), state.WorkflowRevision, source.EventId, 2, payload, state.StartedAtUtc, state.UpdatedAtUtc], token, "lastEventId")
                .ConfigureAwait(false);
    }

    static async Task WriteRiskAsync(IEnlistedPostgresTransaction db, WorkflowStrategyStateUpdatedEvent source, CancellationToken token)
    {
        var row = RiskHistoryIdentity.Row(source);
        if (row is null) return;
        var canonical = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(MessagePackBinarySerializer.Shared.Serialize(source with { EventId = 0 }));
        var hash = RiskContracts.Hash(canonical);
        var payload = MessagePackBinarySerializer.Shared.Serialize(canonical);
        var stored = await db.ScalarAsync("INSERT INTO trade.risk_management_invocation(workflow_id,invocation_id,revision,payload,content_hash) VALUES($1,$2,$3,$4,$5) ON CONFLICT(workflow_id,invocation_id,revision) DO UPDATE SET content_hash=trade.risk_management_invocation.content_hash WHERE trade.risk_management_invocation.content_hash=EXCLUDED.content_hash RETURNING content_hash;",
            [row.WorkflowId, row.InvocationId, row.Revision, payload, hash], token)
                .ConfigureAwait(false);
        if (stored is null) throw new InvalidDataException("Accepted risk history conflicts with different source facts.");
        await UpsertAsync(db, "risk_management_history", "portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload", "portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id",
            [row.PortfolioId, row.FundId, row.ValueDate, row.EvaluatedAtUtc, row.InvocationId, row.Revision, MessagePackBinarySerializer.Shared.Serialize(row)], token, "revision")
                .ConfigureAwait(false);
    }

    // Table and column names are private, reviewed literals above. All source facts are bound parameters.
    static Task<int> UpsertAsync(IEnlistedPostgresTransaction db, string table, string columnList, string keyList, object?[] values, CancellationToken token, string? revision = null)
    {
        var columns = columnList.Split(',');
        var keys = keyList.Split(',');
        var assignments = string.Join(',', columns.Except(keys, StringComparer.Ordinal).Select(name => name + "=EXCLUDED." + name));
        var sql = "INSERT INTO trade." + table + "(" + columnList + ") VALUES(" + string.Join(',', Enumerable.Range(1, values.Length).Select(i => "$" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))) + ") ON CONFLICT(" + keyList + ") DO UPDATE SET " + assignments;
        if (revision is not null) sql += " WHERE trade." + table + "." + revision + "<=EXCLUDED." + revision;
        return db.ExecuteAsync(sql + ";", values, token);
    }

    static StrategyWorkflowStatus Status(IntrinsicTimeStrategyWorkflowView state) => state.Status switch
    {
        WorkflowStrategyMachineStatus.Empty => StrategyWorkflowStatus.None,
        WorkflowStrategyMachineStatus.Started => StrategyWorkflowStatus.Running,
        WorkflowStrategyMachineStatus.Completed => StrategyWorkflowStatus.Completed,
        _ => StrategyWorkflowStatus.Stopped
    };
    static StrategyWorkflowOutcome Outcome(IntrinsicTimeStrategyWorkflowView state) => state.Outcome != StrategyWorkflowOutcome.None ? state.Outcome : state.Status switch
    {
        WorkflowStrategyMachineStatus.Completed => StrategyWorkflowOutcome.Completed,
        WorkflowStrategyMachineStatus.Failed => StrategyWorkflowOutcome.PipelineFailed,
        WorkflowStrategyMachineStatus.TimedOut => StrategyWorkflowOutcome.TimedOut,
        WorkflowStrategyMachineStatus.Cancelled => StrategyWorkflowOutcome.Cancelled,
        _ => StrategyWorkflowOutcome.None
    };
}
