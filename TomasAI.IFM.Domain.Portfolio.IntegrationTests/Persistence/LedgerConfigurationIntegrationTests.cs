using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-03")]
public sealed class LedgerConfigurationIntegrationTests
{
    [Fact]
    public async Task New_book_command_commits_receipt_but_cannot_implicitly_activate_spending()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();
        var id=Random.Shared.Next(100000,1000000000);
        var book=new FinancialBookConfiguration { BookId=id,PortfolioId=id,AccountingEntityId=Guid.NewGuid(),Environment="Integration",ExecutionAccountReference=Guid.NewGuid().ToString(),Funds=[new() { FundId=id+1,PortfolioStreamVersion=1,FundStreamVersion=1 }] };
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            foreach(var stream in new[] { $"Portfolio.{id}",$"PortfolioFund.{id}.{id+1}" })
            {
                var command=Guid.NewGuid();
                await db.AppendAsync(stream,command,new TomasAI.IFM.Domain.Portfolio.Command.Model.PortfolioCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"integration",new()),0,ct);
            }
            return true;
        });
        var accounts=new[] { new LedgerAccountDefinition(1,1,"Cash",PostingSide.Debit,true,""),new LedgerAccountDefinition(2,1,"Equity",PostingSide.Credit,true,"") }
            .Select(x=>x with { ContentHash=FinancialCanonicalHash.Compute(x) }).ToArray();
        var rule=new LedgerPostingRule(Guid.NewGuid(),1,"",LedgerTransactionKind.DepositConfirmed,new(1,1),new(2,1),true);
        rule=rule with { ContentHash=FinancialCanonicalHash.Compute(rule) };
        var request=Command(book,0,new() { Action=LedgerConfigurationAction.CreateBook,BookId=id,Book=book,Accounts=accounts,Rules=[rule],
            PeriodId=Guid.NewGuid(),PeriodStart=new(2026,1,1),PeriodEnd=new(2026,12,31),Reason="Create test book" });
        new List<ValidationError>().ValidateLedgerConfiguration(request).Should().BeEmpty();
        var completed=await Configure(request);
        completed.Receipt.OperatingState.Should().Be("Importing");
        var replay=await Configure(request); replay.Id.Should().Be(completed.Id);
        var stored=await new PortfolioFinancialDbContext(Transactions()).ReadBookAsync(id);
        stored!.MigrationQualified.Should().BeFalse(); stored.Funds.Should().OnlyContain(x=>!x.CanSpend);
        var receipt=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=id,Access=request.Access },new GetPostingReceiptRequest(request.OperationId));
        receipt.Value!.Configuration!.Id.Should().Be(completed.Id);
    }

    [Fact]
    public async Task Reconciliation_reconstructs_journal_totals_and_fences_period_close_against_new_postings()
    {
        var book=await CreateBook(); await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        var reconcile=Command(book,1,new() { Action=LedgerConfigurationAction.Reconcile,BookId=book.BookId,SourceCut="source:1",Reason="Close evidence" });
        var result=await Configure(reconcile);
        result.Receipt.ReconciliationId.Should().Be(reconcile.OperationId);
        var stored=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT status FROM portfolio_financial.ledger_reconciliation WHERE reconciliation_id=$1;",[reconcile.OperationId],ct));
        stored.Should().Be("Matched");
        await Post(Request(book,LedgerTransactionKind.DepositConfirmed,5,2));
        var period=(Guid)(await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT period_id FROM portfolio_financial.ledger_period WHERE book_id=$1;",[book.BookId],ct)))!;
        var close=Command(book,3,new() { Action=LedgerConfigurationAction.ClosePeriod,BookId=book.BookId,PeriodId=period,ExpectedVersion=1,SourceCut="source:1",Reason="Attempt stale closure",ReconciliationId=reconcile.OperationId });
        (await FluentActions.Awaiting(()=>Configure(close)).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.AuthorityDenied);
        var fresh=Command(book,3,reconcile.Body with { SourceCut="source:2" }); await Configure(fresh);
        close=Command(book,4,close.Body with { SourceCut="source:2",ReconciliationId=fresh.OperationId });
        await Configure(close);
        (await FluentActions.Awaiting(()=>Post(Request(book,LedgerTransactionKind.DepositConfirmed,1,5))).Should().ThrowAsync<FinancialOperationException>()).Which.Code.Should().Be(FinancialReasons.ClosedPeriod);
    }

    [Fact]
    public async Task Reconciliation_exposes_corrupted_materialized_balance_and_blocks_admission()
    {
        var book=await CreateBook(); await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            await db.ExecuteAsync("UPDATE portfolio_financial.ledger_account_balance SET debit_total=101,balance=101 WHERE book_id=$1 AND account_id=101;",[book.BookId],ct);
            return true;
        });
        var request=Command(book,1,new() { Action=LedgerConfigurationAction.Reconcile,BookId=book.BookId,SourceCut="source:1",Reason="Verify independent totals" });
        var result=await Configure(request); result.Receipt.OperatingState.Should().Be("NeedsReconciliation");
        var status=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT status FROM portfolio_financial.ledger_reconciliation WHERE reconciliation_id=$1;",[request.OperationId],ct));
        status.Should().Be("Mismatch");
    }

    [Fact]
    public async Task Concurrent_replayed_configuration_has_one_receipt_and_one_revision()
    {
        var book=await CreateBook();
        var request=Command(book,0,new() { Action=LedgerConfigurationAction.Reconcile,BookId=book.BookId,SourceCut="empty:0",Reason="Initial reconciliation" });
        var results=await Task.WhenAll(Configure(request),Configure(request));
        results[0].Id.Should().Be(results[1].Id); results[0].Receipt.FinancialRevision.Should().Be(1);
    }

    internal static ConfigureLedgerCommand Command(FinancialBookConfiguration book,long revision,LedgerConfigurationRequest body)
    {
        var operation=Guid.NewGuid(); var now=DateTime.UtcNow;
        var request=new ConfigureLedgerCommand { OperationId=operation,CommandId=operation,PortfolioId=book.PortfolioId,EntityId=new(book.PortfolioId),
            Subject=new(ActorType.Command,ConfigureLedgerCommand.Actor,ConfigureLedgerCommand.Verb,new LedgerPortfolioId(book.PortfolioId).Format()),
            ExpectedFinancialRevision=revision,RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(1),CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),
            Access=new("integration",["PortfolioAdministrator"]),Body=body };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
    static Task<LedgerConfigurationCompletedEvent> Configure(ConfigureLedgerCommand request)=>new LedgerConfigurationStore(Transactions())
        .ConfigureAsync(request,receipt=>request.Complete(receipt),FinancialCanonicalHash.Compute);
}
