using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface ILedgerConfigurationStore
{
    Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt,LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult,string> hash,CancellationToken token=default);
}

/// <summary>Configuration, period control and independent reconciliation share the financial admission fence.</summary>
public sealed class LedgerConfigurationStore(IPostgresEventTransaction transactions,FinancialDevelopmentPolicy? developmentPolicy=null):ILedgerConfigurationStore
{
    public async Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt,LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult,string> hash,CancellationToken token=default)
    {
        try { return await transactions.ExecuteAsync(async(db,ct)=>
        {
            var replay=await ReadOperationAsync<LedgerConfigurationCompletedEvent>(db,request.PortfolioId,request.OperationId,request.InputSha256,ct);
            if(replay is not null) return replay;
            await db.ScalarAsync("SELECT pg_advisory_xact_lock(34100,$1);",[request.PortfolioId],ct);
            replay=await ReadOperationAsync<LedgerConfigurationCompletedEvent>(db,request.PortfolioId,request.OperationId,request.InputSha256,ct);
            if(replay is not null) return replay;
            var body=request.Body;
            if(body.Action==LedgerConfigurationAction.CreateBook)
            {
                Require(request.ExpectedFinancialRevision==0 && body.Book is { MigrationQualified:false } &&
                    body.Book.PortfolioId==request.PortfolioId && body.Book.BookId==body.BookId && body.Book.Funds.All(x=>!x.CanSpend),
                    FinancialReasons.AuthorityDenied,"New books require explicit migration qualification before spending is enabled.");
                foreach(var fund in body.Book!.Funds)
                {
                    await CheckSource($"Portfolio.{request.PortfolioId}",fund.PortfolioStreamVersion);
                    await CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}",fund.FundStreamVersion);
                }
                await CreateBookAsync(db,body.Book!,body.Accounts,body.Rules,body.PeriodStart,body.PeriodEnd,body.PeriodId,ct);
            }
            var current=await LockAuthorityAsync(db,request.PortfolioId,request.ExpectedFinancialRevision,ct);
            Require(current.Book.BookId==body.BookId,FinancialReasons.AuthorityDenied,"Book does not belong to this Portfolio.");
            Require(DateTime.UtcNow<request.ExpiresAtUtc,FinancialReasons.TimeExpired,"Configuration deadline expired before commit.");
            var revision=checked(current.Revision+1);
            var state=current.State;
            Guid? reconciliationId=null;
            switch(body.Action)
            {
                case LedgerConfigurationAction.CreateBook: break;
                case LedgerConfigurationAction.QualifyDevelopmentBook:
                    Require(developmentPolicy?.IsDevelopmentEnvironment==true && current.Book.Environment=="Emulator" && !current.Book.MigrationQualified &&
                        current.State=="Importing" && current.Book.Funds.All(x=>!x.CanSpend) && body.Book is not null &&
                        FinancialCanonicalHash.Compute(body.Book)==FinancialCanonicalHash.Compute(current.Book),FinancialReasons.AuthorityDenied,"Only the exact unqualified development book can be qualified.");
                    await RequireReconciled(db,body,ct);
                    foreach(var fund in current.Book.Funds.OrderBy(x=>x.FundId))
                    {
                        await CheckSource($"Portfolio.{request.PortfolioId}",fund.PortfolioStreamVersion);
                        await CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}",fund.FundStreamVersion);
                        Require(await db.ScalarAsync("""
                            SELECT empty_verified FROM portfolio_financial.legacy_writer_scope
                            WHERE fund_id=$1 AND portfolio_id=$2 AND qualification_id=$3 AND state='Fenced' FOR SHARE;
                            """,[fund.FundId,request.PortfolioId,current.Book.AccountingEntityId],ct) is true,
                            FinancialReasons.AuthorityDenied,"Fresh legacy scope has not been fenced and independently verified empty.");
                    }
                    state="NeedsRefresh";
                    var manifest=new { Mode="DevelopmentFreshScope",Book=current.Book,body.ReconciliationId,body.SourceCut,
                        LegacyRows=0,WriterFence=current.Book.AccountingEntityId,FinancialRevision=revision };
                    await db.ExecuteAsync("""
                        INSERT INTO portfolio_financial.ledger_migration(migration_id,portfolio_id,mappings,mode,source_watermark,destination_watermark,
                            manifest_hash,verified_totals,writer_fence,cutover_state)
                        VALUES($1,$2,$3,'DevelopmentFreshScope',$4,$5,$6,$7,$8,'Qualified');
                        """,[request.OperationId,request.PortfolioId,Json(current.Book.Funds.Select(x=>new { x.FundId,LegacySource="VerifiedAbsent" }).ToArray()),
                            body.SourceCut,$"FinancialRevision:{revision}",FinancialCanonicalHash.Compute(manifest),Json(manifest),current.Book.AccountingEntityId.ToString("N")],ct);
                    await db.ExecuteAsync("""
                        UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2,migration_state='QualifiedDevelopmentFresh',operating_state=$3 WHERE portfolio_id=$1;
                        """,[request.PortfolioId,Json(current.Book with { MigrationQualified=true }),state],ct);
                    break;
                case LedgerConfigurationAction.AddAccountVersion:
                    foreach(var account in body.Accounts)
                    {
                        var last=Convert.ToInt64(await db.ScalarAsync("SELECT coalesce(max(version),0) FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2;",[body.BookId,account.AccountId],ct));
                        Require(last==body.ExpectedVersion && account.Version==last+1,FinancialReasons.RevisionConflict,"Account version changed.");
                        var category=await db.ScalarAsync("SELECT category FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 ORDER BY version DESC LIMIT 1;",[body.BookId,account.AccountId],ct);
                        Require(category is null || Equals(category,account.Category),FinancialReasons.InvalidContract,"An account's economic category cannot change across versions.");
                        await db.ExecuteAsync("""
                            INSERT INTO portfolio_financial.ledger_account(book_id,account_id,version,category,normal_side,currency,fund_dimension_required,status,content_hash)
                            VALUES($1,$2,$3,$4,$5,'USD',$6,'Active',$7);
                            """,[body.BookId,account.AccountId,account.Version,account.Category,(int)account.NormalSide,account.FundDimensionRequired,account.ContentHash],ct);
                    }
                    break;
                case LedgerConfigurationAction.AddPostingRuleVersion:
                    foreach(var rule in body.Rules)
                    {
                        var last=Convert.ToInt64(await db.ScalarAsync("SELECT coalesce(max(version),0) FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND rule_id=$2;",[body.BookId,rule.RuleId],ct));
                        Require(last==body.ExpectedVersion && rule.Version==last+1,FinancialReasons.RevisionConflict,"Posting rule version changed.");
                        foreach(var account in new[] { rule.Debit,rule.Credit,rule.ValuationAsset,rule.UnrealizedPnl }.OfType<LedgerAccountBinding>())
                            Require(await db.ScalarAsync("SELECT status FROM portfolio_financial.ledger_account WHERE book_id=$1 AND account_id=$2 AND version=$3;",[body.BookId,account.AccountId,account.Version],ct) is "Active",
                                FinancialReasons.InvalidContract,"Posting rules require active exact account versions.");
                        await db.ExecuteAsync("""
                            INSERT INTO portfolio_financial.ledger_posting_rule(book_id,rule_id,version,kind,payload,content_hash,status,effective_from)
                            VALUES($1,$2,$3,$4,$5,$6,'Active',$7);
                            """,[body.BookId,rule.RuleId,rule.Version,(int)rule.Kind,Json(rule),rule.ContentHash,body.PeriodStart],ct);
                    }
                    break;
                case LedgerConfigurationAction.RetireAccount:
                    foreach(var account in body.Accounts)
                    {
                        Require(await db.ScalarAsync("""
                            SELECT count(*) FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND status='Active'
                            AND (payload->'Debit'->>'AccountId'=$2 OR payload->'Credit'->>'AccountId'=$2
                            OR payload->'ValuationAsset'->>'AccountId'=$2 OR payload->'UnrealizedPnl'->>'AccountId'=$2);
                            """,[body.BookId,account.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture)],ct) is 0L,
                            FinancialReasons.AuthorityDenied,"Retire dependent posting rules before retiring an account.");
                        var changed=await db.ExecuteAsync("UPDATE portfolio_financial.ledger_account SET status='Retired' WHERE book_id=$1 AND account_id=$2 AND version=$3 AND status='Active';",[body.BookId,account.AccountId,account.Version],ct);
                        Require(changed==1,FinancialReasons.RevisionConflict,"Account version is not active.");
                    }
                    break;
                case LedgerConfigurationAction.RetirePostingRule:
                    foreach(var rule in body.Rules)
                        Require(await db.ExecuteAsync("UPDATE portfolio_financial.ledger_posting_rule SET status='Retired' WHERE book_id=$1 AND rule_id=$2 AND version=$3 AND status='Active';",[body.BookId,rule.RuleId,rule.Version],ct)==1,
                            FinancialReasons.RevisionConflict,"Posting rule version is not active.");
                    break;
                case LedgerConfigurationAction.OpenPeriod:
                    await db.ExecuteAsync("""
                        INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
                        VALUES($1,$2,$3,$4,'Open',1,$5,$6);
                        """,[body.BookId,body.PeriodId,body.PeriodStart,body.PeriodEnd,body.SourceCut,Json(new { request.OperationId,body.Reason })],ct);
                    break;
                case LedgerConfigurationAction.ClosePeriod:
                case LedgerConfigurationAction.ReopenPeriod:
                    var closing=body.Action==LedgerConfigurationAction.ClosePeriod;
                    if(closing) await RequireReconciled(db,body,ct);
                    Require(await db.ExecuteAsync("""
                        UPDATE portfolio_financial.ledger_period SET state=$4,revision=revision+1,source_cut=$5,evidence=$6
                        WHERE book_id=$1 AND period_id=$2 AND revision=$3 AND state=$7;
                        """,[body.BookId,body.PeriodId,body.ExpectedVersion,closing?"Closed":"Open",body.SourceCut,
                            Json(new { request.OperationId,body.Reason,body.ReconciliationId }),closing?"Open":"Closed"],ct)==1,
                        FinancialReasons.RevisionConflict,"Period state/version changed.");
                    break;
                case LedgerConfigurationAction.RefreshAuthority:
                    if(body.Book?.Funds.Any(x=>x.CanSpend)==true)
                        Require(await db.ScalarAsync("""
                            SELECT count(*) FROM portfolio_financial.capacity_usage WHERE portfolio_id=$1 AND scope_kind=$2
                              AND scope_key NOT LIKE 'U1:%' AND (held<>0 OR working<>0 OR position<>0);
                            """,[request.PortfolioId,(int)CapacityScopeKind.Underlying],ct) is 0L,
                            FinancialReasons.AuthorityDenied,"Legacy contract-specific underlying obligations require reconciliation before new product-wide authority.");
                    Require(body.Book is { } updated && updated.BookId==current.Book.BookId && updated.PortfolioId==request.PortfolioId &&
                        updated.AccountingEntityId==current.Book.AccountingEntityId && updated.Environment==current.Book.Environment &&
                        updated.ExecutionAccountReference==current.Book.ExecutionAccountReference && updated.Currency=="USD" &&
                        updated.MigrationQualified==current.Book.MigrationQualified,
                        FinancialReasons.AuthorityDenied,"Refresh cannot replace book ownership or migration qualification.");
                    Require(body.Book!.Funds.Select(x=>x.FundId).Order().SequenceEqual(current.Book.Funds.Select(x=>x.FundId).Order()),
                        FinancialReasons.AuthorityDenied,"Authority refresh cannot change qualified book membership; a separate scoped qualification is required.");
                    var epoch=Convert.ToInt64(await db.ScalarAsync("SELECT authority_epoch FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[request.PortfolioId],ct));
                    Require(body.Book!.AuthorityEpoch==epoch && body.Book.Funds.All(x=>x.Reference.AuthorityEpoch==epoch),FinancialReasons.AuthorityRevoked,"Authority epoch changed.");
                    foreach(var fund in body.Book.Funds) await ValidateFundSourcesAsync(db,body.Book,fund.FundId,fund.CanSpend,ct);
                    foreach(var fund in body.Book.Funds)
                    {
                        await CheckSource($"Portfolio.{request.PortfolioId}",fund.PortfolioStreamVersion);
                        await CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}",fund.FundStreamVersion);
                        if(fund.CanSpend) await CheckSource($"PortfolioFinancialPolicy.{request.PortfolioId}.{fund.Reference.PolicyId}",fund.PolicyStreamVersion);
                    }
                    state=current.State is "Overdrawn" or "NeedsReconciliation"?current.State:body.Book.MigrationQualified?"Active":"Importing";
                    await db.ExecuteAsync("""
                        UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2,source_watermark=$3,valuation_watermark=$4,operating_state=$5 WHERE portfolio_id=$1;
                        """,[request.PortfolioId,Json(body.Book),body.Book.SourceWatermark,body.Book.ValuationWatermark,state],ct);
                    break;
                case LedgerConfigurationAction.Reconcile:
                    reconciliationId=request.OperationId;
                    var result=await ReconstructAsync(db,body.BookId,reconciliationId.Value,current.Revision,body.SourceCut,ct);
                    result=result with { ContentHash=hash(result) };
                    await db.ExecuteAsync("""
                        INSERT INTO portfolio_financial.ledger_reconciliation(reconciliation_id,book_id,portfolio_id,fund_id,source_cut,counts,totals,content_hash,differences,resolution_links,status)
                        VALUES($1,$2,$3,NULL,$4,$5,$6,$7,$8,'[]',$9);
                        """,[result.ReconciliationId,body.BookId,request.PortfolioId,body.SourceCut,Json(new { result.JournalCount,result.EntryCount,result.FinancialRevision }),
                            Json(new { result.Debits,result.Credits }),result.ContentHash,Json(result.Differences),result.Differences.Length==0 && result.Debits==result.Credits?"Matched":"Mismatch"],ct);
                    if(result.Differences.Length!=0 || result.Debits!=result.Credits)
                    {
                        state="NeedsReconciliation";
                        await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET operating_state=$2 WHERE portfolio_id=$1;",[request.PortfolioId,state],ct);
                    }
                    else if(current.State is "Overdrawn" or "NeedsReconciliation")
                    {
                        var solvent=true;
                        foreach(var fund in current.Book.Funds)
                            if(await GeneralLedgerStore.AvailableCash(db,current.Book.BookId,fund.FundId,ct)<0) solvent=false;
                        // A matching ledger is not a fresh mandate, valuation or admission authorization.
                        // Explicit authority refresh must follow reconciliation before a qualified book can spend again.
                        state=!solvent ? "Overdrawn" : current.Book.MigrationQualified ? "NeedsRefresh" : "Importing";
                        await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET operating_state=$2 WHERE portfolio_id=$1;",[request.PortfolioId,state],ct);
                    }
                    break;
                default: throw new FinancialOperationException(FinancialReasons.InvalidContract,"Unknown configuration action.");
            }
            var completed=complete(new(request.OperationId,body.BookId,body.Action,revision,DateTime.UtcNow,reconciliationId,state));
            await SaveOutcomeAsync(db,request,completed,revision,false,ct);
            return completed;

            async Task CheckSource(string stream,long version)
            {
                var actual=await db.ScalarAsync("SELECT currentversion FROM event_stream_id WHERE eventstream=$1 FOR SHARE;",[stream],ct);
                Require(version>0 && actual is not null && Convert.ToInt64(actual)==version,FinancialReasons.AuthorityRevoked,"Source changed while preparing authority refresh.");
            }
        },token); }
        catch(FunctionCommitOutcomeUnknownException)
        {
            using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                var receipt=await new PortfolioFinancialDbContext(transactions).ReadOperationAsync<LedgerConfigurationCompletedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,recovery.Token);
                if(receipt is not null) return receipt;
            }
            catch(Exception) { /* The caller must reconcile the original identity after connectivity returns. */ }
            throw;
        }
    }

    static async Task RequireReconciled(EnlistedEventTransaction db,LedgerConfigurationRequest body,CancellationToken ct)
    {
        Require(body.ReconciliationId is not null,FinancialReasons.InvalidContract,"Period closure requires reconciliation evidence.");
        var matches=await db.ScalarAsync("""
            SELECT r.status='Matched' AND r.source_cut=$3 AND NOT EXISTS(
              SELECT 1 FROM portfolio_financial.ledger_journal j WHERE j.book_id=r.book_id AND j.financial_revision>(r.counts->>'FinancialRevision')::bigint)
            FROM portfolio_financial.ledger_reconciliation r WHERE r.book_id=$1 AND r.reconciliation_id=$2;
            """,[body.BookId,body.ReconciliationId,body.SourceCut],ct);
        Require(matches is true,FinancialReasons.AuthorityDenied,"Reconciliation is missing, mismatched or predates subsequent journal postings.");
    }

    static async Task<LedgerReconciliationResult> ReconstructAsync(EnlistedEventTransaction db,int bookId,Guid id,long revision,string cut,CancellationToken ct)
    {
        var totals=await db.QueryAsync("""
            SELECT count(DISTINCT j.journal_id),count(e.ordinal),coalesce(sum(e.debit),0),coalesce(sum(e.credit),0)
            FROM portfolio_financial.ledger_journal j LEFT JOIN portfolio_financial.ledger_entry e ON e.journal_id=j.journal_id WHERE j.book_id=$1;
            """,[bookId],r=>(Journals:r.GetInt64(0),Entries:r.GetInt64(1),Debits:r.GetDecimal(2),Credits:r.GetDecimal(3)),ct);
        var differences=await db.QueryAsync("""
            WITH actual AS (SELECT account_id,fund_id,sum(debit) d,sum(credit) c FROM portfolio_financial.ledger_entry WHERE book_id=$1 GROUP BY account_id,fund_id),
            recorded AS (SELECT account_id,fund_id,debit_total d,credit_total c FROM portfolio_financial.ledger_account_balance WHERE book_id=$1),
            keys AS (SELECT account_id,fund_id FROM actual UNION SELECT account_id,fund_id FROM recorded)
            SELECT k.account_id,k.fund_id,coalesce(a.d,0),coalesce(a.c,0),coalesce(r.d,0),coalesce(r.c,0)
            FROM keys k LEFT JOIN actual a ON a.account_id=k.account_id AND a.fund_id IS NOT DISTINCT FROM k.fund_id
            LEFT JOIN recorded r ON r.account_id=k.account_id AND r.fund_id IS NOT DISTINCT FROM k.fund_id
            WHERE coalesce(a.d,0)<>coalesce(r.d,0) OR coalesce(a.c,0)<>coalesce(r.c,0) ORDER BY k.account_id,k.fund_id;
            """,[bookId],r=>new LedgerBalanceDifference(r.GetInt32(0),r.IsDBNull(1)?null:r.GetInt32(1),r.GetDecimal(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5)),ct);
        var total=totals.Single();
        return new(id,revision,total.Journals,total.Entries,total.Debits,total.Credits,differences.ToArray(),cut,string.Empty);
    }
}
