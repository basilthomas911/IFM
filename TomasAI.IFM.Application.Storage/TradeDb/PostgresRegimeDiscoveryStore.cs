using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Rebuilds Regime Discovery materialized results from accepted source facts on their owning transaction.</summary>
public static class PostgresRegimeDiscoveryStore
{
    /// <summary>Preserves immutable invocation facts and updates the latest workflow result atomically.</summary>
    public static async Task ProjectAsync(IEnlistedPostgresTransaction transaction, RegimeDiscoveryPipelineCompletedEvent source, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(source);
        var result = source.Result.ReadRegimeResult();
        if (source.WorkflowId != result.WorkflowId || source.EntityId != result.EntityId || source.SignalSnapshotId != result.SignalSnapshotId
            || source.WorkflowId.Value == Guid.Empty || source.CommandId == Guid.Empty || source.Id == Guid.Empty || source.InputWorkflowRevision <= 0)
            throw new InvalidDataException("Regime Discovery source and result identities must agree.");
        var payload = MessagePackBinarySerializer.Shared.Serialize(source with { EventId = 0 });
        var accepted = await transaction.ScalarAsync("""
            INSERT INTO trade.regime_discovery_invocation(workflow_id,command_id,source_event_id,source_payload)
            VALUES($1,$2,$3,$4)
            ON CONFLICT(workflow_id,command_id) DO UPDATE SET source_payload=EXCLUDED.source_payload
            WHERE regime_discovery_invocation.source_payload=EXCLUDED.source_payload RETURNING command_id;
            """, [source.WorkflowId.Value, source.CommandId, source.Id, payload], token)
            .ConfigureAwait(false);
        if (accepted is null) throw new InvalidDataException("Conflicting accepted Regime Discovery invocation.");
        var bytes = string.Equals(source.Result.ContentType, StrategyStageResultEnvelope.TypedRegimeContentType, StringComparison.Ordinal)
            ? MessagePackSerializer.Serialize(result, cancellationToken: token) : source.Result.Payload.ToArray();
        await UpsertAsync(transaction, new RegimeDiscoveryReadModel
        {
            WorkflowId=source.WorkflowId, WorkflowEntityId=source.EntityId.Format(), InputWorkflowRevision=source.InputWorkflowRevision,
            CommandId=source.CommandId, SourceEventId=source.Id, SourceEventSequence=source.EventId, Status="Completed",
            ParameterPayloadSha256=source.ParameterPayloadSha256, SignalSnapshotId=source.SignalSnapshotId, ResultPayload=bytes,
            ResultPayloadSha256=StrategyStageResultEnvelope.ComputePayloadSha256(bytes),
            ReasonsPayload=MessagePackSerializer.Serialize(result.Reasons, cancellationToken: token), SchemaVersion=source.Result.SchemaVersion,
            TerminalAtUtc=source.CompletedAtUtc, UpdatedAtUtc=source.CompletedAtUtc
        }, token)
            .ConfigureAwait(false);
    }

    /// <summary>Writes the current result, retaining newer accepted workflow revisions.</summary>
    public static Task<int> UpsertAsync(IEnlistedPostgresTransaction transaction, RegimeDiscoveryReadModel value, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(value);
        return transaction.ExecuteAsync("""
            INSERT INTO trade.regime_discovery(workflowId,workflowEntityId,inputWorkflowRevision,commandId,sourceEventId,sourceEventSequence,status,
                parameterPayloadSha256,signalSnapshotId,resultPayload,resultPayloadSha256,failureCode,failureMessage,reasonsPayload,schemaVersion,terminalAtUtc,updatedAtUtc)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17)
            ON CONFLICT(workflowId) DO UPDATE SET workflowEntityId=EXCLUDED.workflowEntityId,inputWorkflowRevision=EXCLUDED.inputWorkflowRevision,
                commandId=EXCLUDED.commandId,sourceEventId=EXCLUDED.sourceEventId,sourceEventSequence=EXCLUDED.sourceEventSequence,status=EXCLUDED.status,
                parameterPayloadSha256=EXCLUDED.parameterPayloadSha256,signalSnapshotId=EXCLUDED.signalSnapshotId,resultPayload=EXCLUDED.resultPayload,
                resultPayloadSha256=EXCLUDED.resultPayloadSha256,failureCode=EXCLUDED.failureCode,failureMessage=EXCLUDED.failureMessage,
                reasonsPayload=EXCLUDED.reasonsPayload,schemaVersion=EXCLUDED.schemaVersion,terminalAtUtc=EXCLUDED.terminalAtUtc,updatedAtUtc=EXCLUDED.updatedAtUtc
            WHERE regime_discovery.inputWorkflowRevision < EXCLUDED.inputWorkflowRevision
               OR (regime_discovery.inputWorkflowRevision = EXCLUDED.inputWorkflowRevision AND regime_discovery.commandId = EXCLUDED.commandId
                   AND regime_discovery.resultPayload = EXCLUDED.resultPayload);
            """, [value.WorkflowId.Value,value.WorkflowEntityId,value.InputWorkflowRevision,value.CommandId,value.SourceEventId,value.SourceEventSequence,
                value.Status,value.ParameterPayloadSha256,value.SignalSnapshotId,value.ResultPayload.ToArray(),value.ResultPayloadSha256,value.FailureCode,
                value.FailureMessage,value.ReasonsPayload.ToArray(),value.SchemaVersion,value.TerminalAtUtc,value.UpdatedAtUtc],token);
    }

    /// <summary>Reads one persisted workflow result without reconstructing historical source events.</summary>
    public static async Task<RegimeDiscoveryReadModel?> ReadAsync(IEnlistedPostgresTransaction transaction, StrategyWorkflowId workflowId, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var rows=await transaction.QueryAsync("""
            SELECT workflowId,workflowEntityId,inputWorkflowRevision,commandId,sourceEventId,sourceEventSequence,status,parameterPayloadSha256,
                signalSnapshotId,resultPayload,resultPayloadSha256,failureCode,failureMessage,reasonsPayload,schemaVersion,terminalAtUtc,updatedAtUtc
            FROM trade.regime_discovery WHERE workflowId=$1;
            """,[workflowId.Value],r=>new RegimeDiscoveryReadModel
            {
                WorkflowId=new StrategyWorkflowId(r.GetGuid(0)),WorkflowEntityId=r.GetString(1),InputWorkflowRevision=r.GetInt64(2),CommandId=r.GetGuid(3),
                SourceEventId=r.GetGuid(4),SourceEventSequence=r.GetInt64(5),Status=r.GetString(6),ParameterPayloadSha256=r.GetString(7),SignalSnapshotId=r.GetGuid(8),
                ResultPayload=r.IsDBNull(9)?ReadOnlyMemory<byte>.Empty:r.GetFieldValue<byte[]>(9),ResultPayloadSha256=r.IsDBNull(10)?string.Empty:r.GetString(10),
                FailureCode=r.IsDBNull(11)?0:r.GetInt32(11),FailureMessage=r.IsDBNull(12)?string.Empty:r.GetString(12),
                ReasonsPayload=r.IsDBNull(13)?ReadOnlyMemory<byte>.Empty:r.GetFieldValue<byte[]>(13),SchemaVersion=r.GetInt32(14),TerminalAtUtc=r.GetDateTime(15),UpdatedAtUtc=r.GetDateTime(16)
            },token)
            .ConfigureAwait(false);
        return rows.Count==0?null:rows[0];
    }
}
