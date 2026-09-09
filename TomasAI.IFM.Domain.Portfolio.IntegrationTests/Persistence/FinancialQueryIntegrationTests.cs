using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-03")]
public sealed class FinancialQueryIntegrationTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    static FinancialQueryStore Store()=>new(Transactions());
    static FinancialReadScope Scope(FinancialBookConfiguration book)=>new() { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,Access=new("integration",["PortfolioAdministrator"]) };

    [Fact]
    public async Task Posting_choices_are_effective_configured_rules_and_period_state_from_one_financial_cut()
    {
        var book=await CreateBook();var scope=Scope(book);var date=DateOnly.FromDateTime(DateTime.UtcNow);
        var choices=await Store().ReadAsync(scope,new GetFinancialPostingConfigurationRequest(date));
        choices.Status.Should().Be(FinancialReadStatus.Found);
        choices.Value!.BookId.Should().Be(book.BookId);choices.Value.FundId.Should().Be(scope.FundId);
        choices.Value.PeriodOpen.Should().BeTrue();choices.Value.Rules.Should().NotBeEmpty();
        choices.Value.Rules.Should().Contain(x=>x.Kind==LedgerTransactionKind.DepositConfirmed);
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("UPDATE portfolio_financial.ledger_period SET state='Closed' WHERE book_id=$1;",[book.BookId],ct);
            return true;
        });
        (await Store().ReadAsync(scope,new GetFinancialPostingConfigurationRequest(date))).Value!.PeriodOpen.Should().BeFalse();
        var outside=await Store().ReadAsync(scope,new GetFinancialPostingConfigurationRequest(new(1900,1,1)));
        outside.Value!.PeriodOpen.Should().BeFalse();outside.Value.Rules.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0,false)] [InlineData(1,true)] [InlineData(100,true)] [InlineData(101,false)]
    public async Task Financial_history_enforces_the_documented_hundred_row_boundary(int size,bool allowed)
    {
        var book=await CreateBook();
        var transactions=()=>Store().ReadAsync(Scope(book),new GetFundTransactionsPageRequest(size));
        var reservations=()=>Store().ReadAsync(Scope(book),new GetFundReservationsPageRequest(size));
        if(allowed)
        {
            (await transactions()).Value!.Items.Should().BeEmpty();
            (await reservations()).Value!.Items.Should().BeEmpty();
        }
        else
        {
            (await FluentActions.Awaiting(transactions).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.InvalidContract);
            (await FluentActions.Awaiting(reservations).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.InvalidContract);
        }
    }

    [Fact]
    public async Task Admission_snapshot_has_one_revision_and_only_exact_deployment_and_underlying_usage()
    {
        var key=new CatalogKey(StrategyCatalogKind.Deployment,Guid.NewGuid(),1);
        var book=await CreateBook(b=>
        {
            var fund=b.Funds[0]; var reference=fund.Reference with { DeploymentKey=key };
            return b with { Funds=[fund with { Reference=reference,Deployments=[new(reference,[],100)],
                Limits=[new(CapacityScopeKind.Portfolio,FinancialScopeKeys.Portfolio(b.PortfolioId),CapacityMeasure.LossCharge,CapacityUnit.Usd,500)] }] };
        });
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1000,0));
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            foreach(var underlying in new[] { "ES","NQ" })
                await db.ExecuteAsync("""
                    INSERT INTO portfolio_financial.capacity_usage(portfolio_id,scope_kind,scope_key,measure,unit,held,working,position,revision)
                    VALUES($1,4,$2,7,4,10,0,0,1);
                    """,[book.PortfolioId,underlying],ct);
            return true;
        });
        var read=await Store().ReadAsync(Scope(book),new GetFinancialAdmissionSnapshotRequest(key,"ES"));
        read.FinancialRevision.Should().Be(1); read.Value!.CanPrepareAdmission.Should().BeTrue(); read.Value.AvailableCash.Should().Be(1000);
        read.Value.Authority.DeploymentKey.Should().Be(key); read.Value.Usage.Should().ContainSingle(x=>x.ScopeKey=="ES");
        read.Value.Usage.Should().NotContain(x=>x.ScopeKey=="NQ");
        await Post(Request(book,LedgerTransactionKind.WithdrawalRequested,200,1));
        var after=await Store().ReadAsync(Scope(book),new GetFinancialAdmissionSnapshotRequest(key,"ES"));
        after.FinancialRevision.Should().Be(2); after.Value!.AvailableCash.Should().Be(800);
        (await FluentActions.Awaiting(()=>Store().ReadAsync(Scope(book),new GetFinancialAdmissionSnapshotRequest(key with { Version=2 },"ES")))
            .Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
    }

    [Fact]
    public async Task Admission_snapshot_refuses_stale_committed_source_versions()
    {
        var key=new CatalogKey(StrategyCatalogKind.Deployment,Guid.NewGuid(),1);
        var book=await CreateBook(b=>b with { Funds=[b.Funds[0] with { Deployments=[new(b.Funds[0].Reference with { DeploymentKey=key },[],100)] }] });
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("UPDATE event_stream_id SET currentversion=currentversion+1 WHERE eventstream=$1;",
                [$"PortfolioFund.{book.PortfolioId}.{book.Funds[0].FundId}"],ct);
            return true;
        });
        (await FluentActions.Awaiting(()=>Store().ReadAsync(Scope(book),new GetFinancialAdmissionSnapshotRequest(key,"ES")))
            .Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.AuthorityRevoked);
    }

    [Fact]
    public async Task Missing_book_returns_not_found_without_inventing_a_zero_balance()
    {
        var result=await Store().ReadAsync(new FinancialReadScope { PortfolioId=int.MaxValue,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        result.Status.Should().Be(FinancialReadStatus.NotFound); result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Balances_trial_balance_receipt_and_journal_reconcile_at_current_revision()
    {
        var book=await CreateBook(); var deposit=Request(book,LedgerTransactionKind.DepositConfirmed,1000,0); var posted=await Post(deposit);
        await Post(Request(book,LedgerTransactionKind.WithdrawalRequested,700,1)); var scope=Scope(book);
        var balances=await Store().ReadAsync(scope,new GetAccountBalancesRequest());
        balances.FinancialRevision.Should().Be(2); balances.Value!.AvailableCash.Should().Be(300); balances.Value.PendingWithdrawals.Should().Be(700);
        var trial=await Store().ReadAsync(scope,new GetTrialBalanceRequest());
        trial.Value!.TotalDebits.Should().Be(1000); trial.Value.TotalCredits.Should().Be(1000); trial.Value.Balanced.Should().BeTrue();
        var receipt=await Store().ReadAsync(scope,new GetPostingReceiptRequest(deposit.OperationId));
        receipt.FinancialRevision.Should().Be(2); receipt.Value!.Posting!.Receipt.FinancialRevision.Should().Be(1);
        var journal=await Store().ReadAsync(scope,new GetJournalRequest(posted.Receipt.JournalId!.Value));
        journal.Value!.Entries.Sum(x=>x.Debit).Should().Be(1000); journal.Value.Entries.Sum(x=>x.Credit).Should().Be(1000);
    }

    [Fact]
    public async Task Paged_transactions_preserve_scope_and_original_cut_when_new_postings_arrive()
    {
        var book=await CreateBook(); var scope=Scope(book);
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,10,0));
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,20,1));
        var first=await Store().ReadAsync(scope,new GetFundTransactionsPageRequest(1));
        first.Value!.Items.Single().Transaction.Amount.Should().Be(10); first.Value.NextCursor.Should().NotBeNull();
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,30,2));
        var second=await Store().ReadAsync(scope,new GetFundTransactionsPageRequest(1,first.Value.NextCursor));
        second.Value!.Items.Single().Transaction.Amount.Should().Be(20); second.Value.NextCursor.Should().BeNull(); second.Value.AsOfRevision.Should().Be(2);
        var tampered=first.Value.NextCursor! with { FundId=book.Funds[0].FundId+1 };
        (await FluentActions.Awaiting(()=>Store().ReadAsync(scope,new GetFundTransactionsPageRequest(1,tampered))).Should().ThrowAsync<FinancialOperationException>())
            .Which.Code.Should().Be(FinancialReasons.InvalidContract);
    }

    [Fact]
    public async Task Read_permission_does_not_grant_access_to_other_portfolios_or_foreign_funds()
    {
        var book=await CreateBook(); var scope=Scope(book);
        var other=scope with { Access=new("reader",["LedgerRead"],[book.PortfolioId+1]) };
        (await FluentActions.Awaiting(()=>Store().ReadAsync(other,new GetAccountBalancesRequest())).Should().ThrowAsync<FinancialOperationException>())
            .Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        (await FluentActions.Awaiting(()=>Store().ReadAsync(scope with { FundId=book.Funds[0].FundId+1 },new GetAccountBalancesRequest())).Should().ThrowAsync<FinancialOperationException>())
            .Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
    }

    [Theory]
    [InlineData("entry")]
    [InlineData("account")]
    [InlineData("period")]
    public async Task Database_rejects_posted_journal_extension_configuration_rewrite_and_period_overlap(string mutation)
    {
        var book=await CreateBook(); var result=await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var failure=await FluentActions.Awaiting(()=>Transactions().ExecuteAsync(async(db,ct)=>
        {
            if(mutation=="entry") await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.ledger_entry(journal_id,ordinal,book_id,account_id,account_version,fund_id,debit,credit,currency,source_line_reference)
                VALUES($1,3,$2,101,1,$3,1,0,'USD','late');
                """,[result.Receipt.JournalId,book.BookId,book.Funds[0].FundId],ct);
            else if(mutation=="account") await db.ExecuteAsync("UPDATE portfolio_financial.ledger_account SET category='Expense' WHERE book_id=$1 AND account_id=101;",[book.BookId],ct);
            else await db.ExecuteAsync("""
                INSERT INTO portfolio_financial.ledger_period(book_id,period_id,start_date,end_date,state,revision,source_cut,evidence)
                VALUES($1,$2,'2026-01-01','2026-12-31','Open',1,'test','{}');
                """,[book.BookId,Guid.NewGuid()],ct);
            return true;
        })).Should().ThrowAsync<Npgsql.PostgresException>();
        failure.Which.SqlState.Should().Be("23514");
        failure.Which.MessageText.Should().Be(mutation switch { "entry"=>"Posted journal cannot receive new entries", "account"=>"Financial configuration versions are immutable", _=>"Ledger accounting periods cannot overlap" });
        (await Store().ReadAsync(Scope(book),new GetTrialBalanceRequest())).Value!.TotalDebits.Should().Be(100);
    }
}
