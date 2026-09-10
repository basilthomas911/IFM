using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
namespace TomasAI.IFM.Application.Storage.TradeDb;

public partial interface ITradeDbContext
{
    Task UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default);
    Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default);
    Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default);
}
public partial class TradeDbContext
{
    public async Task UpsertRiskHistoryAsync(WorkflowStrategyStateUpdatedEvent snapshot, CancellationToken token = default)
    {
        var row = RiskHistoryIdentity.Row(snapshot);
        if (row is null) return;
        token.ThrowIfCancellationRequested();

        // The exact invocation row and its scoped summary are one rebuildable projection unit.
        // Once the unit starts, finish every idempotent Scylla statement even if the recovery
        // worker is asked to stop; otherwise shutdown can leave an exact row without its history
        // row and surface cancellation from the second statement as an unhandled worker error.
        var projectionToken = CancellationToken.None;
        if (snapshot.WorkflowId != snapshot.State.WorkflowId || snapshot.WorkflowRevision != row.Revision || snapshot.EntityId != snapshot.State.EntityId)
            throw new InvalidDataException("Risk history source identity mismatch.");
        var canonical = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(
            MessagePackBinarySerializer.Shared.Serialize(snapshot with { EventId = 0 }));
        var payload = MessagePackBinarySerializer.Shared.Serialize(canonical);
        var hash = RiskContracts.Hash(canonical);
        var inserted = await _dbFactory.TradeDb.Use("RiskHistory.Insert", "INSERT INTO risk_management_invocation (workflow_id,invocation_id,revision,payload,content_hash) VALUES (?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new RiskValues([row.WorkflowId,row.InvocationId,row.Revision,payload,hash])).ExecuteSingleAsync(r=>r.GetBool(0),projectionToken);
        if (!inserted)
        {
            var old = await _dbFactory.TradeDb.Use("RiskHistory.Verify", "SELECT content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? AND revision=?;")
                .SetParameters(new RiskValues([row.WorkflowId,row.InvocationId,row.Revision])).ExecuteSingleAsync(r=>r.GetString(0),projectionToken);
            if (old != hash) throw new InvalidDataException("Conflicting Risk history source revision.");
        }
        var summary = MessagePackBinarySerializer.Shared.Serialize(row);
        var added = await _dbFactory.TradeDb.Use("RiskHistory.SummaryInsert", "INSERT INTO risk_management_history (portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,revision,payload) VALUES (?,?,?,?,?,?,?) IF NOT EXISTS;")
            .SetParameters(new RiskValues([row.PortfolioId,row.FundId,row.ValueDate,row.EvaluatedAtUtc,row.InvocationId,row.Revision,summary])).ExecuteSingleAsync(r=>r.GetBool(0),projectionToken);
        if (!added)
            await _dbFactory.TradeDb.Use("RiskHistory.SummaryAdvance", "UPDATE risk_management_history SET revision=?,payload=? WHERE portfolio_id=? AND fund_id=? AND value_date=? AND evaluated_at_utc=? AND invocation_id=? IF revision<?;")
                .SetParameters(new RiskValues([row.Revision,summary,row.PortfolioId,row.FundId,row.ValueDate,row.EvaluatedAtUtc,row.InvocationId,row.Revision])).ExecuteSingleAsync(r=>r.GetBool(0),projectionToken);
    }
    public Task<WorkflowStrategyStateUpdatedEvent?> GetRiskInvocationAsync(Guid workflow, Guid invocation, CancellationToken token = default)
    {
        if (workflow == Guid.Empty || invocation == Guid.Empty) throw new ArgumentException("Exact Risk identities required.");
        return _dbFactory.TradeDb.Use("RiskHistory.Exact", "SELECT payload,content_hash FROM risk_management_invocation WHERE workflow_id=? AND invocation_id=? ORDER BY revision DESC LIMIT 1;")
            .SetParameters(new RiskValues([workflow,invocation])).ExecuteSingleAsync(r=>
            {
                var payload=r.GetBytes(0);
                var value = MessagePackBinarySerializer.Shared.Deserialize<WorkflowStrategyStateUpdatedEvent>(payload);
                if (RiskContracts.Hash(value) != r.GetString(1) || RiskHistoryIdentity.Row(value) is not { } row || row.WorkflowId != workflow || row.InvocationId != invocation)
                    throw new InvalidDataException("Risk history content hash mismatch.");
                return value;
            },token);
    }
    public Task<QueryPage<RiskHistoryRow>> GetRiskHistoryAsync(int portfolio, int fund, DateOnly date, int size, byte[]? cursor, CancellationToken token = default)
    {
        if (portfolio<=0 || fund<=0 || date==default || size is <1 or >100) throw new ArgumentException("Invalid Risk history scope.");
        return _dbFactory.TradeDb.Use("RiskHistory.Page", "SELECT payload FROM risk_management_history WHERE portfolio_id=? AND fund_id=? AND value_date=?;")
            .SetParameters(new RiskValues([portfolio,fund,date])).ExecutePageAsync(r=>MessagePackBinarySerializer.Shared.Deserialize<RiskHistoryRow>(r.GetBytes(0)),size,cursor,token);
    }
    readonly record struct RiskValues(object[] Values):IBindValue { public object Bind()=>Values; }
}
