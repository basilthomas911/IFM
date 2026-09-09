using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record LedgerCommitInfo(long Revision,DateTime CommittedAtUtc,Guid EventId,IReadOnlyList<LedgerPostedTransaction> Items);
public delegate LedgerPostingPlan LedgerCalculation(LedgerPostingRequest request,LedgerPostingRule rule,
    decimal previousUnrealized,IReadOnlyList<PlannedLedgerLine>? reversalLines,decimal remainingReversibleAmount);

public interface IGeneralLedgerStore
{
    Task<T> PostAsync<T>(IFinancialRequest request,IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate,Func<LedgerCommitInfo,T> complete,CancellationToken token=default) where T:class,IFinancialCompletedEvent;
}

/// <summary>Single and bounded batch posting share the same financial fence and event transaction.</summary>
public sealed class GeneralLedgerStore(IPostgresEventTransaction transactions,
    FinancialDevelopmentPolicy? developmentPolicy = null) : IGeneralLedgerStore
{
    public async Task<T> PostAsync<T>(IFinancialRequest request,IReadOnlyList<PreparedLedgerPosting> items,
        LedgerCalculation calculate,Func<LedgerCommitInfo,T> complete,CancellationToken token=default) where T:class,IFinancialCompletedEvent
    {
        Require(items.Count is >=1 and <=100,FinancialReasons.InvalidContract,"Posting batch must contain 1–100 transactions.");
        Require(items.Select(x=>(x.Request.Source.System,x.Request.Source.SourceEventId,x.Request.TransactionKind)).Distinct().Count()==items.Count,
            FinancialReasons.SourceConflict,"Batch contains duplicate source identities.");
        try { return await transactions.ExecuteAsync(async (db,cancellation)=>
        {
            var replay=await ReadOperationAsync<T>(db,request.PortfolioId,request.OperationId,request.InputSha256,cancellation);
            if(replay is not null) return replay;
            var authority=await LockAuthorityAsync(db,request.PortfolioId,null,cancellation);
            // A duplicate may have committed while this attempt waited on the financial fence.
            replay=await ReadOperationAsync<T>(db,request.PortfolioId,request.OperationId,request.InputSha256,cancellation);
            if(replay is not null) return replay;
            Require(authority.Revision==request.ExpectedFinancialRevision,FinancialReasons.RevisionConflict,"Financial state changed since the request was prepared.");
            var revision=checked(authority.Revision+1); var now=DateTime.UtcNow; var eventId=Guid.NewGuid();
            Require(now<request.ExpiresAtUtc,FinancialReasons.TimeExpired,"Posting expired before acquiring financial authority.");
            var receipts=new List<LedgerPostedTransaction>();
            foreach(var prepared in items)
            {
                var item=prepared.Request;
                Require(item.BookId==authority.Book.BookId,FinancialReasons.AuthorityDenied,"Posting book does not match Portfolio authority.");
                Require(item.Source.SourceEventId!=Guid.Empty && !string.IsNullOrWhiteSpace(item.Source.System) &&
                    !string.IsNullOrWhiteSpace(item.Source.SourceContentHash),FinancialReasons.InvalidContract,"Qualified source identity/hash is required.");
                await ValidateFundSourcesAsync(db,authority.Book,item.FundId,false,cancellation);
                var previous=await db.QueryAsync("""
                    SELECT source_content_hash,operation_id FROM portfolio_financial.financial_source_receipt
                    WHERE book_id=$1 AND source_system=$2 AND source_event_key=$3 AND posting_purpose=$4;
                    """,[item.BookId,item.Source.System,item.Source.SourceEventId.ToString("N"),item.TransactionKind.ToString()],r=>(Hash:r.GetString(0),Operation:r.GetGuid(1)),cancellation);
                if(previous.Count>0) throw new FinancialOperationException(previous[0].Hash==item.Source.SourceContentHash?FinancialReasons.AlreadyPosted:FinancialReasons.SourceConflict,
                    "Source has already been committed.",FinancialCommitDisposition.NoNewMutation,previous[0].Operation);
                var period=await db.ScalarAsync("""
                    SELECT state FROM portfolio_financial.ledger_period WHERE book_id=$1 AND start_date<=$2 AND end_date>=$2;
                    """,[item.BookId,item.AccountingDate],cancellation);
                Require(period is string state && state=="Open",FinancialReasons.ClosedPeriod,"Accounting date is not in an open period.");
                var ruleJson=await db.ScalarAsync("""
                    SELECT payload::text FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND rule_id=$2 AND version=$3
                    AND status='Active' AND effective_from<=$4 AND (effective_to IS NULL OR effective_to>=$4);
                    """,[item.BookId,item.PostingRule.RuleId,(long)item.PostingRule.Version,item.AccountingDate],cancellation);
                Require(ruleJson is string,FinancialReasons.InvalidAccount,"Exact posting rule is not active.");
                var rule=Decode<LedgerPostingRule>((string)ruleJson!);
                var valuation=await db.QueryAsync("""
                    SELECT amount,source_sequence FROM portfolio_financial.ledger_valuation WHERE book_id=$1 AND fund_id=$2 AND position_key=$3;
                    """,[item.BookId,item.FundId,PositionKey(item)],r=>(Amount:r.GetDecimal(0),Sequence:r.GetInt64(1)),cancellation);
                if(item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    Require(valuation.Count==0 || item.Source.SourceSequence>valuation[0].Sequence,FinancialReasons.SourceConflict,"Valuation/realization source is stale.");
                IReadOnlyList<PlannedLedgerLine>? reversal=null; decimal remaining=0;
                if(item.TransactionKind==LedgerTransactionKind.Reversal)
                {
                    var journalOwner=await db.ScalarAsync("SELECT fund_id FROM portfolio_financial.ledger_journal WHERE book_id=$1 AND journal_id=$2;",[item.BookId,item.RelatedJournalId],cancellation);
                    Require(journalOwner is int owner && owner==item.FundId,FinancialReasons.AuthorityDenied,"Original journal belongs to a different Fund/book.");
                    reversal=await ReadJournalLines(db,item.RelatedJournalId!.Value,cancellation);
                    var already=Convert.ToDecimal(await db.ScalarAsync("""
                        SELECT coalesce(sum(t.amount),0) FROM portfolio_financial.ledger_journal j JOIN portfolio_financial.ledger_transaction t ON t.transaction_id=j.transaction_id
                        WHERE j.reversal_journal_id=$1;
                        """,[item.RelatedJournalId],cancellation));
                    remaining=reversal.Sum(x=>x.Debit)-already;
                }
                var plan=calculate(item,rule,valuation.FirstOrDefault().Amount,reversal,remaining);
                Require((item.CapacityReservationId is null)==(item.FundingComponent==CapacityFundingComponent.Undefined),
                    FinancialReasons.InvalidContract,"Reservation and funding component must be supplied together.");
                if(item.TransactionKind==LedgerTransactionKind.OpeningBalance)
                {
                    Require(developmentPolicy?.IsDevelopmentEnvironment == true && authority.Book.Environment=="Emulator",
                        FinancialReasons.AuthorityDenied,"Opening capital is allowed only on a Development host for an Emulator book.");
                    Require(authority.State=="Importing" && !authority.Book.MigrationQualified,FinancialReasons.AuthorityDenied,
                        "Opening balances are restricted to an unqualified book's explicit import phase.");
                    Require(item.Source.System=="DevelopmentOpeningCapital",FinancialReasons.AuthorityDenied,
                        "Opening capital must be explicitly identified as DevelopmentOpeningCapital, never as a confirmed external deposit.");
                }
                var discretionary=plan.IsDiscretionarySpending;
                if(discretionary)
                {
                    Require(authority.State=="Active",FinancialReasons.AuthorityRevoked,"New spending is disabled.");
                    await ValidateFundSourcesAsync(db,authority.Book,item.FundId,true,cancellation);
                    Require(await AvailableCash(db,item.BookId,item.FundId,cancellation)>=item.Amount,
                        FinancialReasons.InsufficientCash,"Cash is unavailable after existing withdrawals and trade commitments.");
                }
                if(item.CounterpartyFundId is { } other && item.TransactionKind==LedgerTransactionKind.FundTransfer)
                    await ValidateFundSourcesAsync(db,authority.Book,other,false,cancellation);
                var obligation=await ApplyObligation(db,request,item,revision,cancellation);
                var journalId=plan.Lines.Count==0?null:prepared.JournalId;
                Require(plan.Lines.Count==0 || journalId is >0,FinancialReasons.InvalidContract,"Generated journal identity is missing.");
                await db.ExecuteAsync("""
                    INSERT INTO portfolio_financial.ledger_transaction(transaction_id,book_id,portfolio_id,fund_id,operation_id,item_ordinal,kind,
                    source_system,source_event_key,source_hash,amount,currency,accounting_date,value_date,settlement_date,obligation_id,related_journal_id,payload)
                    VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,'USD',$12,$13,$14,$15,$16,$17);
                    """,[prepared.TransactionId,item.BookId,request.PortfolioId,item.FundId,request.OperationId,receipts.Count+1,(int)item.TransactionKind,
                        item.Source.System,item.Source.SourceEventId.ToString("N"),item.Source.SourceContentHash,item.Amount,item.AccountingDate,item.ValueDate,
                        item.SettlementDate,obligation,item.RelatedJournalId,Json(item)],cancellation);
                // Hash is supplied by the canonical domain model through the immutable request/rule/line plan.
                var journalHash=journalId is null?null:plan.ContentHash;
                Require(journalId is null || journalHash?.Length==64,FinancialReasons.InvalidContract,"Canonical journal hash is required.");
                if(journalId is not null)
                {
                    await db.ExecuteAsync("""
                        INSERT INTO portfolio_financial.ledger_journal(journal_id,transaction_id,book_id,portfolio_id,fund_id,operation_id,accounting_date,value_date,settlement_date,
                        kind,source_hash,rule_id,rule_version,reversal_journal_id,committed_at_utc,financial_revision,journal_hash)
                        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17);
                        """,[journalId,prepared.TransactionId,item.BookId,request.PortfolioId,item.FundId,request.OperationId,item.AccountingDate,item.ValueDate,item.SettlementDate,
                            (int)item.TransactionKind,item.Source.SourceContentHash,rule.RuleId,(long)rule.Version,
                            item.TransactionKind==LedgerTransactionKind.Reversal?item.RelatedJournalId:null,now,revision,journalHash],cancellation);
                    foreach(var line in plan.Lines.OrderBy(x=>x.Account.AccountId).ThenBy(x=>x.FundId).ThenBy(x=>x.Ordinal))
                    {
                        if(line.FundId is { } lineFund)
                            await ValidateFundSourcesAsync(db,authority.Book,lineFund,false,cancellation);
                        await InsertLine(db,item.BookId,journalId.Value,line,revision,item.TransactionKind==LedgerTransactionKind.Reversal,cancellation);
                    }
                }
                if(item.CapacityReservationId is not null)
                    await RecordFundingSettlement(db,item,prepared.TransactionId,journalId,cancellation);
                await db.ExecuteAsync("""
                    INSERT INTO portfolio_financial.financial_source_receipt(book_id,source_system,source_event_key,posting_purpose,source_content_hash,operation_id,journal_id)
                    VALUES($1,$2,$3,$4,$5,$6,$7);
                    """,[item.BookId,item.Source.System,item.Source.SourceEventId.ToString("N"),item.TransactionKind.ToString(),item.Source.SourceContentHash,request.OperationId,journalId],cancellation);
                if(item.TransactionKind is LedgerTransactionKind.Valuation or LedgerTransactionKind.RealizedPnl)
                    await db.ExecuteAsync("""
                        INSERT INTO portfolio_financial.ledger_valuation(book_id,fund_id,position_key,amount,source_sequence,revision) VALUES($1,$2,$3,$4,$5,$6)
                        ON CONFLICT(book_id,fund_id,position_key) DO UPDATE SET amount=EXCLUDED.amount,source_sequence=EXCLUDED.source_sequence,revision=EXCLUDED.revision;
                        """,[item.BookId,item.FundId,PositionKey(item),item.TransactionKind==LedgerTransactionKind.RealizedPnl?0m:item.Amount,item.Source.SourceSequence,revision],cancellation);
                if(plan.IsActualFinancialFact && await AvailableCash(db,item.BookId,item.FundId,cancellation)<0)
                    await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET operating_state='Overdrawn' WHERE portfolio_id=$1;",[request.PortfolioId],cancellation);
                receipts.Add(new() { Ordinal=receipts.Count+1,TransactionId=prepared.TransactionId,JournalId=journalId,JournalHash=journalHash,Source=item.Source,ObligationId=obligation });
            }
            var completed=complete(new(revision,now,eventId,receipts));
            Require(completed.Id==eventId && completed.OperationId==request.OperationId && completed.InputHash==request.InputSha256,
                FinancialReasons.RequestMismatch,"Completion factory did not preserve committed identity.");
            await SaveOutcomeAsync(db,request,completed,revision,false,cancellation);
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.ledger_posting_receipt(portfolio_id,operation_id,execution_id,input_hash,receipt_type,manifest,completion_event_id,committed_at_utc,financial_revision,payload)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
                """,[request.PortfolioId,request.OperationId,request.Subject.EntityId,request.InputSha256,typeof(T).FullName!,Json(receipts),eventId,now,revision,Json(completed)],cancellation);
            return completed;
        },token).ConfigureAwait(false); }
        catch(FunctionCommitOutcomeUnknownException)
        {
            using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try { var receipt=await transactions.ExecuteAsync((db,ct)=>ReadOperationAsync<T>(db,request.PortfolioId,request.OperationId,request.InputSha256,ct),recovery.Token); if(receipt is not null) return receipt; }
            catch { /* Failure to reconcile cannot establish rollback. */ }
            throw;
        }
    }

    internal static async Task<decimal> AvailableCash(EnlistedEventTransaction db,int bookId,int fundId,CancellationToken token)
    {
        var cash=Convert.ToDecimal(await db.ScalarAsync("""
            SELECT coalesce(sum(b.balance),0) FROM portfolio_financial.ledger_account_balance b
            WHERE b.book_id=$1 AND b.fund_id=$2 AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a
              WHERE a.book_id=b.book_id AND a.account_id=b.account_id AND a.category='Cash');
            """,[bookId,fundId],token));
        var pending=Convert.ToDecimal(await db.ScalarAsync("SELECT coalesce(sum(amount),0) FROM portfolio_financial.financial_encumbrance WHERE book_id=$1 AND fund_id=$2 AND status='Pending';",[bookId,fundId],token));
        var held=Convert.ToDecimal(await db.ScalarAsync("""
            WITH paid AS (
              SELECT f.reservation_id,f.component,sum(greatest(0,f.cash_paid-coalesce((
                SELECT sum(e.debit-e.credit) FROM portfolio_financial.ledger_journal reversal
                  JOIN portfolio_financial.ledger_entry e ON e.journal_id=reversal.journal_id
                WHERE reversal.reversal_journal_id=j.journal_id AND e.fund_id=$2 AND EXISTS(
                  SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id AND a.account_id=e.account_id AND a.category='Cash')),0))) amount
              FROM portfolio_financial.capacity_funding_receipt f JOIN portfolio_financial.ledger_journal j ON j.transaction_id=f.transaction_id
              WHERE j.book_id=$1 AND j.fund_id=$2 GROUP BY f.reservation_id,f.component),
            holds AS (
              SELECT r.reservation_id,(r.remaining_units+r.filled_units-r.closed_units)::numeric/r.strategy_units fraction,
                coalesce((r.requirements->>'SettlementCash')::numeric,0) settlement,
                coalesce((r.requirements->>'MarginFunding')::numeric,0) margin,
                coalesce((r.requirements->>'FeeReserve')::numeric,0) fee,
                coalesce((r.requirements->>'VariationReserve')::numeric,0) variation
              FROM portfolio_financial.capacity_reservation r WHERE r.book_id=$1 AND r.fund_id=$2 AND r.status NOT IN (8,9))
            SELECT coalesce(sum(ceil(100*(greatest(0,h.settlement*h.fraction-coalesce(s.amount,0))+
              greatest(0,h.margin*h.fraction-coalesce(m.amount,0))+greatest(0,h.fee*h.fraction-coalesce(f.amount,0))+h.variation*h.fraction))/100),0)
            FROM holds h LEFT JOIN paid s ON s.reservation_id=h.reservation_id AND s.component=1
              LEFT JOIN paid f ON f.reservation_id=h.reservation_id AND f.component=2
              LEFT JOIN paid m ON m.reservation_id=h.reservation_id AND m.component=3;
            """,[bookId,fundId],token));
        return cash-pending-held;
    }

    static async Task RecordFundingSettlement(EnlistedEventTransaction db,LedgerPostingRequest item,long transactionId,long? journalId,CancellationToken token)
    {
        Require(journalId is not null && (item.FundingComponent==CapacityFundingComponent.EntryFees && item.TransactionKind==LedgerTransactionKind.Commission ||
            item.FundingComponent is CapacityFundingComponent.SettlementCash or CapacityFundingComponent.MarginFunding && item.TransactionKind==LedgerTransactionKind.TradeSettlement),
            FinancialReasons.InvalidContract,"Funding clearance requires a posted entry fee, settlement or margin cash outflow.");
        var scope=await db.QueryAsync("""
            SELECT order_id,execution_id,request::text FROM portfolio_financial.capacity_reservation
            WHERE reservation_id=$1 AND book_id=$2 AND fund_id=$3;
            """,[item.CapacityReservationId,item.BookId,item.FundId],r=>(Order:r.GetInt32(0),Execution:r.IsDBNull(1)?(Guid?)null:r.GetGuid(1),Request:Decode<CapacityReservationRequest>(r.GetString(2))),token);
        Require(scope.Count==1 && scope[0].Execution is { } execution && Guid.TryParse(item.Source.SourceEntityId,out var sourceExecution) && sourceExecution==execution &&
            item.Source.OrderId==scope[0].Order && item.Source.TradeId is { } trade && scope[0].Request.TradeIds.Contains(trade),
            FinancialReasons.AuthorityDenied,"Funding source must identify the exact consumed execution, order, trade and Fund.");
        var paid=Convert.ToDecimal(await db.ScalarAsync("""
            SELECT coalesce(sum(e.credit-e.debit),0) FROM portfolio_financial.ledger_entry e WHERE e.journal_id=$1 AND e.fund_id=$2
            AND EXISTS(SELECT 1 FROM portfolio_financial.ledger_account a WHERE a.book_id=e.book_id AND a.account_id=e.account_id AND a.category='Cash');
            """,[journalId,item.FundId],token));
        Require(paid>0,FinancialReasons.InvalidContract,"Funding clearance requires an actual cash outflow in the committed journal.");
        await db.ExecuteAsync("INSERT INTO portfolio_financial.capacity_funding_receipt(transaction_id,reservation_id,component,cash_paid) VALUES($1,$2,$3,$4);",
            [transactionId,item.CapacityReservationId,(int)item.FundingComponent,paid],token);
    }

    static async Task<Guid?> ApplyObligation(EnlistedEventTransaction db,IFinancialRequest request,LedgerPostingRequest item,long revision,CancellationToken token)
    {
        if(item.TransactionKind==LedgerTransactionKind.WithdrawalRequested)
        {
            var id=Guid.NewGuid();
            await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.financial_encumbrance(obligation_id,portfolio_id,book_id,fund_id,source_identity,kind,amount,currency,status,revision,evidence)
                VALUES($1,$2,$3,$4,$5,'Withdrawal',$6,'USD','Pending',$7,$8);
                """,[id,request.PortfolioId,item.BookId,item.FundId,item.Source.SourceEventId.ToString("N"),item.Amount,revision,Json(item.MovementEvidence)],token);
            return id;
        }
        if(item.TransactionKind is LedgerTransactionKind.WithdrawalSettled or LedgerTransactionKind.WithdrawalCancelled)
        {
            var changed=await db.ExecuteAsync("""
                UPDATE portfolio_financial.financial_encumbrance SET amount=amount-$1,status=CASE WHEN amount=$1 THEN $2 ELSE 'Pending' END,revision=$3,evidence=$4
                WHERE obligation_id=$5 AND portfolio_id=$6 AND book_id=$7 AND fund_id=$8 AND status='Pending' AND amount>=$1;
                """,[item.Amount,item.TransactionKind==LedgerTransactionKind.WithdrawalSettled?"Settled":"Cancelled",revision,Json(item.MovementEvidence),
                    item.RelatedObligationId,request.PortfolioId,item.BookId,item.FundId],token);
            Require(changed==1,FinancialReasons.SourceConflict,"Withdrawal obligation is missing, settled or insufficient.");
            return item.RelatedObligationId;
        }
        return null;
    }

    static async Task InsertLine(EnlistedEventTransaction db,int bookId,long journalId,PlannedLedgerLine line,long revision,bool reversal,CancellationToken token)
    {
        var valid=await db.ScalarAsync("""
            SELECT fund_dimension_required FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 AND version=$3
            AND (status='Active' OR ($4 AND status='Retired'));
            """,[bookId,line.Account.AccountId,line.Account.Version,reversal],token);
        Require(valid is bool fundRequired && (!fundRequired || line.FundId is >0),FinancialReasons.InvalidAccount,"Account version is inactive or missing its required Fund dimension.");
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.ledger_entry(journal_id,ordinal,book_id,account_id,account_version,fund_id,debit,credit,currency,source_line_reference,order_id,trade_id)
            VALUES($1,$2,$3,$4,$5,$6,$7,$8,'USD',$9,$10,$11);
            """,[journalId,line.Ordinal,bookId,line.Account.AccountId,line.Account.Version,line.FundId,line.Debit,line.Credit,line.SourceLineReference,line.OrderId,line.TradeId],token);
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.ledger_account_balance(book_id,account_id,fund_id,currency,debit_total,credit_total,balance,revision)
            VALUES($1,$2,$3,'USD',$4,$5,$4-$5,$6)
            ON CONFLICT(book_id,account_id,fund_id,currency) DO UPDATE SET debit_total=portfolio_financial.ledger_account_balance.debit_total+EXCLUDED.debit_total,
            credit_total=portfolio_financial.ledger_account_balance.credit_total+EXCLUDED.credit_total,
            balance=portfolio_financial.ledger_account_balance.balance+EXCLUDED.balance,revision=EXCLUDED.revision;
            """,[bookId,line.Account.AccountId,line.FundId,line.Debit,line.Credit,revision],token);
    }
    static string PositionKey(LedgerPostingRequest item)=>$"{item.Source.OrderId}:{item.Source.TradeId}";
    static Task<IReadOnlyList<PlannedLedgerLine>> ReadJournalLines(EnlistedEventTransaction db,long journalId,CancellationToken token)=>db.QueryAsync("""
        SELECT ordinal,account_id,account_version,fund_id,debit,credit,order_id,trade_id,source_line_reference FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal;
        """,[journalId],r=>new PlannedLedgerLine(r.GetInt32(0),new(r.GetInt32(1),r.GetInt64(2)),r.IsDBNull(3)?null:r.GetInt32(3),r.GetDecimal(4),r.GetDecimal(5),
            r.IsDBNull(6)?null:r.GetInt32(6),r.IsDBNull(7)?null:r.GetInt32(7),r.GetString(8)),token);
}
