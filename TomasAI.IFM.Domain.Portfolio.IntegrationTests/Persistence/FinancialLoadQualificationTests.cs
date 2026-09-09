using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit.Abstractions;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.CapacityReservationIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancialLoad"),Trait("Gate","PF-FIN-07")]
public sealed class FinancialLoadQualificationTests(PortfolioEventStoreFixture fixture,ITestOutputHelper output):IClassFixture<PortfolioEventStoreFixture>
{
    [Theory]
    [InlineData("single")][InlineData("independent")][InlineData("shared-admission")][InlineData("batch100")]
    public async Task Actual_financial_paths_meet_declared_development_budget_and_reconcile(string workload)
    {
        var independent=workload=="independent";var shared=workload=="shared-admission";var batch=workload=="batch100";
        var books=new List<FinancialBookConfiguration>();
        for(var i=0;i<(independent?8:1);i++) books.Add(shared?await FundedBook():await CreateBook());
        var reservations=new List<ReservePortfolioTradeRiskCommand>();
        if(shared) for(int i=0;i<16;i++) reservations.Add(await ReserveRequest(books[0],700));
        // Warm serialization, pooled connection and event metadata outside measured work.
        var warm=await CreateBook();await Post(Request(warm,LedgerTransactionKind.DepositConfirmed,1,0));
        var durations=new ConcurrentBag<double>();var locks=new ConcurrentBag<double>();var outcomes=new ConcurrentDictionary<string,long>();long retries=0;
        using var listener=new MeterListener();
        listener.InstrumentPublished=(instrument,owner)=> { if(instrument.Meter.Name==FinancialTelemetry.MeterName) owner.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<double>((instrument,value,tags,state)=> { if(instrument.Name=="financial.authority.lock.duration") locks.Add(value); });
        listener.SetMeasurementEventCallback<long>((instrument,value,tags,state)=>
        {
            if(instrument.Name=="financial.transaction.rollback_retries") Interlocked.Add(ref retries,value);
            if(instrument.Name=="financial.transaction.outcomes") foreach(var tag in tags) if(tag.Key=="outcome") outcomes.AddOrUpdate((string)tag.Value!,value,(_,old)=>old+value);
        });listener.Start();
        long allocated=GC.GetTotalAllocatedBytes(true);long heap=GC.GetTotalMemory(false);long working=Process.GetCurrentProcess().WorkingSet64;
        var stopwatch=Stopwatch.StartNew();int committed=0;int conflicts=0;
        async Task Measure(Func<Task> operation)
        {
            var started=Stopwatch.GetTimestamp();
            try { await operation();Interlocked.Increment(ref committed); }
            catch(FinancialOperationException error) when(shared && error.Code==FinancialReasons.RevisionConflict) { Interlocked.Increment(ref conflicts); }
            finally { durations.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
        }
        if(shared) await Task.WhenAll(reservations.Select(request=>Measure(async()=> { await Reserve(request); })));
        else if(independent) await Task.WhenAll(books.Select(async book=>
        {
            for(int revision=0;revision<8;revision++) await Measure(async()=> { await Post(Request(book,LedgerTransactionKind.DepositConfirmed,1,revision)); });
        }));
        else if(batch) for(int revision=0;revision<5;revision++) await Measure(()=>Batch(books[0],revision));
        else for(int revision=0;revision<40;revision++) await Measure(async()=> { await Post(Request(books[0],LedgerTransactionKind.DepositConfirmed,1,revision)); });
        stopwatch.Stop();
        var allocations=GC.GetTotalAllocatedBytes(true)-allocated;var ordered=durations.Order().ToArray();
        double Percentile(double p)=>ordered[(int)Math.Ceiling(p*ordered.Length)-1];
        var record=new { Workload=workload,Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            OS=System.Runtime.InteropServices.RuntimeInformation.OSDescription,Processors=Environment.ProcessorCount,
            Operations=ordered.Length,Committed=committed,Conflicts=conflicts,ElapsedMs=stopwatch.Elapsed.TotalMilliseconds,
            OperationsPerSecond=ordered.Length/stopwatch.Elapsed.TotalSeconds,P50Ms=Percentile(.50),P95Ms=Percentile(.95),P99Ms=Percentile(.99),
            ProcessAllocatedBytes=allocations,AllocatedBytesPerOperation=allocations/ordered.Length,HeapBefore=heap,HeapAfter=GC.GetTotalMemory(false),
            WorkingSetBefore=working,WorkingSetAfter=Process.GetCurrentProcess().WorkingSet64,ConfirmedRollbackRetries=retries,
            TransactionOutcomes=outcomes,SuccessfulLockAcquisitions=locks.Count,MaximumLockAcquisitionMs=locks.DefaultIfEmpty(0).Max() };
        listener.Dispose();
        var json=JsonSerializer.Serialize(record,new JsonSerializerOptions { WriteIndented=true });output.WriteLine(json);
        var directory=Path.Combine(AppContext.BaseDirectory,"TestResults","financial-load");Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,$"{workload}.json"),json);
        Percentile(.99).Should().BeLessThanOrEqualTo(2000);
        if(!batch && !shared) Percentile(.95).Should().BeLessThanOrEqualTo(1000);
        (allocations/ordered.Length).Should().BeLessThanOrEqualTo((batch?128L:32L)*1024*1024);
        outcomes.Keys.Should().NotContain(new[] { "unknown","lock_timeout","statement_timeout","cancelled" });locks.Should().NotBeEmpty();
        committed.Should().Be(shared?1:independent?64:batch?5:40);conflicts.Should().Be(shared?15:0);
        foreach(var book in books)
        {
            var read=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,FundId=book.Funds[0].FundId,
                Access=new("load-fixture",["PortfolioAdministrator"]) },new GetAccountBalancesRequest());
            read.Value!.AvailableCash.Should().Be(shared?300:independent?8:batch?500:40);
            var totals=await Transactions().ExecuteAsync((db,ct)=>db.QueryAsync("SELECT coalesce(sum(debit),0),coalesce(sum(credit),0) FROM portfolio_financial.ledger_entry WHERE book_id=$1;",
                [book.BookId],r=>(Debit:r.GetDecimal(0),Credit:r.GetDecimal(1)),ct));
            totals.Single().Debit.Should().Be(totals.Single().Credit);
            totals.Single().Debit.Should().Be(shared?1000:independent?8:batch?500:40);
        }
    }
    static async Task Batch(FinancialBookConfiguration book,int revision)
    {
        var template=Request(book,LedgerTransactionKind.DepositConfirmed,1,revision);
        var items=Enumerable.Range(0,100).Select(_=>template.Body with { Source=template.Body.Source with { SourceEventId=Guid.NewGuid() } }).ToArray();
        var command=new PostFundTransactionsCommand { CommandId=template.CommandId,OperationId=template.OperationId,PortfolioId=book.PortfolioId,EntityId=template.EntityId,
            Subject=new(ActorType.Command,PostFundTransactionsCommand.Actor,PostFundTransactionsCommand.Verb,template.EntityId.Format()),
            CorrelationId=template.CorrelationId,CausationId=template.CausationId,RequestedAtUtc=template.RequestedAtUtc,ExpiresAtUtc=template.ExpiresAtUtc,
            ExpectedFinancialRevision=revision,Access=template.Access,Body=new() { BookId=book.BookId,Items=items,ManifestHash=FinancialCanonicalHash.Compute(items) } };
        command=command with { InputSha256=FinancialCanonicalHash.Request(command) };
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await new GeneralLedgerStore(Transactions()).PostAsync(command,
            items.Select(x=>new PreparedLedgerPosting(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),x)).ToArray(),
            (body,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(body,rule,prior,original,remaining),command.Complete,deadline.Token);
    }
}
