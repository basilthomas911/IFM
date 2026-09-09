using FluentAssertions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Projection;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-02")]
public sealed class FinancialHistoryIntegrationTests(PortfolioEventStoreFixture events,PortfolioDbFixture scylla)
    :IClassFixture<PortfolioEventStoreFixture>,IClassFixture<PortfolioDbFixture>
{
    [Fact]
    public async Task Real_scylla_history_replays_idempotently_without_reposting_postgres_money()
    {
        var book=await CreateBook(); var request=Request(book,LedgerTransactionKind.DepositConfirmed,1000,0);
        var completed=await Post(request);
        var factory=new DbContextFactory(new DbContextResolver(type=>type==typeof(IObjectRepository<PortfolioDbContext>)?scylla.Db:throw new NotSupportedException()));
        var projection=new FinancialHistoryProjection(factory);
        await projection.ApplyAsync(completed); await projection.ApplyAsync(completed);
        var receipt=(await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,request.OperationId))!;
        await projection.ApplyAsync(receipt);
        var rows=await factory.PortfolioDb.Use("FinancialHistory.IntegrationRead","""
            SELECT sourceEventId,payloadJson FROM financial_operation_by_portfolio_month WHERE portfolioId=? AND month=?;
            """).SetParameters(new Values([book.PortfolioId,completed.CommittedAtUtc.Year*100+completed.CommittedAtUtc.Month]))
            .ExecuteQueryAsync(row=>(EventId:row.GetLong(0),Json:row.GetString(1)),CancellationToken.None);
        rows.Should().ContainSingle(); rows.Single().EventId.Should().Be(completed.EventId);
        rows.Single().Json.Should().Contain(completed.Id.ToString());
        var balance=await new FinancialQueryStore(Transactions()).ReadAsync(new FinancialReadScope { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,
            Access=new("integration",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
        balance.Value!.AvailableCash.Should().Be(1000); balance.FinancialRevision.Should().Be(1);
    }

    [Fact]
    public async Task Receipt_scan_recovers_a_lower_event_id_that_commits_after_a_newer_acknowledged_event()
    {
        var firstBook=await CreateBook(); var secondBook=await CreateBook();
        var first=Request(firstBook,LedgerTransactionKind.DepositConfirmed,100,0);
        var gated=new GateBeforeCommit(Transactions());
        var pending=new GeneralLedgerStore(gated).PostAsync(first,
            [new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),first.Body)],
            (item,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(item,rule,prior,original,remaining),info=>first.Complete(info));
        try
        {
            await gated.Prepared.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var second=await Post(Request(secondBook,LedgerTransactionKind.DepositConfirmed,200,0));
            var journal=new FinancialHistoryJournal(Transactions());
            (await journal.PendingAsync(portfolioId:firstBook.PortfolioId)).Should().BeEmpty();
            await journal.AcknowledgeAsync(second.EventId);
            gated.Release.TrySetResult();
            var committed=await pending;
            committed.EventId.Should().BeLessThan(second.EventId);
            var recovered=await journal.PendingAsync(portfolioId:firstBook.PortfolioId);
            recovered.Should().ContainSingle().Which.Id.Should().Be(committed.Id);
            await journal.AcknowledgeAsync(committed.EventId);
            (await journal.PendingAsync(portfolioId:firstBook.PortfolioId)).Should().BeEmpty();
        }
        finally { gated.Release.TrySetResult(); await pending; }
    }

    sealed class GateBeforeCommit(IPostgresEventTransaction inner):IPostgresEventTransaction
    {
        public TaskCompletionSource Prepared { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction,CancellationToken,Task<T>> operation,CancellationToken token=default)
            =>inner.ExecuteAsync(async(db,ct)=> { var result=await operation(db,ct); Prepared.TrySetResult(); await Release.Task.WaitAsync(TimeSpan.FromSeconds(5),ct); return result; },token);
    }
    readonly record struct Values(object?[] Items):IBindValue { public object Bind()=>Items; }
}
