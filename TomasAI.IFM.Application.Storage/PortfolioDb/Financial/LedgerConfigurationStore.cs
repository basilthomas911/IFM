using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Persists canonical Portfolio ledger configuration operations.</summary>
public interface ILedgerConfigurationStore
{
    /// <summary>Configures a canonical Portfolio ledger operation atomically.</summary>
    Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt,LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult,string> hash,CancellationToken token=default);
}

/// <summary>Configuration, period control, and independent reconciliation share canonical Portfolio authority.</summary>
/// <param name="transactions">The transactional event-store boundary.</param>
/// <param name="developmentPolicy">The optional development-only qualification policy.</param>
public sealed class LedgerConfigurationStore(IPostgresEventTransaction transactions,FinancialDevelopmentPolicy? developmentPolicy=null):ILedgerConfigurationStore
{
    /// <inheritdoc />
    public async Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt,LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult,string> hash,CancellationToken token=default)
    {
        try { return await transactions.ExecuteAsync(async(db,ct)=>
        {
            var replay=await ReadOperationAsync<LedgerConfigurationCompletedEvent>(db,request.PortfolioId,request.OperationId,request.InputSha256,ct);
            if(replay is not null) return replay;
            await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[request.PortfolioId],ct);
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
                    }
                    state="NeedsRefresh";
                    await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update01,[request.PortfolioId,Json(current.Book with { MigrationQualified=true }),state],ct);
                    break;
                case LedgerConfigurationAction.AddAccountVersion:
                    foreach(var account in body.Accounts)
                    {
                        var last=Convert.ToInt64(await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select03,[body.BookId,account.AccountId],ct));
                        Require(last==body.ExpectedVersion && account.Version==last+1,FinancialReasons.RevisionConflict,"Account version changed.");
                        var category=await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select04,[body.BookId,account.AccountId],ct);
                        Require(category is null || Equals(category,account.Category),FinancialReasons.InvalidContract,"An account's economic category cannot change across versions.");
                        await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert02,[body.BookId,account.AccountId,account.Version,account.Category,(int)account.NormalSide,account.FundDimensionRequired,account.ContentHash],ct);
                    }
                    break;
                case LedgerConfigurationAction.AddPostingRuleVersion:
                    foreach(var rule in body.Rules)
                    {
                        var last=Convert.ToInt64(await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select05,[body.BookId,rule.RuleId],ct));
                        Require(last==body.ExpectedVersion && rule.Version==last+1,FinancialReasons.RevisionConflict,"Posting rule version changed.");
                        foreach(var account in new[] { rule.Debit,rule.Credit,rule.ValuationAsset,rule.UnrealizedPnl }.OfType<LedgerAccountBinding>())
                            Require(await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select06,[body.BookId,account.AccountId,account.Version],ct) is "Active",
                                FinancialReasons.InvalidContract,"Posting rules require active exact account versions.");
                        await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert03,[body.BookId,rule.RuleId,rule.Version,(int)rule.Kind,Json(rule),rule.ContentHash,body.PeriodStart],ct);
                    }
                    break;
                case LedgerConfigurationAction.RetireAccount:
                    foreach(var account in body.Accounts)
                    {
                        Require(await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select07,[body.BookId,account.AccountId.ToString(System.Globalization.CultureInfo.InvariantCulture)],ct) is 0L,
                            FinancialReasons.AuthorityDenied,"Retire dependent posting rules before retiring an account.");
                        var changed=await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update02,[body.BookId,account.AccountId,account.Version],ct);
                        Require(changed==1,FinancialReasons.RevisionConflict,"Account version is not active.");
                    }
                    break;
                case LedgerConfigurationAction.RetirePostingRule:
                    foreach(var rule in body.Rules)
                        Require(await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update03,[body.BookId,rule.RuleId,rule.Version],ct)==1,
                            FinancialReasons.RevisionConflict,"Posting rule version is not active.");
                    break;
                case LedgerConfigurationAction.OpenPeriod:
                    await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert04,[body.BookId,body.PeriodId,body.PeriodStart,body.PeriodEnd,body.SourceCut,Json(new { request.OperationId,body.Reason })],ct);
                    break;
                case LedgerConfigurationAction.ClosePeriod:
                case LedgerConfigurationAction.ReopenPeriod:
                    var closing=body.Action==LedgerConfigurationAction.ClosePeriod;
                    if(closing) await RequireReconciled(db,body,ct);
                    Require(await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update04,[body.BookId,body.PeriodId,body.ExpectedVersion,closing?"Closed":"Open",body.SourceCut,
                            Json(new { request.OperationId,body.Reason,body.ReconciliationId }),closing?"Open":"Closed"],ct)==1,
                        FinancialReasons.RevisionConflict,"Period state/version changed.");
                    break;
                case LedgerConfigurationAction.RefreshAuthority:
                    if(body.Book?.Funds.Any(x=>x.CanSpend)==true)
                        Require(await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select08,[request.PortfolioId,(int)CapacityScopeKind.Underlying],ct) is 0L,
                            FinancialReasons.AuthorityDenied,"Legacy contract-specific underlying obligations require reconciliation before new product-wide authority.");
                    Require(body.Book is { } updated && updated.BookId==current.Book.BookId && updated.PortfolioId==request.PortfolioId &&
                        updated.AccountingEntityId==current.Book.AccountingEntityId && updated.Environment==current.Book.Environment &&
                        updated.ExecutionAccountReference==current.Book.ExecutionAccountReference && updated.Currency=="USD" &&
                        updated.MigrationQualified==current.Book.MigrationQualified,
                        FinancialReasons.AuthorityDenied,"Refresh cannot replace book ownership or migration qualification.");
                    Require(body.Book!.Funds.Select(x=>x.FundId).Order().SequenceEqual(current.Book.Funds.Select(x=>x.FundId).Order()),
                        FinancialReasons.AuthorityDenied,"Authority refresh cannot change qualified book membership; a separate scoped qualification is required.");
                    var epoch=Convert.ToInt64(await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select09,[request.PortfolioId],ct));
                    Require(body.Book!.AuthorityEpoch==epoch && body.Book.Funds.All(x=>x.Reference.AuthorityEpoch==epoch),FinancialReasons.AuthorityRevoked,"Authority epoch changed.");
                    foreach(var fund in body.Book.Funds) await ValidateFundSourcesAsync(db,body.Book,fund.FundId,fund.CanSpend,ct);
                    foreach(var fund in body.Book.Funds)
                    {
                        await CheckSource($"Portfolio.{request.PortfolioId}",fund.PortfolioStreamVersion);
                        await CheckSource($"PortfolioFund.{request.PortfolioId}.{fund.FundId}",fund.FundStreamVersion);
                        if(fund.CanSpend) await CheckSource($"PortfolioFinancialPolicy.{request.PortfolioId}.{fund.Reference.PolicyId}",fund.PolicyStreamVersion);
                    }
                    state=current.State is "Overdrawn" or "NeedsReconciliation"?current.State:body.Book.MigrationQualified?"Active":"Importing";
                    await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update05,[request.PortfolioId,Json(body.Book),body.Book.SourceWatermark,body.Book.ValuationWatermark,state],ct);
                    break;
                case LedgerConfigurationAction.Reconcile:
                    reconciliationId=request.OperationId;
                    var result=await ReconstructAsync(db,body.BookId,reconciliationId.Value,current.Revision,body.SourceCut,ct);
                    result=result with { ContentHash=hash(result) };
                    await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Insert05,[result.ReconciliationId,body.BookId,request.PortfolioId,body.SourceCut,Json(new { result.JournalCount,result.EntryCount,result.FinancialRevision }),
                            Json(new { result.Debits,result.Credits }),result.ContentHash,Json(result.Differences),result.Differences.Length==0 && result.Debits==result.Credits?"Matched":"Mismatch"],ct);
                    if(result.Differences.Length!=0 || result.Debits!=result.Credits)
                    {
                        state="NeedsReconciliation";
                        await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update06,[request.PortfolioId,state],ct);
                    }
                    else if(current.State is "Overdrawn" or "NeedsReconciliation")
                    {
                        var solvent=true;
                        foreach(var fund in current.Book.Funds)
                            if(await GeneralLedgerStore.AvailableCash(db,current.Book.BookId,fund.FundId,ct)<0) solvent=false;
                        // A matching ledger is not a fresh mandate, valuation or admission authorization.
                        // Explicit authority refresh must follow reconciliation before a qualified book can spend again.
                        state=!solvent ? "Overdrawn" : current.Book.MigrationQualified ? "NeedsRefresh" : "Importing";
                        await db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Update07,[request.PortfolioId,state],ct);
                    }
                    break;
                default: throw new FinancialOperationException(FinancialReasons.InvalidContract,"Unknown configuration action.");
            }
            var completed=complete(new(request.OperationId,body.BookId,body.Action,revision,DateTime.UtcNow,reconciliationId,state));
            await SaveOutcomeAsync(db,request,completed,revision,false,ct);
            return completed;

            async Task CheckSource(string stream,long version)
            {
                var actual=await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select10,[stream],ct);
                Require(version>0 && actual is not null && Convert.ToInt64(actual)==version,FinancialReasons.AuthorityRevoked,"Source changed while preparing authority refresh.");
            }
        },token); }
        catch(FunctionCommitOutcomeUnknownException)
        {
            using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                var receipt=await new PortfolioFinancialStore(transactions).ReadOperationAsync<LedgerConfigurationCompletedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,recovery.Token);
                if(receipt is not null) return receipt;
            }
            catch(Exception) { /* The caller must reconcile the original identity after connectivity returns. */ }
            throw;
        }
    }

    static async Task RequireReconciled(EnlistedEventTransaction db,LedgerConfigurationRequest body,CancellationToken ct)
    {
        Require(body.ReconciliationId is not null,FinancialReasons.InvalidContract,"Period closure requires reconciliation evidence.");
        var matches=await db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select11,[body.BookId,body.ReconciliationId,body.SourceCut],ct);
        Require(matches is true,FinancialReasons.AuthorityDenied,"Reconciliation is missing, mismatched or predates subsequent journal postings.");
    }

    static async Task<LedgerReconciliationResult> ReconstructAsync(EnlistedEventTransaction db,int bookId,Guid id,long revision,string cut,CancellationToken ct)
    {
        var totals=await db.QueryAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select12,[bookId],r=>(Journals:r.GetInt64(0),Entries:r.GetInt64(1),Debits:r.GetDecimal(2),Credits:r.GetDecimal(3)),ct);
        var differences=await db.QueryAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.With01,[bookId],r=>new LedgerBalanceDifference(r.GetInt32(0),r.IsDBNull(1)?null:r.GetInt32(1),r.GetDecimal(2),r.GetDecimal(3),r.GetDecimal(4),r.GetDecimal(5)),ct);
        var total=totals.Single();
        return new(id,revision,total.Journals,total.Entries,total.Debits,total.Credits,differences.ToArray(),cut,string.Empty);
    }
}
