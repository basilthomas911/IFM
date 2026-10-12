using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpsertMarketConditionByFund(
    int FundId, string InstrumentRoot, string TargetHorizon, DateTime EvaluatedAtUtc, Guid WorkflowId,
    string WorkflowEntityId, long InputWorkflowRevision, Guid CommandId, Guid SourceEventId,
    Guid ParameterSetId, int ParameterSetVersion, string ParameterPayloadSha256, Guid SnapshotId,
    string SnapshotSha256, string Tradeability, string ConditionType, string Direction, string Phase,
    decimal Strength, decimal Confidence, string PrimaryReasonCode, byte[] ResultPayload,
    string ResultPayloadSha256, DateTime ValidUntilUtc, DateTime MarketDataAsOfUtc,
    DateTime CompletedAtUtc, DateTime UpdatedAtUtc, string VolatilityBehavior, string LiquidityQuality,
    string DataQuality, string UpstreamAlignment, byte[] EvidencePayload, byte[] ConflictingEvidencePayload,
    byte[] BlockingReasonsPayload, byte[] ReasonsPayload, string SummaryText) : IBindValue
{
    public object Bind() => new object?[] { FundId, InstrumentRoot, TargetHorizon, EvaluatedAtUtc,
        WorkflowId, WorkflowEntityId, InputWorkflowRevision, CommandId, SourceEventId, ParameterSetId,
        ParameterSetVersion, ParameterPayloadSha256, SnapshotId, SnapshotSha256, Tradeability,
        ConditionType, Direction, Phase, Strength, Confidence, PrimaryReasonCode, ResultPayload,
        ResultPayloadSha256, ValidUntilUtc, MarketDataAsOfUtc, CompletedAtUtc, UpdatedAtUtc,
        VolatilityBehavior, LiquidityQuality, DataQuality, UpstreamAlignment, EvidencePayload,
        ConflictingEvidencePayload, BlockingReasonsPayload, ReasonsPayload, SummaryText };
}
