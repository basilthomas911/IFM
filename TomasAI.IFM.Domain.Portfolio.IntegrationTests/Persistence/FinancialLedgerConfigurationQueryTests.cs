using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-03")]
public sealed class FinancialLedgerConfigurationQueryTests
{
    [Fact]
    public async Task Configured_selectors_and_latest_reconciliation_are_read_at_one_revision()
    {
        var book=await CreateBook();await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var command=LedgerConfigurationIntegrationTests.Command(book,1,new() { Action=LedgerConfigurationAction.Reconcile,
            BookId=book.BookId,SourceCut="qualified-query:1",Reason="Check ledger control selectors" });
        await new LedgerConfigurationStore(Transactions()).ConfigureAsync(command,x=>command.Complete(x),FinancialCanonicalHash.Compute);
        var read=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,
            Access=new("integration",["PortfolioAdministrator"]) },new GetFinancialLedgerConfigurationRequest());
        read.Status.Should().Be(FinancialReadStatus.Found);read.FinancialRevision.Should().Be(2);
        var value=read.Value!;value.BookId.Should().Be(book.BookId);value.Accounts.Should().HaveCount(3);
        value.Rules.Should().HaveCount(3);value.Periods.Should().ContainSingle();value.Periods[0].State.Should().Be("Open");
        value.Periods[0].Version.Should().Be(1);
        value.LatestReconciliation!.Debits.Should().Be(100);value.LatestReconciliation.Credits.Should().Be(100);
        value.LatestReconciliation.Differences.Should().BeEmpty();value.LatestReconciliation.ReconciliationId.Should().Be(command.OperationId);
    }
    [Fact]
    public async Task Fund_scoped_request_cannot_be_mistaken_for_Portfolio_wide_controls()
    {
        var book=await CreateBook();
        var action=()=>new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,
            FundId=book.Funds[0].FundId,Access=new("integration",["PortfolioAdministrator"]) },new GetFinancialLedgerConfigurationRequest());
        (await FluentActions.Awaiting(action).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.InvalidContract);
    }
}
