using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-03")]
[Collection("PortfolioFinancialDatabase")]
public sealed class GeneralLedgerPostingIntegrationTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    internal static PostgresEventTransaction Transactions()=>new(new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
        "Host=localhost;Port=5432;Database=event-source-test-db","System.Data.Postgres"));

    [Fact]
    public async Task Posting_replays_original_event_after_later_commands_without_reposting_money()
    {
        var book=await CreateBook();
        var first=Request(book,LedgerTransactionKind.DepositConfirmed,1000,0);
        var original=await Post(first);
        var second=await Post(Request(book,LedgerTransactionKind.DepositConfirmed,50,1));
        var replay=await Post(first);
        replay.Id.Should().Be(original.Id); replay.Receipt.FinancialRevision.Should().Be(1);
        second.Receipt.FinancialRevision.Should().Be(2);
        (await Cash(book)).Should().Be(1050);
        var saved=await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,first.OperationId,first.InputSha256);
        saved!.Receipt.JournalId.Should().Be(original.Receipt.JournalId);
    }

    [Fact]
    public async Task Duplicate_source_under_new_operation_does_not_create_a_second_posting()
    {
        var book=await CreateBook(); var first=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        await Post(first);
        var second=Request(book,LedgerTransactionKind.DepositConfirmed,100,1) with { Body=first.Body };
        second=second with { InputSha256=FinancialCanonicalHash.Request(second) };
        var error=await FluentActions.Awaiting(()=>Post(second)).Should().ThrowAsync<FinancialOperationException>();
        error.Which.Code.Should().Be(FinancialReasons.AlreadyPosted); error.Which.ExistingOperationId.Should().Be(first.OperationId);
        (await Cash(book)).Should().Be(100);
    }

    [Fact]
    public async Task Concurrent_duplicate_commands_return_the_same_committed_receipt()
    {
        var book=await CreateBook(); var request=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        var results=await Task.WhenAll(Post(request),Post(request));
        results[0].Id.Should().Be(results[1].Id); (await Cash(book)).Should().Be(100);
    }

    [Fact]
    public async Task Competing_withdrawals_cannot_spend_the_same_cash_twice()
    {
        var book=await CreateBook(); await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1000,0));
        async Task<bool> Attempt()
        {
            try { await Post(Request(book,LedgerTransactionKind.WithdrawalRequested,700,1)); return true; }
            catch(FinancialOperationException error) when(error.Code is FinancialReasons.RevisionConflict or FinancialReasons.InsufficientCash) { return false; }
        }
        (await Task.WhenAll(Attempt(),Attempt())).Count(x=>x).Should().Be(1);
        (await Cash(book)).Should().Be(1000);
        var pending=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT sum(amount) FROM portfolio_financial.financial_encumbrance WHERE book_id=$1 AND status='Pending';",[book.BookId],ct));
        pending.Should().Be(700m);
    }

    [Fact]
    public async Task Settlement_posts_actual_loss_even_when_cash_becomes_negative()
    {
        var book=await CreateBook(); await Post(Request(book,LedgerTransactionKind.DepositConfirmed,100,0));
        await Post(Request(book,LedgerTransactionKind.Commission,150,1));
        (await Cash(book)).Should().Be(-50);
        var status=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT operating_state FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[book.PortfolioId],ct));
        status.Should().Be("Overdrawn");
    }

    [Fact]
    public async Task Unbalanced_plan_is_rejected_by_the_database_even_if_domain_validation_is_bypassed()
    {
        var book=await CreateBook(); var request=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        var store=new GeneralLedgerStore(Transactions());
        var action=()=>store.PostAsync(request,Prepare(request),(_,rule,_,_,_)=>new(
            [new(1,rule.Debit,request.Body.FundId,100,0,null,null,"a"),new(2,rule.Credit,request.Body.FundId,0,99.99m,null,null,"b")],0,false,true,new string('A',64)),
            info=>Completed(request,info));
        var failure=await FluentActions.Awaiting(action).Should().ThrowAsync<Npgsql.PostgresException>();
        failure.Which.SqlState.Should().Be("23514");
        failure.Which.MessageText.Should().Be("GL.JOURNAL.UNBALANCED");
        (await Cash(book)).Should().Be(0);
        (await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,request.OperationId)).Should().BeNull();
    }

    internal static async Task<FinancialBookConfiguration> CreateBook(Func<FinancialBookConfiguration,FinancialBookConfiguration>? configure = null)
    {
        var transactions=Transactions(); await new PortfolioFinancialSchema(transactions).InitializeAsync();
        var id=Random.Shared.Next(100000,1000000000);
        var fund=new FinancialFundAuthority { FundId=id+1,CanSpend=true,PortfolioStreamVersion=1,FundStreamVersion=1,PolicyStreamVersion=1,
            Reference=new() { PolicyId=id+2,ValidUntilUtc=DateTime.UtcNow.AddDays(1) } };
        var book=new FinancialBookConfiguration { BookId=id,PortfolioId=id,AccountingEntityId=Guid.NewGuid(),ExecutionAccountReference=Guid.NewGuid().ToString("N"),Environment="Integration",Funds=[fund],MigrationQualified=true };
        if(configure is not null) book=configure(book);
        await transactions.ExecuteAsync(async (db,ct)=>
        {
            foreach(var stream in new[] { $"Portfolio.{id}",$"PortfolioFund.{id}.{id+1}",$"PortfolioFinancialPolicy.{id}.{id+2}" })
            {
                var command=Guid.NewGuid();
                await db.AppendAsync(stream,command,new PortfolioCreated(Guid.NewGuid(),command,1,DateTime.UtcNow,"integration",new()),0,ct);
            }
            return true;
        });
        await new PortfolioFinancialDbContext(transactions).CreateBookAsync(book,
            [new(101,1,"Cash",PostingSide.Debit,true,"cash"),new(102,1,"Equity",PostingSide.Credit,true,"equity"),new(103,1,"Expense",PostingSide.Debit,true,"expense")],
            [Rule(LedgerTransactionKind.DepositConfirmed),Rule(LedgerTransactionKind.WithdrawalRequested),Rule(LedgerTransactionKind.Commission)],
            new DateOnly(2020,1,1),new DateOnly(2099,12,31));
        return book;
    }
    static LedgerPostingRule Rule(LedgerTransactionKind kind)=>new(new Guid((int)kind,0,0,new byte[8]),1,$"rule-{kind}",kind,
        new(kind==LedgerTransactionKind.Commission?103:101,1),new(kind==LedgerTransactionKind.Commission?101:102,1),kind!=LedgerTransactionKind.WithdrawalRequested);
    internal static PostFundTransactionCommand Request(FinancialBookConfiguration book,LedgerTransactionKind kind,decimal amount,long revision)
    {
        var rule=Rule(kind); var operation=Guid.NewGuid(); var now=DateTime.UtcNow;
        var request=new PostFundTransactionCommand { CommandId=operation,OperationId=operation,PortfolioId=book.PortfolioId,EntityId=new(book.PortfolioId),
            Subject=new(ActorType.Command,PostFundTransactionCommand.Actor,PostFundTransactionCommand.Verb,book.PortfolioId.ToString()),
            CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(2),ExpectedFinancialRevision=revision,
            Access=new("integration",["PortfolioAdministrator"]),Body=new() { BookId=book.BookId,FundId=book.Funds[0].FundId,Currency="USD",Amount=amount,
                TransactionKind=kind,AccountingDate=DateOnly.FromDateTime(now),ValueDate=DateOnly.FromDateTime(now),
                Source=new() { System="Integration",SourceEventId=Guid.NewGuid(),SourceContentHash=Guid.NewGuid().ToString("N"),OccurredAtUtc=now },
                PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },MovementEvidence=new() { Status=MovementStatus.Confirmed,SourceReference="integration-movement" } } };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
    static PreparedLedgerPosting[] Prepare(PostFundTransactionCommand request)=>[new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),request.Body)];
    internal static Task<LedgerPostingCompletedEvent> Post(PostFundTransactionCommand request)=>new GeneralLedgerStore(Transactions()).PostAsync(request,Prepare(request),
        (item,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(item,rule,prior,original,remaining),info=>Completed(request,info));
    static LedgerPostingCompletedEvent Completed(PostFundTransactionCommand request,LedgerCommitInfo info)=>new()
    {
        Id=info.EventId,OperationId=request.OperationId,CommandId=request.CommandId,PortfolioId=request.PortfolioId,EntityId=request.EntityId,
        Subject=new(ActorType.Event,PostFundTransactionCommand.Actor,nameof(LedgerPostingCompletedEvent),request.EntityId.Format()),
        CommittedAtUtc=info.CommittedAtUtc,ReceivedOn=info.CommittedAtUtc,InputHash=request.InputSha256,CorrelationId=request.CorrelationId,CausationId=request.CausationId,
        Receipt=new() { OperationId=request.OperationId,PortfolioId=request.PortfolioId,BookId=request.Body.BookId,FundId=request.Body.FundId,
            TransactionId=info.Items[0].TransactionId,JournalId=info.Items[0].JournalId,JournalHash=info.Items[0].JournalHash,InputHash=request.InputSha256,
            FinancialRevision=info.Revision,CommittedAtUtc=info.CommittedAtUtc,CompletedEventId=info.EventId,Source=request.Body.Source,ObligationId=info.Items[0].ObligationId }
    };
    static async Task<decimal> Cash(FinancialBookConfiguration book)=>Convert.ToDecimal(await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync(
        "SELECT coalesce(sum(balance),0) FROM portfolio_financial.ledger_account_balance WHERE book_id=$1 AND account_id=101;",[book.BookId],ct)));
}
