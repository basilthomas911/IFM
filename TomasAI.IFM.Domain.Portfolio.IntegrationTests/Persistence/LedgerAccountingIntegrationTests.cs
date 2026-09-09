using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-03")]
public sealed class LedgerAccountingIntegrationTests
{
    [Fact]
    public async Task Replenished_overdrawn_book_requires_reconciliation_and_fresh_authority_before_spending()
    {
        var book=await CreateBook();await GeneralLedgerPostingIntegrationTests.Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        await GeneralLedgerPostingIntegrationTests.Post(Request(book,LedgerTransactionKind.Commission,150,1));
        async Task<string> Reconcile(long revision)
        {
            var command=LedgerConfigurationIntegrationTests.Command(book,revision,new() { Action=LedgerConfigurationAction.Reconcile,
                BookId=book.BookId,SourceCut="isolated-recovery/"+revision,Reason="Verify replenished cash before refreshing authority" });
            var result=await new LedgerConfigurationStore(Transactions()).ConfigureAsync(command,x=>command.Complete(x),FinancialCanonicalHash.Compute);
            return result.Receipt.OperatingState;
        }
        (await Reconcile(2)).Should().Be("Overdrawn");
        await GeneralLedgerPostingIntegrationTests.Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,3));
        (await Balance(book)).OperatingState.Should().Be("Overdrawn");
        (await Reconcile(4)).Should().Be("NeedsRefresh");
        var balance=await Balance(book);balance.AvailableCash.Should().Be(50);balance.OperatingState.Should().Be("NeedsRefresh");
    }
    [Fact]
    public async Task Withdrawal_settlement_and_cancellation_preserve_cash_and_obligation_accounting()
    {
        var (book,rules)=await Book();
        await Post(book,rules,LedgerTransactionKind.DepositConfirmed,1000,2);
        var pending=await Post(book,rules,LedgerTransactionKind.WithdrawalRequested,200,3);
        pending.Receipt.JournalId.Should().BeNull(); pending.Receipt.ObligationId.Should().NotBeNull();
        await Post(book,rules,LedgerTransactionKind.WithdrawalSettled,80,4,pending.Receipt.ObligationId);
        var remaining=await Balance(book); remaining.PendingWithdrawals.Should().Be(120); remaining.AvailableCash.Should().Be(800);
        await Post(book,rules,LedgerTransactionKind.WithdrawalCancelled,120,5,pending.Receipt.ObligationId);
        var final=await Balance(book); final.PendingWithdrawals.Should().Be(0); final.AvailableCash.Should().Be(920);
        final.Accounts.Single(x=>x.AccountId==101).Balance.Should().Be(920);
    }

    [Fact]
    public async Task Valuation_replay_and_realization_do_not_double_count_unrealized_profit_or_touch_cash_twice()
    {
        var (book,rules)=await Book(); await Post(book,rules,LedgerTransactionKind.DepositConfirmed,1000,2);
        await Post(book,rules,LedgerTransactionKind.Valuation,40,3,sequence:1);
        (await Balance(book)).AvailableCash.Should().Be(1000);
        var unchanged=await Post(book,rules,LedgerTransactionKind.Valuation,40,4,sequence:2);
        unchanged.Receipt.JournalId.Should().BeNull();
        await Post(book,rules,LedgerTransactionKind.RealizedPnl,50,5,sequence:3);
        var balance=await Balance(book); balance.AvailableCash.Should().Be(1050);
        balance.Accounts.Single(x=>x.AccountId==104).Balance.Should().Be(0);
        balance.Accounts.Single(x=>x.AccountId==105).Balance.Should().Be(0);
        balance.Accounts.Single(x=>x.AccountId==106).Balance.Should().Be(-50);
        var stale=()=>Post(book,rules,LedgerTransactionKind.Valuation,40,6,sequence:2);
        (await FluentActions.Awaiting(stale).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.SourceConflict);
    }

    [Fact]
    public async Task Transfer_is_portfolio_neutral_and_fees_and_settlement_have_the_expected_cash_sign()
    {
        var (book,rules)=await Book(); await Post(book,rules,LedgerTransactionKind.DepositConfirmed,1000,2);
        await Post(book,rules,LedgerTransactionKind.FundTransfer,100,3);
        await Post(book,rules,LedgerTransactionKind.Commission,10,4);
        await Post(book,rules,LedgerTransactionKind.TradeSettlement,-25,5);
        var balances=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        balances.Value!.Accounts.Where(x=>x.AccountId==101).Sum(x=>x.Balance).Should().Be(965);
        (await Balance(book)).AvailableCash.Should().Be(865);
    }

    [Fact]
    public async Task Reversal_remainder_is_enforced_and_retired_original_accounts_can_be_corrected()
    {
        var (book,rules)=await Book(); var deposit=await Post(book,rules,LedgerTransactionKind.DepositConfirmed,100,2);
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            // Isolate the historical-account correction boundary; lifecycle retirement is separately command-tested.
            await db.ExecuteAsync("UPDATE portfolio_financial.ledger_account SET status='Retired' WHERE book_id=$1 AND account_id=102;",[book.BookId],ct);
            return true;
        });
        await Post(book,rules,LedgerTransactionKind.Reversal,40,3,journal:deposit.Receipt.JournalId);
        (await Balance(book)).AvailableCash.Should().Be(60);
        (await FluentActions.Awaiting(()=>Post(book,rules,LedgerTransactionKind.Reversal,61,4,journal:deposit.Receipt.JournalId)).Should().ThrowAsync<FinancialOperationException>())
            .Which.Code.Should().Be(FinancialReasons.ExcessReversal);
        await Post(book,rules,LedgerTransactionKind.Reversal,60,4,journal:deposit.Receipt.JournalId);
        (await Balance(book)).AvailableCash.Should().Be(0);
    }

    [Fact]
    public async Task Opening_balance_cannot_inject_capital_into_an_already_qualified_book()
    {
        var (book,rules)=await Book();
        (await FluentActions.Awaiting(()=>Post(book,rules,LedgerTransactionKind.OpeningBalance,100,2)).Should().ThrowAsync<FinancialOperationException>())
            .Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        (await Balance(book)).AvailableCash.Should().Be(0);
    }

    internal static async Task<(FinancialBookConfiguration,Dictionary<LedgerTransactionKind,LedgerPostingRule>)> Book(
        Func<FinancialBookConfiguration,FinancialBookConfiguration>? configure = null)
    {
        var book=await CreateBook(b=>
        {
            var configured=b with { Funds=[b.Funds[0],b.Funds[0] with { FundId=b.Funds[0].FundId+100,CanSpend=false }] };
            return configure is null ? configured : configure(configured);
        });
        var accounts=new[] { new LedgerAccountDefinition(104,1,"Asset",PostingSide.Debit,true,""),new LedgerAccountDefinition(105,1,"UnrealizedPnl",PostingSide.Credit,true,""),new LedgerAccountDefinition(106,1,"RealizedPnl",PostingSide.Credit,true,"") }
            .Select(x=>x with { ContentHash=FinancialCanonicalHash.Compute(x) }).ToArray();
        var configuration=LedgerConfigurationIntegrationTests.Command(book,0,new() { Action=LedgerConfigurationAction.AddAccountVersion,BookId=book.BookId,Accounts=accounts,Reason="Add accounting fixtures" });
        var store=new LedgerConfigurationStore(Transactions()); await store.ConfigureAsync(configuration,x=>configuration.Complete(x),FinancialCanonicalHash.Compute);
        var rules=Enum.GetValues<LedgerTransactionKind>().Where(x=>x!=LedgerTransactionKind.Undefined).ToDictionary(kind=>kind,kind=>
        {
            var (debit,credit)=kind switch
            {
                LedgerTransactionKind.WithdrawalSettled=>(102,101),LedgerTransactionKind.Commission=>(103,101),
                LedgerTransactionKind.Valuation=>(104,105),LedgerTransactionKind.RealizedPnl=>(101,106),LedgerTransactionKind.FundTransfer=>(101,101),_=>(101,102)
            };
            var rule=new LedgerPostingRule(Guid.NewGuid(),1,"",kind,new(debit,1),new(credit,1),false,new(104,1),new(105,1));
            return rule with { ContentHash=FinancialCanonicalHash.Compute(rule) };
        });
        configuration=LedgerConfigurationIntegrationTests.Command(book,1,new() { Action=LedgerConfigurationAction.AddPostingRuleVersion,BookId=book.BookId,Rules=rules.Values.ToArray(),PeriodStart=new(2020,1,1),Reason="Add all kind fixtures" });
        await store.ConfigureAsync(configuration,x=>configuration.Complete(x),FinancialCanonicalHash.Compute);
        return (book,rules);
    }

    internal static Task<LedgerPostingCompletedEvent> Post(FinancialBookConfiguration book,Dictionary<LedgerTransactionKind,LedgerPostingRule> rules,LedgerTransactionKind kind,decimal amount,long revision,
        Guid? obligation=null,long? journal=null,long sequence=1)
    {
        var template=Request(book,LedgerTransactionKind.DepositConfirmed,amount,revision); var rule=rules[kind];
        var request=template with { Body=template.Body with { TransactionKind=kind,RelatedObligationId=obligation,RelatedJournalId=journal,
            CounterpartyFundId=kind==LedgerTransactionKind.FundTransfer?book.Funds[1].FundId:null,
            PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },
            Source=template.Body.Source with { OrderId=10,TradeId=20,SourceSequence=sequence } } };
        request=request with { InputSha256=FinancialCanonicalHash.Request(request) };
        return new GeneralLedgerStore(Transactions()).PostAsync(request,[new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),request.Body)],
            (body,selected,prior,original,remaining)=>LedgerPostingModel.Calculate(body,selected,prior,original,remaining,true),info=>request.Complete(info));
    }
    static async Task<FinancialBalanceSnapshot> Balance(FinancialBookConfiguration book)=>(await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest())).Value!;
}
