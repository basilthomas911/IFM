using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public delegate void CapacityAdmissionCalculation(CapacityReservationRequest request, QualifiedCapacityAssessment assessment,
    FinancialBookConfiguration book, decimal availableCash, IReadOnlyList<CapacityUsed> used, DateTime nowUtc);
public delegate CapacityTransition CapacityLifecycleCalculation(ReservationSnapshot current, CapacityLifecycleRequest change,
    bool consumption, DateTime nowUtc);

public interface ICapacityReservationStore
{
    Task<CapacityReservationCompletedEvent> ReserveAsync(ReservePortfolioTradeRiskCommand request,
        CapacityAdmissionCalculation validate, Func<CapacityReservationReceipt, CapacityReservationCompletedEvent> complete, CancellationToken token = default);
    Task<T> ChangeAsync<T>(IFinancialRequest<CapacityLifecycleRequest> request, bool consumption,
        CapacityLifecycleCalculation calculate, Func<CapacityLifecycleReceipt, T> complete, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent;
    Task<ReservationSnapshot?> ReadCurrentAsync(int portfolioId, Guid reservationId, CancellationToken token = default);
}

/// <summary>Uses the ledger's Portfolio fence for admission, consumption and all lifecycle changes.</summary>
public sealed class CapacityReservationStore(IPostgresEventTransaction transactions) : ICapacityReservationStore
{
    public Task<CapacityReservationCompletedEvent> ReserveAsync(ReservePortfolioTradeRiskCommand request,
        CapacityAdmissionCalculation validate, Func<CapacityReservationReceipt, CapacityReservationCompletedEvent> complete, CancellationToken token = default)
        => CommitAsync(request, async (db, authority, cancellation) =>
    {
        var body = request.Body; var now = DateTime.UtcNow;
        Require(authority.State == "Active" && body.BookId == authority.Book.BookId && body.ExecutionEnvironment == authority.Book.Environment,
            FinancialReasons.AuthorityDenied, "Financial book/environment is not active for this reservation.");
        await ValidateFundSourcesAsync(db, authority.Book, body.FundId, true, cancellation);
        // This evidence is read from a committed Risk event, never accepted from the reserve caller.
        var result = await ReadEvidenceAsync<ICapacityAssessmentCompletedEvent>(db, body.RiskInvocationId, cancellation);
        Require(result is not null, FinancialReasons.AuthorityDenied, "Committed qualified Risk assessment is unavailable.");
        Require(body.Requirements.Exposures.Length is >0 and <=256,FinancialReasons.InvalidContract,"Capacity scope count exceeds its bound.");
        var used = await db.QueryAsync("""
            SELECT u.scope_kind,u.scope_key,u.measure,u.unit,u.held,u.working,u.position
            FROM portfolio_financial.capacity_usage u
            WHERE u.portfolio_id=$1 AND EXISTS (
              SELECT 1 FROM jsonb_to_recordset($2::jsonb) AS requested(scope_kind int,scope_key text,measure int,unit int)
              WHERE requested.scope_kind=u.scope_kind AND requested.scope_key=u.scope_key AND requested.measure=u.measure AND requested.unit=u.unit)
            LIMIT 257;
            """, [request.PortfolioId,Json(body.Requirements.Exposures.Select(x=>new
                { scope_kind=(int)x.ScopeKind,scope_key=x.ScopeKey,measure=(int)x.Measure,unit=(int)x.Unit }).ToArray())], r => new CapacityUsed((CapacityScopeKind)r.GetInt32(0), r.GetString(1),
                (CapacityMeasure)r.GetInt32(2), (CapacityUnit)r.GetInt32(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6)), cancellation);
        Require(used.Count<=256,FinancialReasons.InvalidContract,"Capacity usage exceeds its bound.");
        var available = await GeneralLedgerStore.AvailableCash(db, body.BookId, body.FundId, cancellation);
        validate(body, result!.CapacityAssessment, authority.Book, available, used, now);
        var existing = await db.ScalarAsync("""
            SELECT reservation_id FROM portfolio_financial.capacity_reservation
            WHERE reservation_id=$1 OR (portfolio_id=$2 AND order_id=$3 AND status NOT IN (8,9));
            """, [body.ReservationId, request.PortfolioId, body.OrderId], cancellation);
        Require(existing is null, FinancialReasons.RequestMismatch, "Reservation identity/order already has a capacity decision.");
        var revision = checked(authority.Revision + 1);
        var receipt = new CapacityReservationReceipt
        {
            OperationId=request.OperationId, ReservationId=body.ReservationId, PortfolioId=request.PortfolioId,
            FundId=body.FundId, BookId=body.BookId, OrderId=body.OrderId, TradeIds=body.TradeIds,
            RiskResultId=body.RiskResultId, RiskAssessmentHash=body.RiskAssessmentHash,
            CompositionResultHash=body.CompositionResultHash, UnitCandidateHash=body.UnitCandidateHash,
            SizedOrderHash=body.SizedOrderHash, StrategyUnits=body.StrategyUnits, Requirements=body.Requirements,
            AuthorityEpoch=authority.Book.AuthorityEpoch, FinancialRevision=revision, GrantedAtUtc=now,
            ValidUntilUtc=body.ValidUntilUtc, ExecutionEnvironment=body.ExecutionEnvironment,
            CompletedEventId=Guid.NewGuid(), InputHash=request.InputSha256
        };
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.capacity_reservation(reservation_id,portfolio_id,operation_id,fund_id,book_id,order_id,
              candidate_hash,risk_hash,sized_order_hash,strategy_units,requirements,environment,expires_at_utc,status,version,
              completion_event_id,request,receipt,input_hash,remaining_units)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,1,1,$14,$15,$16,$17,$10);
            """, [body.ReservationId,request.PortfolioId,request.OperationId,body.FundId,body.BookId,body.OrderId,
                body.UnitCandidateHash,body.RiskAssessmentHash,body.SizedOrderHash,body.StrategyUnits,Json(body.Requirements),
                body.ExecutionEnvironment,body.ValidUntilUtc,receipt.CompletedEventId,Json(body),Json(receipt),request.InputSha256], cancellation);
        foreach(var exposure in body.Requirements.Exposures)
            await AddUsageAsync(db, request.PortfolioId, exposure, (0,0,0), (1,0,0), revision, cancellation);
        var completed = complete(receipt);
        CheckOutcome(request, completed, receipt.CompletedEventId);
        await SaveOutcomeAsync(db, request, completed, revision, true, cancellation);
        return completed;
    }, token);

    public Task<T> ChangeAsync<T>(IFinancialRequest<CapacityLifecycleRequest> request, bool consumption,
        CapacityLifecycleCalculation calculate, Func<CapacityLifecycleReceipt,T> complete, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent => CommitAsync(request, async (db, authority, cancellation) =>
    {
        var body=request.Body; var now=DateTime.UtcNow;
        Require(consumption == (request is ConsumeCapacityReservationCommand) &&
            (!consumption || body.ChangeKind == CapacityChangeKind.Consume), FinancialReasons.InvalidLifecycle, "Consumption requires its dedicated Function contract.");
        var row=await ReadReservationAsync(db,request.PortfolioId,body.ReservationId,cancellation);
        Require(row is not null,FinancialReasons.InvalidLifecycle,"Reservation does not belong to this Portfolio.");
        var (current, original)=row!.Value;
        Require(body.Source.SourceEventId!=Guid.Empty && !string.IsNullOrWhiteSpace(body.Source.System) &&
            !string.IsNullOrWhiteSpace(body.Source.SourceContentHash),FinancialReasons.InvalidContract,"Lifecycle source identity/hash is required.");
        var source=$"{body.Source.System}:{body.Source.SourceEventId:N}";
        var prior=await db.ScalarAsync("SELECT operation_id FROM portfolio_financial.capacity_lifecycle WHERE reservation_id=$1 AND source_identity=$2;",
            [body.ReservationId,source],cancellation);
        if(prior is Guid priorOperation) throw new FinancialOperationException(FinancialReasons.SourceConflict,
            "Lifecycle source has already been committed.",FinancialCommitDisposition.NoNewMutation,priorOperation);
        if(consumption)
        {
            Require(authority.State=="Active" && original.ExecutionEnvironment==authority.Book.Environment,
                FinancialReasons.AuthorityRevoked,"Financial authority no longer permits submission.");
            await ValidateFundSourcesAsync(db,authority.Book,original.FundId,true,cancellation);
            var fund=authority.Book.Funds.Single(x=>x.FundId==original.FundId);
            var reference=fund.Deployments.Length==0?fund.Reference:fund.Deployments.SingleOrDefault(x=>x.Reference.DeploymentKey==original.Authority.DeploymentKey)?.Reference;
            Require(original.Authority==reference,
                FinancialReasons.AuthorityRevoked,"Reservation authority changed before consumption.");
            var accepted=await ReadEvidenceAsync<ICapacityExecutionAcceptedEvent>(db,body.ExecutionId,cancellation);
            var intent=accepted?.CapacityAcceptance;
            Require(intent is not null && intent.ExecutionId==body.ExecutionId && intent.ExecutionRevision==body.ExecutionRevision &&
                intent.ReservationId==body.ReservationId && intent.PortfolioId==request.PortfolioId && intent.FundId==original.FundId &&
                intent.OrderId==original.OrderId && intent.SizedOrderHash==original.SizedOrderHash && intent.RequirementsHash==original.Requirements.ContentHash &&
                intent.Environment==original.ExecutionEnvironment && intent.ValidUntilUtc>now,
                FinancialReasons.AuthorityDenied,"Exact committed workflow acceptance is required before consumption.");
        }
        if(body.ChangeKind is CapacityChangeKind.ConfirmCancel or CapacityChangeKind.RecordPositionClose)
        {
            var reconciled=await ReadEvidenceAsync<ICapacityExecutionReconciledEvent>(db,body.Source.SourceEventId,cancellation);
            var evidence=reconciled?.CapacityReconciliation;
            Require(evidence is not null && evidence.FinancialFactsComplete && evidence.ExecutionId==body.ExecutionId &&
                evidence.ExecutionRevision==body.ExecutionRevision && evidence.PortfolioId==request.PortfolioId && evidence.FundId==original.FundId &&
                evidence.OrderId==original.OrderId && evidence.ReservationId==body.ReservationId && evidence.SourceContentHash==body.Source.SourceContentHash &&
                evidence.FilledUnits==body.FilledUnits && evidence.CancelledUnits==body.CancelledUnits && evidence.ClosedUnits==body.ClosedUnits && evidence.Environment==original.ExecutionEnvironment &&
                evidence.ReconciliationReference==body.RelatedPostingReference && !string.IsNullOrWhiteSpace(body.RelatedPostingReference),
                FinancialReasons.InvalidLifecycle,"Exact committed terminal execution/financial reconciliation is required before release.");
        }
        var next=calculate(current,body,consumption,now); var revision=checked(authority.Revision+1);
        var previous=Fractions(current);
        foreach(var exposure in original.Requirements.Exposures)
            await AddUsageAsync(db,request.PortfolioId,exposure,previous,
                (next.HeldFraction,next.WorkingFraction,next.PositionFraction),revision,cancellation);
        var receipt=new CapacityLifecycleReceipt
        {
            OperationId=request.OperationId,ReservationId=body.ReservationId,ReservationVersion=checked(current.Version+1),
            FinancialRevision=revision,Status=next.Status,FilledUnits=next.FilledUnits,CancelledUnits=next.CancelledUnits,
            RemainingUnits=next.RemainingUnits,ClosedUnits=next.ClosedUnits,CurrentRequirementsHash=original.Requirements.ContentHash,
            CommittedAtUtc=now,CompletedEventId=Guid.NewGuid(),InputHash=request.InputSha256
        };
        await db.ExecuteAsync("""
            UPDATE portfolio_financial.capacity_reservation SET status=$3,version=$4,execution_id=$5,
              filled_units=$6,cancelled_units=$7,remaining_units=$8,closed_units=$9 WHERE reservation_id=$1 AND portfolio_id=$2;
            """,[body.ReservationId,request.PortfolioId,(int)next.Status,receipt.ReservationVersion,
                body.ExecutionId==Guid.Empty?null:body.ExecutionId,next.FilledUnits,next.CancelledUnits,next.RemainingUnits,next.ClosedUnits],cancellation);
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.capacity_lifecycle(reservation_id,version,source_identity,operation_id,prior_status,new_status,
              usage_delta,filled_units,cancelled_units,remaining_units,execution_id,execution_revision,committed_at_utc,completion_event_id,input_hash,receipt)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16);
            """,[body.ReservationId,receipt.ReservationVersion,source,request.OperationId,(int)current.Status,(int)next.Status,Json(next),
                next.FilledUnits,next.CancelledUnits,next.RemainingUnits,body.ExecutionId==Guid.Empty?null:body.ExecutionId,
                body.ExecutionRevision,now,receipt.CompletedEventId,request.InputSha256,Json(receipt)],cancellation);
        var completed=complete(receipt); CheckOutcome(request,completed,receipt.CompletedEventId);
        await SaveOutcomeAsync(db,request,completed,revision,consumption,cancellation);
        return completed;
    },token);

    public Task<ReservationSnapshot?> ReadCurrentAsync(int portfolioId,Guid reservationId,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,cancellation)=>(await ReadReservationAsync(db,portfolioId,reservationId,cancellation))?.State,token);

    async Task<T> CommitAsync<T>(IFinancialRequest request,
        Func<EnlistedEventTransaction,(FinancialBookConfiguration Book,long Revision,string State),CancellationToken,Task<T>> apply,
        CancellationToken token) where T:class,IFinancialCompletedEvent
    {
        try { return await transactions.ExecuteAsync(async(db,cancellation)=>
        {
            var replay=await ReadOperationAsync<T>(db,request.PortfolioId,request.OperationId,request.InputSha256,cancellation);
            if(replay is not null) return replay;
            var authority=await LockAuthorityAsync(db,request.PortfolioId,null,cancellation);
            replay=await ReadOperationAsync<T>(db,request.PortfolioId,request.OperationId,request.InputSha256,cancellation);
            if(replay is not null) return replay;
            Require(request.ExpectedFinancialRevision==authority.Revision,FinancialReasons.RevisionConflict,"Financial state changed since preparation.");
            Require(request.ExpiresAtUtc>DateTime.UtcNow,FinancialReasons.TimeExpired,"Request expired before acquiring financial authority.");
            return await apply(db,authority,cancellation);
        },token); }
        catch(FunctionCommitOutcomeUnknownException)
        {
            using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                var replay=await new PortfolioFinancialDbContext(transactions).ReadOperationAsync<T>(request.PortfolioId,request.OperationId,request.InputSha256,recovery.Token);
                if(replay is not null) return replay;
            }
            catch(Exception) { /* An unavailable receipt is not proof that COMMIT failed. */ }
            throw;
        }
    }

    static async Task<(ReservationSnapshot State,CapacityReservationRequest Request)?> ReadReservationAsync(
        EnlistedEventTransaction db,int portfolioId,Guid reservationId,CancellationToken token)
    {
        var rows=await db.QueryAsync("""
            SELECT r.version,r.status,r.strategy_units,r.filled_units,r.cancelled_units,r.remaining_units,
              r.requirements->>'ContentHash',r.expires_at_utc,r.execution_id,
              coalesce((SELECT max(l.execution_revision) FROM portfolio_financial.capacity_lifecycle l WHERE l.reservation_id=r.reservation_id),0),r.request::text,r.closed_units
            FROM portfolio_financial.capacity_reservation r WHERE portfolio_id=$1 AND reservation_id=$2;
            """,[portfolioId,reservationId],r=>(new ReservationSnapshot(reservationId,r.GetInt64(0),(ReservationStatus)r.GetInt32(1),
                r.GetInt32(2),r.GetInt32(3),r.GetInt32(4),r.GetInt32(5),r.GetString(6),r.GetDateTime(7),r.IsDBNull(8)?null:r.GetGuid(8),r.GetInt64(9),r.GetInt32(11)),
                Decode<CapacityReservationRequest>(r.GetString(10))),token);
        return rows.Count==0?null:rows.Single();
    }

    internal static async Task<T?> ReadEvidenceAsync<T>(EnlistedEventTransaction db,Guid invocationId,CancellationToken token) where T:class,IEvent
    {
        var events=await db.QueryAsync("""
            SELECT e.eventstreamid,n.eventname,n.eventtypename,e.eventversion,e.eventdata::text,e.commandid,e.eventtimestamp::text,e.streamversion
            FROM event_log e JOIN event_name_id n ON n.eventnameid=e.eventnameid WHERE e.commandid=$1 ORDER BY e.eventversion LIMIT 65;
            """,[invocationId],r=>new EventLogReadModel(r.GetInt64(0),r.GetString(1),r.GetString(2),r.GetInt64(3),
                r.GetString(4),r.GetGuid(5),r.GetString(6),r.GetInt64(7)),token);
        Require(events.Count<=64,FinancialReasons.AuthorityDenied,"Upstream evidence exceeds its bounded event count.");
        var matches=events.Select(x=>x.ToDomainEvent()).OfType<T>().Take(2).ToArray();
        Require(matches.Length<=1,FinancialReasons.AuthorityDenied,"Conflicting committed upstream evidence.");
        return matches.SingleOrDefault();
    }

    static Task<int> AddUsageAsync(EnlistedEventTransaction db,int portfolioId,CapacityExposure exposure,
        (decimal Held,decimal Working,decimal Position) previous,
        (decimal Held,decimal Working,decimal Position) next,long revision,CancellationToken token)
    {
        if(exposure.Measure==CapacityMeasure.PositionSlots && exposure.Unit==CapacityUnit.Positions)
        {
            // One partly filled strategy still occupies a whole position slot after its unfilled remainder is cancelled.
            previous=PositionSlot(previous);
            next=PositionSlot(next);
        }
        return db.ExecuteAsync("""
        INSERT INTO portfolio_financial.capacity_usage(portfolio_id,scope_kind,scope_key,measure,unit,held,working,position,revision)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9) ON CONFLICT(portfolio_id,scope_kind,scope_key,measure,unit)
        DO UPDATE SET held=capacity_usage.held+EXCLUDED.held,working=capacity_usage.working+EXCLUDED.working,
          position=capacity_usage.position+EXCLUDED.position,revision=EXCLUDED.revision;
        """,[portfolioId,(int)exposure.ScopeKind,exposure.ScopeKey,(int)exposure.Measure,(int)exposure.Unit,
            Contribution(exposure.Amount,next.Held)-Contribution(exposure.Amount,previous.Held),
            Contribution(exposure.Amount,next.Working)-Contribution(exposure.Amount,previous.Working),
            Contribution(exposure.Amount,next.Position)-Contribution(exposure.Amount,previous.Position),revision],token);
    }

    static (decimal Held,decimal Working,decimal Position) PositionSlot((decimal Held,decimal Working,decimal Position) value)
        =>value.Position>0?(0,0,1):value.Working>0?(0,1,0):value.Held>0?(1,0,0):(0,0,0);

    // Quantize each absolute contribution, then subtract. Rounding each incremental fill independently leaves residual holds.
    static decimal Contribution(decimal amount,decimal fraction)=>decimal.Round(Math.Abs(amount)*fraction,10,MidpointRounding.AwayFromZero);

    static (decimal Held,decimal Working,decimal Position) Fractions(ReservationSnapshot state) =>
        (state.Status==ReservationStatus.Reserved?(decimal)state.RemainingUnits/state.StrategyUnits:0,
         state.Status is not (ReservationStatus.Reserved or ReservationStatus.Released or ReservationStatus.Expired or ReservationStatus.Filled)
            ?(decimal)state.RemainingUnits/state.StrategyUnits:0,(decimal)(state.FilledUnits-state.ClosedUnits)/state.StrategyUnits);

    static void CheckOutcome(IFinancialRequest request,IFinancialCompletedEvent completed,Guid eventId) =>
        Require(completed.Id==eventId && completed.OperationId==request.OperationId && completed.PortfolioId==request.PortfolioId &&
            completed.InputHash==request.InputSha256,FinancialReasons.InvalidContract,"Completion must describe this exact committed operation.");
}
