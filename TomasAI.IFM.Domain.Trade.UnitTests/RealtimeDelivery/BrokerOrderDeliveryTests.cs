using System.Reflection;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class BrokerOrderDeliveryTests
{
    static string LedgerPath()=>Path.Combine(Path.GetTempPath(),"ifm-broker-settlement",Guid.NewGuid().ToString("N"),"ledger.json");
    [Theory]
    [InlineData("Futures",false)][InlineData("Futures",true)][InlineData("Option",false)][InlineData("Option",true)]
    public async Task Preaccept_failure_stays_failed_and_fresh_decode_retry_preserves_quote(string kind,bool metrics)
    {
        var bytes=BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input(kind,1));
        await using var fixture=new BrokerOrderDeliveryFixture(metrics:metrics){Failure="throw"};await fixture.Start();
        Assert.False(await fixture.Deliver(BrokerOrderDeliveryFixture.Decode(kind,bytes)));var original=Assert.Single(fixture.Attempts);
        fixture.Failure="none";Assert.True(await fixture.Deliver(BrokerOrderDeliveryFixture.Decode(kind,bytes)));
        Assert.Equal(original,fixture.Attempts[1]);
    }
    [Theory][InlineData("Futures")][InlineData("Option")]
    public async Task Awaited_broker_processing_keeps_successors_in_order(string kind)
    {
        await using var fixture=new BrokerOrderDeliveryFixture();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Publish=async quote=>{if(quote.SourceSequence==1){entered.TrySetResult();await release.Task;}return 0;};await fixture.Start();
        var first=fixture.Deliver(BrokerOrderDeliveryFixture.Decode(kind,BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input(kind,1))));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second=fixture.Deliver(BrokerOrderDeliveryFixture.Decode(kind,BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input(kind,2))));
        try{Assert.False(first.IsCompleted);Assert.False(second.IsCompleted);Assert.Single(fixture.Attempts);}
        finally{release.TrySetResult();}
        Assert.True(await first);Assert.True(await second);Assert.Equal(new long[]{1,2},fixture.Attempts.Select(q=>q.SourceSequence));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task Quote_decoder_lease_survives_await_and_is_released_on_success_or_failure(bool fails)
    {
        var source=(FuturesTickQuoteDataChangedEvent)BrokerOrderDeliveryFixture.Decode("Futures",BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input("Futures",1)));
        var owner=typeof(FuturesTickQuoteDataSegment).GetProperty("Owner",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(source.QuoteData)!;
        Assert.NotNull(owner);var returned=owner.GetType().GetField("_returned",BindingFlags.Instance|BindingFlags.NonPublic)!;
        await using var fixture=new BrokerOrderDeliveryFixture();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Publish=async quote=>{entered.TrySetResult();await release.Task;if(fails)throw new IOException("Injected broker await failure.");return 0;};await fixture.Start();
        var delivery=fixture.Deliver(source);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try{Assert.Equal(0,(int)returned.GetValue(owner)!);Assert.Equal(5500.25m,source.QuoteData.Buffer[1].AskPrice);}
        finally{release.TrySetResult();}
        Assert.Equal(!fails,await delivery);Assert.Equal(1,(int)returned.GetValue(owner)!);
        source.QuoteData.Dispose();Assert.Equal(1,(int)returned.GetValue(owner)!);
    }
    [Fact]
    public async Task Missing_final_bid_is_a_nonapplicable_input_and_does_not_reuse_an_older_book()
    {
        var source=(FuturesTickQuoteDataChangedEvent)BrokerOrderDeliveryFixture.Input("Futures",1);
        source.QuoteData.Buffer[1]=source.QuoteData.Buffer[1] with{BidPrice=null};
        await using var fixture=new BrokerOrderDeliveryFixture();await fixture.Start();
        Assert.True(await fixture.Deliver(BrokerOrderDeliveryFixture.Decode("Futures",BrokerOrderDeliveryFixture.Encode(source))));Assert.Empty(fixture.Attempts);
    }
    [Fact]
    public async Task Actual_committed_full_fill_survives_lost_reply_and_restart_without_another_fill()
    {
        var real=new BrokerEmulatorSettlementFixture(LedgerPath());await real.Place();
        await using(var first=new BrokerOrderDeliveryFixture(){Failure="lost-reply"})
        {
            first.Publish=q=>real.Broker.PublishMarketQuoteAsync(q);await first.Start();
            Assert.False(await first.Deliver(BrokerOrderDeliveryFixture.Decode("Futures",BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input("Futures",1)))));
            Assert.Single(await real.Broker.ReconcileOrderAsync("mailbox-order"),f=>f.Kind==BrokerObservationKind.Execution);
        }
        var cash=(await real.Broker.GetAccountSnapshotAsync()).CashBalance;real.Restart();
        await using var recovered=new BrokerOrderDeliveryFixture();recovered.Publish=q=>real.Broker.PublishMarketQuoteAsync(q);await recovered.Start();
        Assert.True(await recovered.Deliver(BrokerOrderDeliveryFixture.Decode("Futures",BrokerOrderDeliveryFixture.Encode(BrokerOrderDeliveryFixture.Input("Futures",1)))));
        Assert.Equal(cash,(await real.Broker.GetAccountSnapshotAsync()).CashBalance);
        Assert.Single(await real.Broker.ReconcileOrderAsync("mailbox-order"),f=>f.Kind==BrokerObservationKind.Execution);
    }
    [Fact]
    public async Task Failed_emulator_checkpoint_hides_uncommitted_fill_and_retry_commits_it_before_restart()
    {
        var proof=await BrokerEmulatorSettlementFixture.AuditSaveFailure(LedgerPath());
        Assert.False(proof.FirstSettled);Assert.True(proof.RetrySettled);
        Assert.Equal(proof.StartingCash,proof.MemoryCash);Assert.True(proof.RestoredCash<proof.StartingCash);Assert.True(proof.PendingReadsBlocked);
        Assert.Equal(0,proof.MemoryExecutions);Assert.Equal(1,proof.RestoredExecutions);Assert.Equal(1,proof.RetryCheckpointWrites);
    }
}
