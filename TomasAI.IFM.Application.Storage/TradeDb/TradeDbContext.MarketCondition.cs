using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial class TradeDbContext
{
    public Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId)
        => GetMarketConditionAsync(workflowId, CancellationToken.None);
    public Task<MarketConditionReadModel?> GetMarketConditionAsync(StrategyWorkflowId workflowId, CancellationToken token)
        => _dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetMarketCondition)}", TradeDbCql.GetMarketCondition)
            .SetParameters(new GetMarketCondition(workflowId.Value)).ExecuteSingleAsync(MapMarketCondition, token);
    public Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(
        int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize)
        => GetMarketConditionHistoryAsync(fundId, instrumentRoot, targetHorizon, beforeUtc, pageSize,
            CancellationToken.None);
    public Task<ICollection<MarketConditionReadModel>> GetMarketConditionHistoryAsync(
        int fundId, string instrumentRoot, TimeFrameType targetHorizon, DateTime beforeUtc, int pageSize,
        CancellationToken token)
    {
        if (fundId <= 0) throw new ArgumentOutOfRangeException(nameof(fundId));
        if (string.IsNullOrWhiteSpace(instrumentRoot)) throw new ArgumentException(
            "An instrument root is required.", nameof(instrumentRoot));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return _dbFactory.TradeDb.Use(
                $"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetMarketConditionHistory)}",
                TradeDbCql.GetMarketConditionHistory)
            .SetParameters(new GetMarketConditionHistory(fundId, instrumentRoot, targetHorizon.ToString(),
                beforeUtc, pageSize)).ExecuteQueryAsync(MapMarketCondition, token);
    }
    public Task UpsertMarketConditionAsync(MarketConditionReadModel result)
        => UpsertMarketConditionAsync(result, CancellationToken.None);
    public async Task UpsertMarketConditionAsync(MarketConditionReadModel result, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(result);
        await _dbFactory.TradeDb.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertMarketCondition)}", TradeDbCql.UpsertMarketCondition)
            .SetParameters(new UpsertMarketCondition(result.WorkflowId.Value, result.WorkflowEntityId,
                result.InputWorkflowRevision, result.CommandId, result.SourceEventId, result.FundId,
                result.InstrumentRoot, result.TargetHorizon.ToString(), result.ParameterSetId,
                result.ParameterSetVersion, result.ParameterPayloadSha256, result.SnapshotId,
                result.SnapshotSha256, result.Tradeability.ToString(), result.ConditionType.ToString(),
                result.Direction.ToString(), result.Phase.ToString(), result.Strength, result.Confidence,
                result.PrimaryReasonCode, result.ResultPayload.ToArray(), result.ResultPayloadSha256,
                result.EvaluatedAtUtc, result.ValidUntilUtc, result.MarketDataAsOfUtc,
                result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(),
                result.LiquidityQuality.ToString(), result.DataQuality.ToString(),
                result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(),
                result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(),
                result.ReasonsPayload.ToArray(), result.SummaryText)).ExecuteCommandAsync(token).ConfigureAwait(false);
        await _dbFactory.TradeDb.Use(
                $"{nameof(TradeDbCql)}.{nameof(TradeDbCql.UpsertMarketConditionByFund)}",
                TradeDbCql.UpsertMarketConditionByFund)
            .SetParameters(new UpsertMarketConditionByFund(result.FundId, result.InstrumentRoot,
                result.TargetHorizon.ToString(), result.EvaluatedAtUtc, result.WorkflowId.Value,
                result.WorkflowEntityId, result.InputWorkflowRevision, result.CommandId, result.SourceEventId,
                result.ParameterSetId, result.ParameterSetVersion, result.ParameterPayloadSha256,
                result.SnapshotId, result.SnapshotSha256, result.Tradeability.ToString(),
                result.ConditionType.ToString(), result.Direction.ToString(), result.Phase.ToString(),
                result.Strength, result.Confidence, result.PrimaryReasonCode, result.ResultPayload.ToArray(),
                result.ResultPayloadSha256, result.ValidUntilUtc, result.MarketDataAsOfUtc,
                result.CompletedAtUtc, result.UpdatedAtUtc, result.VolatilityBehavior.ToString(),
                result.LiquidityQuality.ToString(), result.DataQuality.ToString(),
                result.UpstreamAlignment.ToString(), result.EvidencePayload.ToArray(),
                result.ConflictingEvidencePayload.ToArray(), result.BlockingReasonsPayload.ToArray(),
                result.ReasonsPayload.ToArray(), result.SummaryText)).ExecuteCommandAsync(token).ConfigureAwait(false);
    }
    static MarketConditionReadModel MapMarketCondition(IObjectDataRecord row) => new()
    {
        WorkflowId = new StrategyWorkflowId(row.GetGuid(0)), WorkflowEntityId = row.GetString(1),
        InputWorkflowRevision = row.GetLong(2), CommandId = row.GetGuid(3), SourceEventId = row.GetGuid(4),
        FundId = row.GetInt(5), InstrumentRoot = row.GetString(6),
        TargetHorizon = Enum.Parse<TimeFrameType>(row.GetString(7)), ParameterSetId = row.GetGuid(8),
        ParameterSetVersion = row.GetInt(9), ParameterPayloadSha256 = row.GetString(10),
        SnapshotId = row.GetGuid(11), SnapshotSha256 = row.GetString(12),
        Tradeability = Enum.Parse<MarketTradeability>(row.GetString(13)),
        ConditionType = Enum.Parse<MarketConditionType>(row.GetString(14)),
        Direction = Enum.Parse<MarketConditionDirection>(row.GetString(15)),
        Phase = Enum.Parse<MarketConditionPhase>(row.GetString(16)), Strength = row.GetDecimal(17),
        Confidence = row.GetDecimal(18), PrimaryReasonCode = row.GetString(19),
        ResultPayload = row.GetBytes(20), ResultPayloadSha256 = row.GetString(21),
        EvaluatedAtUtc = row.GetDateTime(22), ValidUntilUtc = row.GetDateTime(23),
        MarketDataAsOfUtc = row.GetDateTime(24), CompletedAtUtc = row.GetDateTime(25), UpdatedAtUtc = row.GetDateTime(26),
        VolatilityBehavior = Enum.Parse<MarketConditionVolatilityBehavior>(row.GetString(27)),
        LiquidityQuality = Enum.Parse<MarketConditionLiquidityQuality>(row.GetString(28)),
        DataQuality = Enum.Parse<MarketConditionDataQuality>(row.GetString(29)),
        UpstreamAlignment = Enum.Parse<MarketConditionUpstreamAlignment>(row.GetString(30)),
        EvidencePayload = row.GetBytes(31), ConflictingEvidencePayload = row.GetBytes(32),
        BlockingReasonsPayload = row.GetBytes(33), ReasonsPayload = row.GetBytes(34),
        SummaryText = row.GetString(35)
    };
}
