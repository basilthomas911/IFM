using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IFinancialQueryStore
{
    Task<FinancialRead<FinancialLedgerConfiguration>> ReadAsync(FinancialReadScope scope,GetFinancialLedgerConfigurationRequest request,CancellationToken token=default)
        => throw new NotSupportedException();
    Task<FinancialRead<FinancialPostingConfiguration>> ReadAsync(FinancialReadScope scope,GetFinancialPostingConfigurationRequest request,CancellationToken token=default)
        => throw new NotSupportedException();
    Task<FinancialRead<FundRiskAuthorizationEvidence>> ReadAsync(FinancialReadScope scope,GetFundRiskAuthorizationRequest request,CancellationToken token=default)
        => throw new NotSupportedException();
    Task<FinancialRead<FinancialAdmissionSnapshot>> ReadAsync(FinancialReadScope scope,GetFinancialAdmissionSnapshotRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialOperationOutcome>> ReadAsync(FinancialReadScope scope,GetPostingReceiptRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialJournal>> ReadAsync(FinancialReadScope scope,GetJournalRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialBalanceSnapshot>> ReadAsync(FinancialReadScope scope,GetAccountBalancesRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialTrialBalance>> ReadAsync(FinancialReadScope scope,GetTrialBalanceRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialReservationView>> ReadAsync(FinancialReadScope scope,GetCapacityReservationRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialCapacityUsage>> ReadAsync(FinancialReadScope scope,GetCapacityUsageRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialPage<FinancialTransactionRow>>> ReadAsync(FinancialReadScope scope,GetFundTransactionsPageRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialPage<FinancialReservationView>>> ReadAsync(FinancialReadScope scope,GetFundReservationsPageRequest request,CancellationToken token=default);
    Task<FinancialRead<FinancialReconciliationView>> ReadAsync(FinancialReadScope scope,GetReconciliationRequest request,CancellationToken token=default);
}

/// <summary>Bounded authoritative queries. The financial fence gives each response one consistent current revision.</summary>
public sealed class FinancialQueryStore(IPostgresEventTransaction transactions,FinancialDevelopmentPolicy? developmentPolicy=null) : IFinancialQueryStore
{
    public Task<FinancialRead<FinancialLedgerConfiguration>> ReadAsync(FinancialReadScope scope,GetFinancialLedgerConfigurationRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        Require(scope.FundId is null,FinancialReasons.InvalidContract,"Ledger controls require an explicit Portfolio-wide scope.");
        var state=(string)(await db.ScalarAsync("SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[scope.PortfolioId],ct))!;
        var periods=await db.QueryAsync("""
            SELECT period_id,start_date,end_date,revision,state FROM portfolio_financial.ledger_period
            WHERE book_id=$1 ORDER BY start_date DESC LIMIT 257;
            """,[book.BookId],r=>new FinancialLedgerPeriod(r.GetGuid(0),r.GetFieldValue<DateOnly>(1),r.GetFieldValue<DateOnly>(2),r.GetInt64(3),r.GetString(4)),ct);
        var accounts=await db.QueryAsync("""
            SELECT DISTINCT ON (account_id) account_id,version,category,normal_side,fund_dimension_required,content_hash,status
            FROM portfolio_financial.ledger_account WHERE book_id=$1 ORDER BY account_id,version DESC LIMIT 257;
            """,[book.BookId],r=>new FinancialConfiguredAccount(new(r.GetInt32(0),r.GetInt64(1),r.GetString(2),(PostingSide)r.GetInt32(3),r.GetBoolean(4),r.GetString(5)),r.GetString(6)),ct);
        var rules=await db.QueryAsync("""
            SELECT DISTINCT ON (rule_id) payload::text,status,effective_from,effective_to FROM portfolio_financial.ledger_posting_rule
            WHERE book_id=$1 ORDER BY rule_id,version DESC LIMIT 257;
            """,[book.BookId],r=>new FinancialConfiguredRule(Decode<LedgerPostingRule>(r.GetString(0)),r.GetString(1),r.GetFieldValue<DateOnly>(2),
                r.IsDBNull(3)?null:r.GetFieldValue<DateOnly>(3)),ct);
        Require(periods.Count<=256 && accounts.Count<=256 && rules.Count<=256,FinancialReasons.InvalidContract,"Ledger control selectors exceed their supported bound.");
        var reconciliations=await db.QueryAsync("""
            SELECT reconciliation_id,(counts->>'FinancialRevision')::bigint,(counts->>'JournalCount')::bigint,
                (counts->>'EntryCount')::bigint,(totals->>'Debits')::numeric,(totals->>'Credits')::numeric,
                differences::text,source_cut,content_hash
            FROM portfolio_financial.ledger_reconciliation WHERE book_id=$1 AND fund_id IS NULL
            ORDER BY (counts->>'FinancialRevision')::bigint DESC,reconciliation_id DESC LIMIT 1;
            """,[book.BookId],r=>new LedgerReconciliationResult(r.GetGuid(0),r.GetInt64(1),r.GetInt64(2),r.GetInt64(3),r.GetDecimal(4),r.GetDecimal(5),
                Decode<LedgerBalanceDifference[]>(r.GetString(6)),r.GetString(7),r.GetString(8)),ct);
        return new FinancialLedgerConfiguration(book.BookId,book.Currency,book.Environment,state,book.SourceWatermark,
            periods.ToArray(),accounts.ToArray(),rules.ToArray(),reconciliations.SingleOrDefault(),
            developmentPolicy?.IsDevelopmentEnvironment==true && book.Environment=="Emulator" && !book.MigrationQualified && state=="Importing" && book.Funds.All(x=>!x.CanSpend)?book:null);
    },token);
    public Task<FinancialRead<FinancialPostingConfiguration>> ReadAsync(FinancialReadScope scope,GetFinancialPostingConfigurationRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        Require(scope.FundId is >0 && request.AccountingDate!=default,FinancialReasons.InvalidContract,"Fund and accounting date are required.");
        var rules=await db.QueryAsync("""
            SELECT payload::text FROM portfolio_financial.ledger_posting_rule WHERE book_id=$1 AND status='Active'
              AND effective_from<=$2 AND (effective_to IS NULL OR effective_to>=$2) ORDER BY kind,rule_id,version LIMIT 257;
            """,[book.BookId,request.AccountingDate],r=>Decode<LedgerPostingRule>(r.GetString(0)),ct);
        Require(rules.Count<=256,FinancialReasons.InvalidContract,"Posting rule selection exceeds its bound.");
        var open=await db.ScalarAsync("SELECT state='Open' FROM portfolio_financial.ledger_period WHERE book_id=$1 AND start_date<=$2 AND end_date>=$2;",
            [book.BookId,request.AccountingDate],ct) is true;
        var state=(string)(await db.ScalarAsync("SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[scope.PortfolioId],ct))!;
        return new FinancialPostingConfiguration(book.BookId,scope.FundId!.Value,book.Funds.Single(x=>x.FundId==scope.FundId).Reference,
            rules.ToArray(),open,request.AccountingDate,developmentPolicy?.IsDevelopmentEnvironment==true && book.Environment=="Emulator" && !book.MigrationQualified && state=="Importing");
    },token);
    public Task<FinancialRead<FundRiskAuthorizationEvidence>> ReadAsync(FinancialReadScope scope,GetFundRiskAuthorizationRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        Require(request.CommandId!=Guid.Empty && scope.FundId is >0,FinancialReasons.InvalidContract,"Fund scope and command identity are required.");
        var result=await CapacityReservationStore.ReadEvidenceAsync<IFundRiskAuthorizedEvent>(db,request.CommandId,ct);
        if(result?.FinancialAuthorization is not { } authorization) return null;
        Require(authorization.PortfolioId==scope.PortfolioId && authorization.FundId==scope.FundId,
            FinancialReasons.AuthorityDenied,"Authorization belongs to a different scope.");
        return new FundRiskAuthorizationEvidence(result.CommandId,result.Id,authorization);
    },token);
    public Task<FinancialRead<FinancialAdmissionSnapshot>> ReadAsync(FinancialReadScope scope,GetFinancialAdmissionSnapshotRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        Require(scope.FundId is >0 && request.DeploymentKey.Kind==Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind.Deployment
            && request.DeploymentKey.Id!=Guid.Empty && request.DeploymentKey.Version>0 && !string.IsNullOrWhiteSpace(request.UnderlyingId)
            && request.UnderlyingId.Length<=128,FinancialReasons.InvalidContract,"Exact Fund, deployment and underlying are required.");
        var fund=book.Funds.Single(x=>x.FundId==scope.FundId);
        var deployment=fund.Deployments.SingleOrDefault(x=>x.Reference.DeploymentKey==request.DeploymentKey);
        Require(deployment is not null,FinancialReasons.AuthorityDenied,"Fund does not authorize this exact deployment.");
        var state=(string)(await db.ScalarAsync("SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[scope.PortfolioId],ct))!;
        bool ready=state=="Active" && book.MigrationQualified && fund.CanSpend && deployment!.MaximumRiskPerTrade>0 && deployment.Reference.ValidUntilUtc>DateTime.UtcNow;
        if(ready) await ValidateFundSourcesAsync(db,book,fund.FundId,true,ct);
        bool Relevant(CapacityScopeKind kind,string key)=>kind switch
        {
            CapacityScopeKind.Portfolio=>key==FinancialScopeKeys.Portfolio(scope.PortfolioId),
            CapacityScopeKind.Fund=>key==FinancialScopeKeys.Fund(fund.FundId),
            CapacityScopeKind.Deployment=>key==FinancialScopeKeys.Deployment(request.DeploymentKey),
            CapacityScopeKind.Underlying=>key==request.UnderlyingId,
            _=>false
        };
        var limits=fund.Limits.Concat(deployment!.Limits).Where(x=>Relevant(x.ScopeKind,x.ScopeKey)).ToArray();
        Require(limits.Length<=256,FinancialReasons.InvalidContract,"Admission limits exceed the bounded scope count.");
        var rows=await db.QueryAsync("""
            SELECT scope_kind,scope_key,measure,unit,held,working,position FROM portfolio_financial.capacity_usage
            WHERE portfolio_id=$1 AND ((scope_kind=1 AND scope_key=$2) OR (scope_kind=3 AND scope_key=$3)
              OR (scope_kind=2 AND scope_key=$4) OR (scope_kind=4 AND scope_key=$5))
            ORDER BY scope_kind,scope_key,measure,unit LIMIT 257;
            """,[scope.PortfolioId,FinancialScopeKeys.Portfolio(scope.PortfolioId),FinancialScopeKeys.Fund(fund.FundId),
                FinancialScopeKeys.Deployment(request.DeploymentKey),request.UnderlyingId],r=>new CapacityUsed((CapacityScopeKind)r.GetInt32(0),r.GetString(1),
                    (CapacityMeasure)r.GetInt32(2),(CapacityUnit)r.GetInt32(3),r.GetDecimal(4),r.GetDecimal(5),r.GetDecimal(6)),ct);
        Require(rows.Count<=256,FinancialReasons.InvalidContract,"Admission usage exceeds the bounded scope count.");
        return new FinancialAdmissionSnapshot(book.BookId,book.PortfolioId,fund.FundId,state,book.MigrationQualified,ready,deployment.Reference,
            await GeneralLedgerStore.AvailableCash(db,book.BookId,fund.FundId,ct),limits,rows.ToArray(),book.Environment,book.ExecutionAccountReference,deployment.MaximumRiskPerTrade);
    },token);
    public Task<FinancialRead<FinancialOperationOutcome>> ReadAsync(FinancialReadScope scope,GetPostingReceiptRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        Require(request.OperationId!=Guid.Empty,FinancialReasons.InvalidContract,"OperationId is required.");
        var result=await ReadOperationAsync<IFinancialCompletedEvent>(db,scope.PortfolioId,request.OperationId,null,ct);
        if(result is null) return null;
        if(scope.FundId is { } fund)
        {
            var permitted=result switch
            {
                EmulatorOrderSubmittedEvent x=>x.Receipt.Order.FundId==fund,
                LedgerPostingCompletedEvent x=>x.Receipt.FundId==fund,
                LedgerPostingBatchCompletedEvent=>await db.ScalarAsync("""
                    SELECT count(*)>0 AND bool_and(fund_id=$3) FROM portfolio_financial.ledger_transaction WHERE portfolio_id=$1 AND operation_id=$2;
                    """,[scope.PortfolioId,request.OperationId,fund],ct) is true,
                CapacityReservationCompletedEvent x=>x.Receipt.FundId==fund,
                CapacityConsumptionCompletedEvent x=>await ReservationFund(db,scope.PortfolioId,x.Receipt.ReservationId,ct)==fund,
                CapacityLifecycleCompletedEvent x=>await ReservationFund(db,scope.PortfolioId,x.Receipt.ReservationId,ct)==fund,
                _=>false
            };
            Require(permitted,FinancialReasons.AuthorityDenied,"Operation is outside the selected Fund scope.");
        }
        return result switch
        {
            EmulatorOrderSubmittedEvent x=>new FinancialOperationOutcome { EmulatorSubmission=x },
            LedgerPostingCompletedEvent x=>new FinancialOperationOutcome { Posting=x },
            LedgerPostingBatchCompletedEvent x=>new FinancialOperationOutcome { Batch=x },
            CapacityReservationCompletedEvent x=>new FinancialOperationOutcome { Reservation=x },
            CapacityConsumptionCompletedEvent x=>new FinancialOperationOutcome { Consumption=x },
            CapacityLifecycleCompletedEvent x=>new FinancialOperationOutcome { Lifecycle=x },
            LedgerConfigurationCompletedEvent x=>new FinancialOperationOutcome { Configuration=x },
            _=>throw new FinancialOperationException(FinancialReasons.InvalidContract,"Unknown financial operation type.")
        };
    },token);

    public Task<FinancialRead<FinancialJournal>> ReadAsync(FinancialReadScope scope,GetJournalRequest request,CancellationToken token=default)
        =>Read<FinancialJournal>(scope,async(db,book,revision,ct)=>
    {
        Require(request.JournalId>0,FinancialReasons.InvalidContract,"JournalId is required.");
        var rows=await db.QueryAsync("""
            SELECT journal_id,transaction_id,book_id,fund_id,accounting_date,journal_hash FROM portfolio_financial.ledger_journal
            WHERE portfolio_id=$1 AND journal_id=$2 AND ($3::int IS NULL OR fund_id=$3);
            """,[scope.PortfolioId,request.JournalId,scope.FundId],r=>new FinancialJournal(r.GetInt64(0),r.GetInt64(1),r.GetInt32(2),
                r.IsDBNull(3)?null:r.GetInt32(3),r.GetFieldValue<DateOnly>(4),r.GetString(5),[]),ct);
        if(rows.Count==0) return null;
        var entries=await db.QueryAsync("""
            SELECT ordinal,account_id,account_version,fund_id,debit,credit,source_line_reference
            FROM portfolio_financial.ledger_entry WHERE journal_id=$1 ORDER BY ordinal LIMIT 257;
            """,[request.JournalId],r=>new FinancialJournalEntry(r.GetInt32(0),r.GetInt32(1),r.GetInt64(2),r.IsDBNull(3)?null:r.GetInt32(3),
                r.GetDecimal(4),r.GetDecimal(5),r.GetString(6)),ct);
        Require(entries.Count<=256,FinancialReasons.InvalidContract,"Journal exceeds the supported line count.");
        return rows[0] with { Entries=entries.ToArray() };
    },token);

    public Task<FinancialRead<FinancialBalanceSnapshot>> ReadAsync(FinancialReadScope scope,GetAccountBalancesRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        var accounts=await Accounts(db,book.BookId,scope.FundId,ct);
        var pending=Convert.ToDecimal(await db.ScalarAsync("""
            SELECT coalesce(sum(amount),0) FROM portfolio_financial.financial_encumbrance
            WHERE book_id=$1 AND ($2::int IS NULL OR fund_id=$2) AND status='Pending';
            """,[book.BookId,scope.FundId],ct));
        var state=(string)(await db.ScalarAsync("SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[scope.PortfolioId],ct))!;
        decimal available=0;
        foreach(var fund in book.Funds.Where(x=>scope.FundId is null || x.FundId==scope.FundId))
            available+=await GeneralLedgerStore.AvailableCash(db,book.BookId,fund.FundId,ct);
        return new FinancialBalanceSnapshot(book.BookId,state,accounts,pending,available,book.MigrationQualified);
    },token);

    public Task<FinancialRead<FinancialTrialBalance>> ReadAsync(FinancialReadScope scope,GetTrialBalanceRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        var accounts=await Accounts(db,book.BookId,scope.FundId,ct);
        var debits=accounts.Sum(x=>x.Debits); var credits=accounts.Sum(x=>x.Credits);
        return new FinancialTrialBalance(accounts,debits,credits,debits==credits);
    },token);

    public Task<FinancialRead<FinancialReservationView>> ReadAsync(FinancialReadScope scope,GetCapacityReservationRequest request,CancellationToken token=default)
        =>Read<FinancialReservationView>(scope,async(db,book,revision,ct)=>
    {
        var rows=await Reservations(db,scope,request.ReservationId,0,long.MaxValue,1,ct);
        return rows.SingleOrDefault();
    },token);

    public Task<FinancialRead<FinancialCapacityUsage>> ReadAsync(FinancialReadScope scope,GetCapacityUsageRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        var rows=await db.QueryAsync("""
            SELECT scope_kind,scope_key,measure,unit,held,working,position FROM portfolio_financial.capacity_usage
            WHERE portfolio_id=$1 AND ($2::text IS NULL OR (scope_kind=3 AND scope_key=$2))
            ORDER BY scope_kind,scope_key,measure,unit LIMIT 4097;
            """,[scope.PortfolioId,scope.FundId?.ToString(System.Globalization.CultureInfo.InvariantCulture)],r=>new CapacityUsed(
                (CapacityScopeKind)r.GetInt32(0),r.GetString(1),(CapacityMeasure)r.GetInt32(2),(CapacityUnit)r.GetInt32(3),r.GetDecimal(4),r.GetDecimal(5),r.GetDecimal(6)),ct);
        Require(rows.Count<=4096,FinancialReasons.InvalidContract,"Usage exceeds the bounded supported scope count.");
        return new FinancialCapacityUsage(rows.ToArray());
    },token);

    public Task<FinancialRead<FinancialPage<FinancialTransactionRow>>> ReadAsync(FinancialReadScope scope,GetFundTransactionsPageRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        var cursor=Cursor(scope,request.Cursor,request.PageSize,revision,"Transactions");
        var rows=await db.QueryAsync("""
            SELECT t.transaction_id,t.operation_id,o.financial_revision,t.item_ordinal,t.payload::text,j.journal_id
            FROM portfolio_financial.ledger_transaction t JOIN portfolio_financial.financial_operation_receipt o
              ON o.portfolio_id=t.portfolio_id AND o.operation_id=t.operation_id
            LEFT JOIN portfolio_financial.ledger_journal j ON j.transaction_id=t.transaction_id
            WHERE t.portfolio_id=$1 AND t.fund_id=$2 AND o.financial_revision<=$3
              AND (o.financial_revision,t.item_ordinal)>($4,$5)
            ORDER BY o.financial_revision,t.item_ordinal LIMIT $6;
            """,[scope.PortfolioId,scope.FundId,cursor.AsOfRevision,cursor.AfterRevision,cursor.AfterOrdinal,request.PageSize+1],r=>new FinancialTransactionRow(
                r.GetInt64(0),r.GetGuid(1),r.GetInt64(2),r.GetInt32(3),Decode<LedgerPostingRequest>(r.GetString(4)),r.IsDBNull(5)?null:r.GetInt64(5)),ct);
        var items=rows.Take(request.PageSize).ToArray();
        var next=rows.Count>request.PageSize?cursor with { AfterRevision=items[^1].FinancialRevision,AfterOrdinal=items[^1].Ordinal }:null;
        return new FinancialPage<FinancialTransactionRow>(items,next,cursor.AsOfRevision);
    },token);

    public Task<FinancialRead<FinancialPage<FinancialReservationView>>> ReadAsync(FinancialReadScope scope,GetFundReservationsPageRequest request,CancellationToken token=default)
        =>Read(scope,async(db,book,revision,ct)=>
    {
        var cursor=Cursor(scope,request.Cursor,request.PageSize,revision,"Reservations");
        var rows=await Reservations(db,scope,null,cursor.AfterRevision,cursor.AsOfRevision,request.PageSize+1,ct);
        var items=rows.Take(request.PageSize).ToArray();
        var next=rows.Count>request.PageSize?cursor with { AfterRevision=items[^1].OriginalReceipt.FinancialRevision }:null;
        return new FinancialPage<FinancialReservationView>(items,next,cursor.AsOfRevision);
    },token);

    public Task<FinancialRead<FinancialReconciliationView>> ReadAsync(FinancialReadScope scope,GetReconciliationRequest request,CancellationToken token=default)
        =>Read<FinancialReconciliationView>(scope,async(db,book,revision,ct)=>
        (await db.QueryAsync("""
            SELECT reconciliation_id,source_cut,status,content_hash,differences::text FROM portfolio_financial.ledger_reconciliation
            WHERE portfolio_id=$1 AND reconciliation_id=$2 AND ($3::int IS NULL OR fund_id=$3);
            """,[scope.PortfolioId,request.ReconciliationId,scope.FundId],r=>new FinancialReconciliationView(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)),ct)).SingleOrDefault(),token);

    async Task<FinancialRead<T>> Read<T>(FinancialReadScope scope,Func<EnlistedEventTransaction,FinancialBookConfiguration,long,CancellationToken,Task<T?>> query,
        CancellationToken token) where T:class
    {
        Require(scope.PortfolioId>0 && (scope.FundId is null or >0),FinancialReasons.InvalidContract,"Portfolio/Fund scope is invalid.");
        var access=scope.Access; var admin=access.Roles?.Contains("PortfolioAdministrator",StringComparer.Ordinal)==true;
        Require(!string.IsNullOrWhiteSpace(access.Principal) && (admin || access.Roles?.Contains("LedgerRead",StringComparer.Ordinal)==true &&
            access.PortfolioIds?.Contains(scope.PortfolioId)==true),FinancialReasons.AuthorityDenied,"Caller cannot read this Portfolio's financial data.");
        return await transactions.ExecuteAsync(async(db,ct)=>
        {
            var rows=await db.QueryAsync("""
                SELECT policy_source_versions::text,financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1 FOR SHARE;
                """,[scope.PortfolioId],r=>(Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Revision:r.GetInt64(1)),ct);
            if(rows.Count==0) return new FinancialRead<T>(FinancialReadStatus.NotFound,null,0,DateTime.UtcNow);
            var (book,revision)=rows[0];
            if(scope.FundId is { } fund) Require(book.Funds.Any(x=>x.FundId==fund),FinancialReasons.AuthorityDenied,"Fund does not belong to this financial book.");
            var result=await query(db,book,revision,ct);
            return new FinancialRead<T>(result is null?FinancialReadStatus.NotFound:FinancialReadStatus.Found,result,revision,DateTime.UtcNow);
        },token);
    }

    static async Task<FinancialAccountBalance[]> Accounts(EnlistedEventTransaction db,int book,int? fund,CancellationToken token)
    {
        var rows=await db.QueryAsync("""
            SELECT account_id,fund_id,currency,debit_total,credit_total,balance FROM portfolio_financial.ledger_account_balance
            WHERE book_id=$1 AND ($2::int IS NULL OR fund_id=$2) ORDER BY account_id,fund_id LIMIT 4097;
            """,[book,fund],r=>new FinancialAccountBalance(r.GetInt32(0),r.IsDBNull(1)?null:r.GetInt32(1),r.GetString(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5)),token);
        Require(rows.Count<=4096,FinancialReasons.InvalidContract,"Balance query exceeds the supported account count.");
        return rows.ToArray();
    }
    static FinancialPageCursor Cursor(FinancialReadScope scope,FinancialPageCursor? cursor,int size,long revision,string kind)
    {
        Require(scope.FundId is >0 && size is >=1 and <=100,FinancialReasons.InvalidContract,"A Fund scope and page size 1–100 are required.");
        if(cursor is null) return new(scope.PortfolioId,scope.FundId!.Value,revision,0,0,kind);
        Require(cursor.PortfolioId==scope.PortfolioId && cursor.FundId==scope.FundId && cursor.Kind==kind &&
            cursor.AsOfRevision>=0 && cursor.AsOfRevision<=revision && cursor.AfterRevision>=0 && cursor.AfterRevision<=cursor.AsOfRevision && cursor.AfterOrdinal>=0,
            FinancialReasons.InvalidContract,"Cursor scope/revision does not match this query.");
        return cursor;
    }
    static async Task<int?> ReservationFund(EnlistedEventTransaction db,int portfolio,Guid reservation,CancellationToken token)
        =>(await db.ScalarAsync("SELECT fund_id FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1 AND reservation_id=$2;",[portfolio,reservation],token)) as int?;
    static Task<IReadOnlyList<FinancialReservationView>> Reservations(EnlistedEventTransaction db,FinancialReadScope scope,Guid? reservation,
        long after,long asOf,int limit,CancellationToken token)=>db.QueryAsync("""
            SELECT r.receipt::text,r.reservation_id,r.version,r.status,r.strategy_units,r.filled_units,r.cancelled_units,r.remaining_units,
              r.requirements->>'ContentHash',r.expires_at_utc,r.execution_id,
              coalesce((SELECT max(l.execution_revision) FROM portfolio_financial.capacity_lifecycle l WHERE l.reservation_id=r.reservation_id),0),r.closed_units
            FROM portfolio_financial.capacity_reservation r JOIN portfolio_financial.financial_operation_receipt o
              ON o.portfolio_id=r.portfolio_id AND o.operation_id=r.operation_id
            WHERE r.portfolio_id=$1 AND ($2::int IS NULL OR r.fund_id=$2) AND ($3::uuid IS NULL OR r.reservation_id=$3)
              AND o.financial_revision>$4 AND o.financial_revision<=$5 ORDER BY o.financial_revision LIMIT $6;
            """,[scope.PortfolioId,scope.FundId,reservation,after,asOf,limit],r=>new FinancialReservationView(Decode<CapacityReservationReceipt>(r.GetString(0)),
                new(r.GetGuid(1),r.GetInt64(2),(ReservationStatus)r.GetInt32(3),r.GetInt32(4),r.GetInt32(5),r.GetInt32(6),r.GetInt32(7),
                    r.GetString(8),r.GetDateTime(9),r.IsDBNull(10)?null:r.GetGuid(10),r.GetInt64(11),r.GetInt32(12))),token);
}
