using MessagePack;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Function.Projector;

/// <summary>
/// Synchronously writes the candidate completed Regime Discovery result to ScyllaDB. It owns no queue, replay,
/// actor route, publication, processing event, or failed-result projection.
/// </summary>
public sealed class RegimeDiscoveryFunctionProjector(IDbContextFactory dbFactory)
    : IFunctionProjector<RegimeDiscoveryPipelineCompletedEvent>
{
    const int SchemaVersion = 1;
    readonly IDbContextFactory _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));

    public async ValueTask ProjectAsync(
        RegimeDiscoveryPipelineCompletedEvent completed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completed);
        var payload = completed.Result.Payload;
        var result = MessagePackSerializer.Deserialize<RegimeDiscoveryResult>(payload);
        await _dbFactory.TradeDb.UpsertRegimeDiscoveryAsync(new RegimeDiscoveryReadModel
        {
            WorkflowId = completed.WorkflowId,
            WorkflowEntityId = completed.EntityId.Format(),
            InputWorkflowRevision = completed.InputWorkflowRevision,
            CommandId = completed.CommandId,
            SourceEventId = completed.Id,
            // Projection intentionally precedes the PostgreSQL commit, so no event sequence exists yet.
            SourceEventSequence = 0,
            Status = "Completed",
            ParameterPayloadSha256 = completed.ParameterPayloadSha256,
            SignalSnapshotId = completed.SignalSnapshotId,
            ResultPayload = payload,
            ResultPayloadSha256 = completed.Result.PayloadSha256,
            ReasonsPayload = MessagePackSerializer.Serialize(result.Reasons),
            SchemaVersion = SchemaVersion,
            TerminalAtUtc = completed.CompletedAtUtc,
            UpdatedAtUtc = completed.CompletedAtUtc
        }, cancellationToken).ConfigureAwait(false);
    }
}
