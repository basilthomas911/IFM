using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-02")]
public sealed class FinancialCommitUncertaintyTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Lost_real_commit_acknowledgement_recovers_original_receipt_or_reports_unknown_without_duplicate_money(bool refuseRecovery)
    {
        var book=await CreateBook(); var request=Request(book,LedgerTransactionKind.DepositConfirmed,1000,0);
        await using var proxy=new LostCommitReplyProxy(refuseRecovery);
        var store=new GeneralLedgerStore(ProxiedTransactions(proxy.Port));
        var attempt=()=>store.PostAsync(request,
            [new PreparedLedgerPosting(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),request.Body)],
            (item,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(item,rule,prior,original,remaining),info=>request.Complete(info));
        LedgerPostingCompletedEvent? reply=null;
        if(refuseRecovery) await FluentActions.Awaiting(attempt).Should().ThrowAsync<FunctionCommitOutcomeUnknownException>();
        else reply=await attempt();
        proxy.Dropped.Should().BeTrue("the proxy observed and suppressed PostgreSQL's actual COMMIT response");
        var committed=await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,request.OperationId);
        committed.Should().NotBeNull(); if(reply is not null) reply.Id.Should().Be(committed!.Id);
        var retry=await Post(request); retry.Id.Should().Be(committed!.Id);
        var balance=await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,
            Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        balance.FinancialRevision.Should().Be(1); balance.Value!.AvailableCash.Should().Be(1000);
    }

    internal static PostgresEventTransaction ProxiedTransactions(int port)=>new(new DbConnectionSettings().Add(
        EventSourceActorDbContext.EventSourceActorDbConnection,$"Host=127.0.0.1;Port={port};Database=event-source-test-db;SSL Mode=Disable;Timeout=2","System.Data.Postgres"));
}
