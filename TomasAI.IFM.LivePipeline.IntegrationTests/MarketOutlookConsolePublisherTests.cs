using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class MarketOutlookConsolePublisherTests
{
    [Fact]
    public async Task Identical_notices_are_suppressed_but_price_changes_and_new_value_dates_are_delivered()
    {
        var notices = new System.Collections.Concurrent.ConcurrentQueue<StatusConsoleLoggedEvent>();
        var console = Substitute.For<IStatusConsoleEventProducer>();console.IsRunning.Returns(true);
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        console.PublishConsoleAsync(Arg.Any<StatusConsoleLoggedEvent>()).Returns(call => {
            notices.Enqueue(call.Arg<StatusConsoleLoggedEvent>());
            if(notices.Count == 3) delivered.TrySetResult();
            return ValueTask.CompletedTask;
        });
        var inner = Substitute.For<IMarketOutlookSnapshotPublisher>();
        using var publisher = new MarketOutlookConsolePublisher(inner,console,TimeProvider.System,NullLogger<MarketOutlookConsolePublisher>.Instance);
        var snapshot = Snapshot(DateTime.UtcNow,7700m);
        for(var i=0;i<100;i++) await publisher.PublishAsync(Update(snapshot),snapshot with { UpdatedAtUtc = snapshot.UpdatedAtUtc.AddMilliseconds(i) },default);
        var changed = Snapshot(snapshot.UpdatedAtUtc,7701m);
        await publisher.PublishAsync(Update(changed),changed,default);
        await publisher.PublishAsync(Update(changed),changed with { ValueDate = changed.ValueDate.AddDays(1) },default);
        await publisher.StartAsync(default);
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await publisher.StopAsync(default);
        Assert.Equal(3,notices.Count);
        await inner.Received(102).PublishAsync(Arg.Any<MarketOutlookUpdate>(),Arg.Any<MarketOutlookReadModel>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Backend_notice_uses_snapshot_time_and_does_not_require_a_UI()
    {
        var now=DateTime.UtcNow;var snapshot=Snapshot(now,7728.75m);
        var console=Substitute.For<IStatusConsoleEventProducer>();console.IsRunning.Returns(true);
        var received=new TaskCompletionSource<StatusConsoleLoggedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        console.PublishConsoleAsync(Arg.Any<StatusConsoleLoggedEvent>()).Returns(call=>{received.TrySetResult(call.Arg<StatusConsoleLoggedEvent>());return ValueTask.CompletedTask;});
        var inner=Substitute.For<IMarketOutlookSnapshotPublisher>();
        using var publisher=new MarketOutlookConsolePublisher(inner,console,TimeProvider.System,NullLogger<MarketOutlookConsolePublisher>.Instance);
        await publisher.StartAsync(default);
        var update=Update(snapshot);await publisher.PublishAsync(update,snapshot,default);
        var notice=await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(update.UpdateId,notice.Id);Assert.Equal(now,notice.StatusConsoleLog.StatusDate);
        Assert.Equal("ES20261218 Market Outlook refreshed by Component @ 7728.75",notice.StatusConsoleLog.Message);
        await inner.Received(1).PublishAsync(update,snapshot,Arg.Any<CancellationToken>());
        await publisher.StopAsync(default);
    }

    [Fact]
    public async Task A_blocked_console_does_not_block_market_publication_or_shutdown_and_staging_is_bounded()
    {
        var console=Substitute.For<IStatusConsoleEventProducer>();console.IsRunning.Returns(true);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        console.PublishConsoleAsync(Arg.Any<StatusConsoleLoggedEvent>()).Returns(_=>{entered.TrySetResult();return new ValueTask(held.Task);});
        var inner=Substitute.For<IMarketOutlookSnapshotPublisher>();
        using var publisher=new MarketOutlookConsolePublisher(inner,console,TimeProvider.System,NullLogger<MarketOutlookConsolePublisher>.Instance);
        await publisher.StartAsync(default);
        var snapshot=Snapshot(DateTime.UtcNow,7700m);
        await publisher.PublishAsync(Update(snapshot),snapshot,default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        for(var i=0;i<1000;i++)await publisher.PublishAsync(Update(snapshot),snapshot,default).AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        await publisher.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(2));
        held.TrySetResult();
        await inner.Received(1001).PublishAsync(Arg.Any<MarketOutlookUpdate>(),snapshot,Arg.Any<CancellationToken>());
        await console.Received(1).PublishConsoleAsync(Arg.Any<StatusConsoleLoggedEvent>());
    }

    [Fact]
    public async Task Expired_staged_notice_is_not_replayed_as_current_console_status()
    {
        var console=Substitute.For<IStatusConsoleEventProducer>();console.IsRunning.Returns(true);
        using var publisher=new MarketOutlookConsolePublisher(Substitute.For<IMarketOutlookSnapshotPublisher>(),console,TimeProvider.System,NullLogger<MarketOutlookConsolePublisher>.Instance);
        var snapshot=Snapshot(DateTime.UtcNow.AddMinutes(-6),7728m);
        await publisher.PublishAsync(Update(snapshot),snapshot,default);
        await publisher.StartAsync(default);await Task.Delay(50);await publisher.StopAsync(default);
        await console.DidNotReceive().PublishConsoleAsync(Arg.Any<StatusConsoleLoggedEvent>());
    }

    static MarketOutlookReadModel Snapshot(DateTime at,decimal close)=>new(){ContractId="ES20261218",ValueDate=DateOnly.FromDateTime(at),UpdatedAtUtc=at,MarketDataAsOfUtc=at,
        RefreshTrigger=MarketOutlookRefreshTrigger.Component,
        FuturesEodData=new("ES20261218",DateOnly.FromDateTime(at),"ES",7700m,7800m,7600m,close,1000)};
    static EodMarketOutlookUpdate Update(MarketOutlookReadModel snapshot)=>new(){UpdateId=Guid.NewGuid(),EntityId=new(snapshot.ContractId,snapshot.ValueDate),
        ReceivedAtUtc=snapshot.UpdatedAtUtc,MarketDataAsOfUtc=snapshot.MarketDataAsOfUtc,Eod=snapshot.FuturesEodData};
}
