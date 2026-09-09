using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-07")]
public sealed class FinancialPayloadBoundaryTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Throwing_diagnostic_listener_cannot_turn_confirmed_posting_into_a_failure()
    {
        var book=await CreateBook();var request=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        using var listener=new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished=(instrument,owner)=> { if(instrument.Meter.Name==FinancialTelemetry.MeterName) owner.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<double>((_,_,_,_)=>throw new InvalidOperationException("Injected observer failure"));
        listener.SetMeasurementEventCallback<long>((_,_,_,_)=>throw new InvalidOperationException("Injected observer failure"));listener.Start();
        var completed=await Post(request);listener.Dispose();
        var saved=await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,request.OperationId,request.InputSha256);
        saved!.Id.Should().Be(completed.Id);saved.Receipt.FinancialRevision.Should().Be(1);
        var balance=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,Access=request.Access },new GetAccountBalancesRequest());
        balance.Value!.AvailableCash.Should().Be(100);
    }

    [Fact]
    public async Task Maximum_256_line_adjustment_commits_balanced_totals_but_257_lines_has_no_effect()
    {
        var (book,rules)=await LedgerAccountingIntegrationTests.Book();
        var template=Request(book,LedgerTransactionKind.DepositConfirmed,128,2);var rule=rules[LedgerTransactionKind.Adjustment];
        var request=template with { Body=template.Body with { TransactionKind=LedgerTransactionKind.Adjustment,
            PostingRule=new() { RuleId=rule.RuleId,Version=rule.Version,ContentHash=rule.ContentHash },
            Lines=Enumerable.Range(0,256).Select(i=>new LedgerEntryDraft { Ordinal=i+1,FundId=book.Funds[0].FundId,AccountId=i%2==0?101:102,Amount=1,Currency="USD",
                PostingSide=i%2==0?PostingSide.Debit:PostingSide.Credit,SourceLineReference=$"boundary:{i}" }).ToArray() } };
        request=request with { InputSha256=FinancialCanonicalHash.Request(request) };
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await PostLines(request,deadline.Token);
        var counts=await Transactions().ExecuteAsync((db,ct)=>db.QueryAsync("SELECT count(*),sum(debit),sum(credit) FROM portfolio_financial.ledger_entry WHERE book_id=$1;",
            [book.BookId],r=>(Count:r.GetInt64(0),Debit:r.GetDecimal(1),Credit:r.GetDecimal(2)),ct));
        counts.Single().Should().Be((256L,128m,128m));
        var invalid=request with { CommandId=Guid.NewGuid(),OperationId=Guid.NewGuid(),ExpectedFinancialRevision=3,
            Body=request.Body with { Source=request.Body.Source with { SourceEventId=Guid.NewGuid() },Lines=[..request.Body.Lines,request.Body.Lines[0]] } };
        invalid=invalid with { InputSha256=FinancialCanonicalHash.Request(invalid) };
        await FluentActions.Awaiting(()=>PostLines(invalid,default)).Should().ThrowAsync<FinancialOperationException>();
        (await new PortfolioFinancialDbContext(Transactions()).ReadOperationAsync<LedgerPostingCompletedEvent>(book.PortfolioId,invalid.OperationId)).Should().BeNull();
        var balance=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,Access=template.Access },new GetAccountBalancesRequest());
        balance.Value!.AvailableCash.Should().Be(128);balance.FinancialRevision.Should().Be(3);
    }
    [Fact]
    public void Individually_bounded_journals_cannot_bypass_whole_batch_uncompressed_payload_limit()
    {
        var body=new LedgerPostingRequest { Lines=Enumerable.Range(0,256).Select(i=>new LedgerEntryDraft { AccountId=101,Amount=1,Currency="USD",
            PostingSide=PostingSide.Debit,SourceLineReference=new string('X',128)+i }).ToArray() };
        var now=DateTime.UtcNow;var id=Guid.NewGuid();
        var command=new PostFundTransactionsCommand { PortfolioId=1,EntityId=new(1),CommandId=id,OperationId=id,CorrelationId=Guid.NewGuid(),CausationId=Guid.NewGuid(),
            RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(1),Access=new("fixture",["PortfolioAdministrator"]),
            Subject=new(ActorType.Command,PostFundTransactionsCommand.Actor,PostFundTransactionsCommand.Verb,new LedgerPortfolioId(1).Format()),
            Body=new() { Items=Enumerable.Repeat(body,100).ToArray() } };
        command=command with { InputSha256=FinancialCanonicalHash.Request(command) };
        var errors=new List<ValidationError>().ValidateFinancialRequest<PostFundTransactionsCommand,LedgerPostingBatchRequest>(command,
            ActorType.Command,PostFundTransactionsCommand.Actor,PostFundTransactionsCommand.Verb);
        errors.Should().ContainSingle(x=>x.ErrorMessage=="Financial request exceeds 1 MiB.");
    }
    static Task<LedgerPostingCompletedEvent> PostLines(PostFundTransactionCommand request,CancellationToken token)
        =>new GeneralLedgerStore(Transactions()).PostAsync(request,[new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),request.Body)],
            (body,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(body,rule,prior,original,remaining,true),request.Complete,token);
}
