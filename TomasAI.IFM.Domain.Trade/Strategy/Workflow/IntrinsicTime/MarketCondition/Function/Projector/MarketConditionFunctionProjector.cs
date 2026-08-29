using MessagePack;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Projector;

public sealed class MarketConditionFunctionProjector(IDbContextFactory dbFactory)
    : IFunctionProjector<MarketConditionPipelineCompletedEvent>
{
    public async ValueTask ProjectAsync(MarketConditionPipelineCompletedEvent completed,
        CancellationToken token = default)
    {
        using var activity = MarketConditionTelemetry.Start("market-condition.completed-projection");
        ArgumentNullException.ThrowIfNull(completed);
        var result = MessagePackSerializer.Deserialize<MarketConditionResult>(completed.Result.Payload);
        await dbFactory.TradeDb.UpsertMarketConditionAsync(new MarketConditionReadModel
        {
            WorkflowId = completed.WorkflowId, WorkflowEntityId = completed.EntityId.Format(),
            InputWorkflowRevision = completed.InputWorkflowRevision, CommandId = completed.CommandId,
            SourceEventId = completed.Id, FundId = result.FundId, InstrumentRoot = result.InstrumentRoot,
            TargetHorizon = result.TargetHorizon, ParameterSetId = result.MarketConditionParameterSetId,
            ParameterSetVersion = result.MarketConditionParameterSetVersion,
            ParameterPayloadSha256 = completed.ParameterPayloadSha256,
            SnapshotId = result.SnapshotId, SnapshotSha256 = result.SnapshotSha256,
            Tradeability = result.Tradeability, ConditionType = result.ConditionType,
            Direction = result.Direction, Phase = result.Phase, Strength = result.Strength,
            Confidence = result.Confidence, PrimaryReasonCode = result.PrimaryReasonCode,
            ResultPayload = completed.Result.Payload, ResultPayloadSha256 = completed.Result.PayloadSha256,
            EvaluatedAtUtc = result.EvaluatedAtUtc, ValidUntilUtc = result.ValidUntilUtc,
            MarketDataAsOfUtc = result.MarketDataAsOfUtc, CompletedAtUtc = completed.CompletedAtUtc,
            UpdatedAtUtc = completed.CompletedAtUtc, VolatilityBehavior = result.VolatilityBehavior,
            LiquidityQuality = result.LiquidityQuality, DataQuality = result.DataQuality,
            UpstreamAlignment = result.UpstreamAlignment,
            EvidencePayload = MessagePackSerializer.Serialize(result.EvidenceItems),
            ConflictingEvidencePayload = MessagePackSerializer.Serialize(result.ConflictingEvidenceItems),
            BlockingReasonsPayload = MessagePackSerializer.Serialize(result.BlockingReasons),
            ReasonsPayload = MessagePackSerializer.Serialize(result.Reasons), SummaryText = result.SummaryText
        }, token).ConfigureAwait(false);
    }
}
