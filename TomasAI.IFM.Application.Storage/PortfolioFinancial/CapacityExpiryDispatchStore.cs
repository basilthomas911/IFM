using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record CapacityExpiryDispatch(int FundId,ChangeCapacityReservationCommand Request);

/// <summary>Durable dispatch journal only. Expiry changes money/usage exclusively through the lifecycle Command actor.</summary>
public sealed class CapacityExpiryDispatchStore(IPostgresEventTransaction transactions)
{
    public Task<IReadOnlyList<CapacityExpiryDispatch>> PrepareAsync(Func<CapacityExpiryCandidate,DateTime,ChangeCapacityReservationCommand> create,
        CancellationToken token=default,int? portfolioId=null)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        var now=DateTime.UtcNow;
        var candidates=await db.QueryAsync("""
            SELECT r.portfolio_id,r.fund_id,r.reservation_id,r.version,r.strategy_units,r.requirements->>'ContentHash',
              a.financial_revision,r.expires_at_utc
            FROM portfolio_financial.capacity_reservation r JOIN portfolio_financial.financial_authority a USING(portfolio_id)
            WHERE r.status=1 AND r.execution_id IS NULL AND r.expires_at_utc<=$1 AND ($2::int IS NULL OR r.portfolio_id=$2)
              AND NOT EXISTS(SELECT 1 FROM portfolio_financial.capacity_expiry_dispatch d WHERE d.reservation_id=r.reservation_id AND d.status='Pending')
            ORDER BY r.expires_at_utc,r.reservation_id LIMIT 32;
            """,[now,portfolioId],r=>new CapacityExpiryCandidate(r.GetInt32(0),r.GetInt32(1),r.GetGuid(2),r.GetInt64(3),r.GetInt32(4),r.GetString(5),r.GetInt64(6),r.GetDateTime(7)),ct);
        foreach(var candidate in candidates)
        {
            var command=create(candidate,now);
            Require(command.Body.ReservationId==candidate.ReservationId && command.Body.ChangeKind==CapacityChangeKind.ExpireUnconsumed &&
                command.InputSha256==FinancialCanonicalHash.Request(command),FinancialReasons.InvalidContract,"Invalid expiry dispatch request.");
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.capacity_expiry_dispatch(operation_id,reservation_id,portfolio_id,fund_id,request,input_hash,status,created_at_utc)
                VALUES($1,$2,$3,$4,$5,$6,'Pending',$7) ON CONFLICT DO NOTHING;
                """,[command.OperationId,candidate.ReservationId,candidate.PortfolioId,candidate.FundId,Json(command),command.InputSha256,now],ct);
        }
        var pending=await db.QueryAsync("""
            SELECT fund_id,request::text FROM portfolio_financial.capacity_expiry_dispatch WHERE status='Pending' AND ($1::int IS NULL OR portfolio_id=$1)
            ORDER BY created_at_utc,operation_id LIMIT 32;
            """,[portfolioId],r=>new CapacityExpiryDispatch(r.GetInt32(0),Decode<ChangeCapacityReservationCommand>(r.GetString(1))),ct);
        return (IReadOnlyList<CapacityExpiryDispatch>)pending;
    },token);

    /// <summary>Closes a dispatcher attempt under the same fence as posting, only after receipt or expired no-commit proof.</summary>
    public Task<bool> ReconcileAsync(ChangeCapacityReservationCommand request,CancellationToken token=default)=>transactions.ExecuteAsync(async(db,ct)=>
    {
        await LockAuthorityAsync(db,request.PortfolioId,null,ct);
        var receipt=await ReadOperationAsync<CapacityLifecycleCompletedEvent>(db,request.PortfolioId,request.OperationId,request.InputSha256,ct);
        string? status=null;
        if(receipt is not null)
        {
            Require(receipt.Receipt.ReservationId==request.Body.ReservationId && receipt.Receipt.Status==ReservationStatus.Expired,
                FinancialReasons.RequestMismatch,"Expiry receipt differs from the dispatched operation.");
            status="Committed";
        }
        else if(DateTime.UtcNow>=request.ExpiresAtUtc) status="ExpiredWithoutCommit";
        if(status is null) return false;
        await db.ExecuteAsync("""
            UPDATE portfolio_financial.capacity_expiry_dispatch SET status=$3,reconciled_at_utc=$4
            WHERE operation_id=$1 AND input_hash=$2 AND status='Pending';
            """,[request.OperationId,request.InputSha256,status,DateTime.UtcNow],ct);
        return true;
    },token);
}
