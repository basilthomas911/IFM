using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"), Trait("Category", "PortfolioFinancial"), Trait("Gate", "PF-FIN-06")]
public sealed class DevelopmentOpeningCapitalIntegrationTests
{
    [Fact]
    public async Task Posting_choices_expose_development_capital_only_from_the_trusted_host_policy_and_unqualified_book()
    {
        var (book,_)=await LedgerAccountingIntegrationTests.Book(b=>b with { Environment="Emulator",MigrationQualified=false });
        var scope=new FinancialReadScope { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,Access=new("test",["PortfolioAdministrator"]) };
        var request=new GetFinancialPostingConfigurationRequest(new(2026,9,8));
        (await new FinancialQueryStore(Transactions()).ReadAsync(scope,request)).Value!.AllowDevelopmentOpeningCapital.Should().BeFalse();
        (await new FinancialQueryStore(Transactions(),new(true)).ReadAsync(scope,request)).Value!.AllowDevelopmentOpeningCapital.Should().BeTrue();
        var (qualified,_)=await LedgerAccountingIntegrationTests.Book(b=>b with { Environment="Emulator" });
        (await new FinancialQueryStore(Transactions(),new(true)).ReadAsync(scope with { PortfolioId=qualified.PortfolioId,FundId=qualified.Funds[0].FundId },request))
            .Value!.AllowDevelopmentOpeningCapital.Should().BeFalse();
    }
    [Theory]
    [InlineData(null, "Emulator", false, "DevelopmentOpeningCapital")]
    [InlineData(false, "Emulator", false, "DevelopmentOpeningCapital")]
    [InlineData(true, "Live", false, "DevelopmentOpeningCapital")]
    [InlineData(true, "Development", false, "DevelopmentOpeningCapital")]
    [InlineData(true, "Emulator", true, "DevelopmentOpeningCapital")]
    [InlineData(true, "Emulator", false, "BankDeposit")]
    public async Task Disallowed_opening_capital_commits_no_money_receipt_source_or_event(
        bool? developmentHost, string bookEnvironment, bool qualified, string sourceSystem)
    {
        var (book, rules)=await LedgerAccountingIntegrationTests.Book(b=>b with
        { Environment=bookEnvironment, MigrationQualified=qualified });
        var command=Opening(book,rules,sourceSystem);
        var policy=developmentHost is { } enabled ? new FinancialDevelopmentPolicy(enabled) : null;

        var failure=await FluentActions.Awaiting(()=>PostOpening(command,policy)).Should().ThrowAsync<FinancialOperationException>();
        failure.Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        await AssertBook(book,expectedCash:0,expectedRevision:2);
        (await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,command.OperationId))
            .Should().BeNull();
        var counts=await Transactions().ExecuteAsync(async(db,ct)=>
        {
            var postings=await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.ledger_transaction WHERE operation_id=$1;",[command.OperationId],ct);
            var sources=await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.financial_source_receipt WHERE operation_id=$1;",[command.OperationId],ct);
            return (Postings:Convert.ToInt64(postings),Sources:Convert.ToInt64(sources));
        });
        counts.Should().Be((0L,0L));
    }

    [Fact]
    public async Task Denied_opening_item_rolls_back_an_earlier_valid_deposit_in_the_same_batch()
    {
        var (book,rules)=await LedgerAccountingIntegrationTests.Book(b=>b with { Environment="Emulator",MigrationQualified=false });
        var opening=Opening(book,rules,"DevelopmentOpeningCapital");
        var rule=rules[LedgerTransactionKind.DepositConfirmed];
        var deposit=opening.Body with
        {
            TransactionKind=LedgerTransactionKind.DepositConfirmed,
            PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },
            Source=opening.Body.Source with { System="Integration",SourceEventId=Guid.NewGuid() }
        };
        LedgerPostingRequest[] items=[deposit,opening.Body];
        var command=new PostFundTransactionsCommand
        {
            CommandId=opening.CommandId,OperationId=opening.OperationId,PortfolioId=book.PortfolioId,
            EntityId=opening.EntityId,CorrelationId=opening.CorrelationId,CausationId=opening.CausationId,
            RequestedAtUtc=opening.RequestedAtUtc,ExpiresAtUtc=opening.ExpiresAtUtc,
            ExpectedFinancialRevision=2,Access=opening.Access,
            Body=new() { BookId=book.BookId,Items=items,ManifestHash=FinancialCanonicalHash.Compute(items) }
        };
        command=command with { InputSha256=FinancialCanonicalHash.Request(command) };
        var failure=await FluentActions.Awaiting(()=>new GeneralLedgerStore(Transactions(),new(false)).PostAsync(command,
            items.Select(x=>new PreparedLedgerPosting(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),x)).ToArray(),
            (body,selected,prior,original,remaining)=>LedgerPostingModel.Calculate(body,selected,prior,original,remaining,true),
            info=>command.Complete(info))).Should().ThrowAsync<FinancialOperationException>();
        failure.Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        await AssertBook(book,expectedCash:0,expectedRevision:2);
        (await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingBatchCompletedEvent>(book.PortfolioId,command.OperationId))
            .Should().BeNull();
        var sources=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync(
            "SELECT count(*) FROM portfolio_financial.financial_source_receipt WHERE operation_id=$1;",[command.OperationId],ct));
        Convert.ToInt64(sources).Should().Be(0);
    }

    [Fact]
    public async Task Explicit_development_opening_is_audited_and_replay_does_not_add_capital_or_enable_spending()
    {
        var (book,rules)=await LedgerAccountingIntegrationTests.Book(b=>b with { Environment="Emulator",MigrationQualified=false });
        var command=Opening(book,rules,"DevelopmentOpeningCapital");
        var original=await PostOpening(command,new(true));
        original.Receipt.Source.System.Should().Be("DevelopmentOpeningCapital");
        original.Receipt.JournalId.Should().NotBeNull();
        // Recovery may read/replay committed truth after a restart with development funding disabled.
        // This must never create another journal or silently relabel the source as an external deposit.
        var replay=await PostOpening(command,new(false));
        replay.Id.Should().Be(original.Id);
        await AssertBook(book,expectedCash:100,expectedRevision:3);
        var saved=await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(book.PortfolioId);
        saved!.MigrationQualified.Should().BeFalse();
    }

    static PostFundTransactionCommand Opening(FinancialBookConfiguration book,
        Dictionary<LedgerTransactionKind,LedgerPostingRule> rules,string sourceSystem)
    {
        var template=Request(book,LedgerTransactionKind.DepositConfirmed,100,2);
        var rule=rules[LedgerTransactionKind.OpeningBalance];
        var command=template with { Body=template.Body with
        {
            TransactionKind=LedgerTransactionKind.OpeningBalance,
            PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },
            Source=template.Body.Source with { System=sourceSystem }
        }};
        return command with { InputSha256=FinancialCanonicalHash.Request(command) };
    }

    static Task<LedgerPostingCompletedEvent> PostOpening(PostFundTransactionCommand command,FinancialDevelopmentPolicy? policy)
        =>new GeneralLedgerStore(Transactions(),policy).PostAsync(command,
            [new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),command.Body)],
            (body,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(body,rule,prior,original,remaining,true),
            info=>command.Complete(info));

    static async Task AssertBook(FinancialBookConfiguration book,decimal expectedCash,long expectedRevision)
    {
        var snapshot=await new FinancialQueryStore(Transactions()).ReadAsync(new()
        { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        snapshot.Value!.Accounts.Where(x=>x.AccountId==101).Sum(x=>x.Balance).Should().Be(expectedCash);
        snapshot.FinancialRevision.Should().Be(expectedRevision);
        if(!book.MigrationQualified) snapshot.Value.OperatingState.Should().Be("Importing");
    }
}
