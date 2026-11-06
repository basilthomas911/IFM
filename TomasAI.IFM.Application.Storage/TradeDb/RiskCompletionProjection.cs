using System.Security.Cryptography;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Stores and verifies immutable Risk calculation receipts for the read side.</summary>
public static class RiskCompletionProjection
{
    public static async Task WriteAsync(EnlistedEventTransaction db, RiskManagementFunctionCompletedEvent source, CancellationToken token)
    {
        Validate(source, source.WorkflowId.Value, source.CommandId, source.EventId);
        var payload = MessagePackBinarySerializer.Shared.Serialize(source);
        var hash = Convert.ToHexString(SHA256.HashData(payload));
        if (await db.ExecuteAsync(TradeDbSql.InsertRiskCompletion,
                [source.WorkflowId.Value, source.CommandId, source.EventId, payload, hash], token).ConfigureAwait(false) == 1)
            return;

        var prior = await db.QueryAsync(TradeDbSql.ReadRiskCompletion, [source.WorkflowId.Value, source.CommandId],
            row => (EventId: row.GetInt64(0), Hash: row.GetString(2)), token).ConfigureAwait(false);
        if (prior.Single() != (source.EventId, hash))
            throw new InvalidDataException("Conflicting projected Risk completion.");
    }

    internal static RiskManagementFunctionCompletedEvent Read(Guid workflow, Guid invocation, long eventId, byte[] payload, string hash)
    {
        if (!string.Equals(hash, Convert.ToHexString(SHA256.HashData(payload)), StringComparison.Ordinal))
            throw new InvalidDataException("Risk completion projection content hash mismatch.");
        var source = MessagePackBinarySerializer.Shared.Deserialize<RiskManagementFunctionCompletedEvent>(payload);
        Validate(source, workflow, invocation, eventId);
        return source;
    }

    static void Validate(RiskManagementFunctionCompletedEvent source, Guid workflow, Guid invocation, long eventId)
    {
        if (eventId <= 0 || source.EventId != eventId || source.Id == Guid.Empty || workflow == Guid.Empty
            || invocation == Guid.Empty || source.WorkflowId.Value != workflow || source.CommandId != invocation
            || source.Result.ResultId != source.Id || source.Result.WorkflowId != source.WorkflowId || source.Result.InvocationId != invocation
            || source.Result.InputWorkflowRevision != source.InputWorkflowRevision)
            throw new InvalidDataException("Risk completion projection identity or revision mismatch.");
        RiskContracts.ValidateResult(source.Result);
    }
}
