using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-07")]
public sealed class AccountingExportIntegrationTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    static readonly FinancialAccess Access=new("export-integration",["PortfolioAdministrator"]);
    static AccountingExportStore Store()=>new(Transactions());
    static AccountingExportRequest Export(FinancialBookConfiguration book,long revision,params long[] journals)
        =>new(Guid.NewGuid(),book.PortfolioId,book.BookId,revision,journals,
            new("isolated-company/"+book.PortfolioId,1,[new(101,1,"cash"),new(102,1,"equity"),new(103,1,"expense")]));

    [Fact]
    public async Task Restart_and_replay_return_the_original_payload_without_reposting_money()
    {
        var book=await CreateBook();var deposit=await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var request=Export(book,1,deposit.Receipt.JournalId!.Value);
        var original=await Store().PrepareAsync(request,Access,AccountingExportModel.Create);
        var replay=await Store().PrepareAsync(request,Access,AccountingExportModel.Create);
        replay.PayloadHash.Should().Be(original.PayloadHash);replay.RequestHash.Should().Be(original.RequestHash);
        var restored=await Store().ReadAsync(book.PortfolioId,request.Mapping.DestinationCompany,request.ExportId,Access);
        restored!.PayloadHash.Should().Be(original.PayloadHash);restored.DeliveryStatus.Should().Be("Pending");
        restored.Payload.Journals[0].Lines.Select(x=>(x.ExternalAccountReference,x.Debit,x.Credit))
            .Should().Equal(("cash",100m,0m),("equity",0m,100m));
        var balances=await Balance(book);balances.FinancialRevision.Should().Be(1);balances.Value!.AvailableCash.Should().Be(100);
        (await FluentActions.Awaiting(()=>Store().PrepareAsync(request with { Mapping=request.Mapping with { Version=2 } },Access,AccountingExportModel.Create))
            .Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.SourceConflict);
    }

    [Fact]
    public async Task Source_journal_cannot_be_included_in_two_exports_even_with_concurrent_preparation()
    {
        var book=await CreateBook();var deposit=await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var first=Export(book,1,deposit.Receipt.JournalId!.Value);
        async Task<bool> Attempt(AccountingExportRequest request)
        {
            try { await Store().PrepareAsync(request,Access,AccountingExportModel.Create);return true; }
            catch(PostgresException error) when(error.SqlState==PostgresErrorCodes.UniqueViolation) { return false; }
        }
        (await Task.WhenAll(Attempt(first),Attempt(first with { ExportId=Guid.NewGuid() }))).Count(x=>x).Should().Be(1);
        var count=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT count(*) FROM portfolio_financial.accounting_export WHERE destination_company=$1;",[first.Mapping.DestinationCompany],ct));
        count.Should().Be(1L); // Losing insert and partial checkpoint rolled back together.
    }

    [Fact]
    public async Task Failed_unknown_and_delivered_attempts_are_idempotent_and_never_modify_the_financial_revision()
    {
        var book=await CreateBook();var deposit=await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var request=Export(book,1,deposit.Receipt.JournalId!.Value);var original=await Store().PrepareAsync(request,Access,AccountingExportModel.Create);
        var attempt=Guid.NewGuid();
        async Task<AccountingExportReceipt> Record(Guid id,string status,string? receipt=null)
            =>await Store().RecordAttemptAsync(book.PortfolioId,request.Mapping.DestinationCompany,request.ExportId,id,status,receipt,Access);
        (await Record(attempt,"Unknown")).Attempts.Should().Be(1);
        (await Record(attempt,"Unknown")).Attempts.Should().Be(1);
        (await Record(Guid.NewGuid(),"Failed")).DeliveryStatus.Should().Be("Pending");
        var delivered=await Record(Guid.NewGuid(),"Delivered","internal-fixture/receipt-one");
        delivered.Attempts.Should().Be(3);delivered.PayloadHash.Should().Be(original.PayloadHash);
        delivered.DeliveryStatus.Should().Be("Delivered");
        await FluentActions.Awaiting(()=>Record(attempt,"Failed")).Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>Record(Guid.NewGuid(),"Delivered","different-receipt")).Should().ThrowAsync<FinancialOperationException>();
        await FluentActions.Awaiting(()=>Record(Guid.NewGuid(),"Unknown")).Should().ThrowAsync<FinancialOperationException>();
        var balance=await Balance(book);balance.FinancialRevision.Should().Be(1);balance.Value!.AvailableCash.Should().Be(100);
    }

    [Fact]
    public async Task A_later_correction_exports_as_a_new_linked_journal_and_does_not_rewrite_the_original()
    {
        var (book,rules)=await LedgerAccountingIntegrationTests.Book();
        var deposit=await LedgerAccountingIntegrationTests.Post(book,rules,LedgerTransactionKind.DepositConfirmed,100,2);
        var originalRequest=Export(book,3,deposit.Receipt.JournalId!.Value);
        var original=await Store().PrepareAsync(originalRequest,Access,AccountingExportModel.Create);
        var correction=await LedgerAccountingIntegrationTests.Post(book,rules,LedgerTransactionKind.Reversal,40,3,journal:deposit.Receipt.JournalId);
        var next=await Store().PrepareAsync(Export(book,4,correction.Receipt.JournalId!.Value),Access,AccountingExportModel.Create);
        next.Payload.Journals[0].ReversesJournalId.Should().Be(deposit.Receipt.JournalId);
        next.Payload.Journals[0].Lines.Sum(x=>x.Debit).Should().Be(40);
        (await Store().ReadAsync(book.PortfolioId,originalRequest.Mapping.DestinationCompany,originalRequest.ExportId,Access))!.PayloadHash.Should().Be(original.PayloadHash);
        (await Balance(book)).Value!.AvailableCash.Should().Be(60);
    }

    [Fact]
    public async Task Alternate_database_path_cannot_rewrite_the_frozen_export_or_its_source_set()
    {
        var book=await CreateBook();var deposit=await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var request=Export(book,1,deposit.Receipt.JournalId!.Value);await Store().PrepareAsync(request,Access,AccountingExportModel.Create);
        var mutate=()=>Transactions().ExecuteAsync((db,ct)=>db.ExecuteAsync("UPDATE portfolio_financial.accounting_export SET payload_hash='changed' WHERE destination_company=$1;",[request.Mapping.DestinationCompany],ct));
        (await FluentActions.Awaiting(mutate).Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
    }

    static Task<FinancialRead<FinancialBalanceSnapshot>> Balance(FinancialBookConfiguration book)
        =>new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,Access=Access },new GetAccountBalancesRequest());
}
