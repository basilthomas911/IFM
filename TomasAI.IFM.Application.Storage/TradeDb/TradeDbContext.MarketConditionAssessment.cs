using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial interface ITradeDbContext
{
    Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default);
    Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default);
    Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default);
}

public partial class TradeDbContext
{
    public async Task UpsertMarketConditionAssessmentAsync(MarketConditionAssessmentCompletedEvent completed, CancellationToken cancellationToken = default)
    {
        var r = MarketConditionAssessmentContracts.ReadResult(completed.Result);
        if (completed.Snapshot.ComputeHash() != r.SnapshotSha256 || completed.Snapshot.PayloadSha256 != r.SnapshotSha256)
            throw new ArgumentException("Assessment projection snapshot hash mismatch.");
        var bytes = MessagePackSerializer.Serialize(completed);
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        await _dbFactory.TradeDb.Use("AssessmentProjection.Exact", "INSERT INTO market_condition_assessment (workflow_id,payload,payload_sha256) VALUES (?,?,?);")
            .SetParameters(new AssessmentValues([r.WorkflowId.Value, bytes, hash])).ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await _dbFactory.TradeDb.Use("AssessmentProjection.History", "INSERT INTO market_condition_assessment_by_profile (market_profile_id,instrument_root,target_horizon,evaluated_at_utc,workflow_id,payload,payload_sha256) VALUES (?,?,?,?,?,?,?);")
            .SetParameters(new AssessmentValues([r.MarketProfileId, r.InstrumentRoot, r.TargetHorizon.ToString(), r.EvaluatedAtUtc, r.WorkflowId.Value, bytes, hash]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
    public Task<MarketConditionAssessmentCompletedEvent?> GetMarketConditionAssessmentAsync(StrategyWorkflowId workflowId, CancellationToken cancellationToken = default)
        => _dbFactory.TradeDb.Use("AssessmentProjection.Get", "SELECT payload,payload_sha256 FROM market_condition_assessment WHERE workflow_id=?;")
            .SetParameters(new AssessmentValues([workflowId.Value])).ExecuteSingleAsync(MapAssessment, cancellationToken);
    public Task<ICollection<MarketConditionAssessmentCompletedEvent>> GetMarketConditionAssessmentHistoryAsync(string profile, string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(profile) || root != "ES" || !MarketConditionAssessmentParameterSet.IsHorizon(horizon) || pageSize is < 1 or > 100 || !MarketConditionAssessmentContracts.Utc(beforeUtc))
            throw new ArgumentException("Invalid assessment history partition or bound.");
        return _dbFactory.TradeDb.Use("AssessmentProjection.HistoryRead", "SELECT payload,payload_sha256 FROM market_condition_assessment_by_profile WHERE market_profile_id=? AND instrument_root=? AND target_horizon=? AND evaluated_at_utc<? LIMIT ?;")
            .SetParameters(new AssessmentValues([profile, root, horizon.ToString(), beforeUtc, pageSize])).ExecuteQueryAsync(MapAssessment, cancellationToken);
    }
    static MarketConditionAssessmentCompletedEvent MapAssessment(IObjectDataRecord row)
    {
        var bytes = row.GetBytes(0);
        if (Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) != row.GetString(1)) throw new InvalidOperationException("Stored assessment payload hash mismatch.");
        var completed = MessagePackSerializer.Deserialize<MarketConditionAssessmentCompletedEvent>(bytes);
        var result = MarketConditionAssessmentContracts.ReadResult(completed.Result);
        if(completed.WorkflowId != result.WorkflowId || completed.Snapshot.PayloadSha256 != result.SnapshotSha256 ||
            completed.Snapshot.ComputeHash() != result.SnapshotSha256)
            throw new InvalidOperationException("Stored assessment snapshot or workflow identity mismatch.");
        return completed;
    }
    readonly record struct AssessmentValues(object[] Values) : IBindValue { public object Bind() => Values; }
}
