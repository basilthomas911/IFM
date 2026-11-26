using MessagePack;
using NSubstitute;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class FuturesOptionRoutingRecoveryTests
{
    static string DirectoryPath()=>Path.Combine(Path.GetTempPath(),"ifm-option-routing-test",Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("failure")][InlineData("throw")][InlineData("wrong-id")][InlineData("lost-reply")]
    public async Task Failed_branch_is_retained_and_successful_sibling_is_not_repeated(string mode)
    {
        var path=DirectoryPath(); var source=FuturesOptionRoutingDeliveryFixture.Input(1);
        var accepted=new Dictionary<Guid,byte[]>(); ChangeTradeLegDataCommand? original=null;
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path))
        {
            first.Send=command=>
            {
                if(command.EntityId.Trade.TradeId==1) {accepted.TryAdd(command.CommandId,MessagePackSerializer.Serialize(command)); return ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(command.CommandId));}
                original=command;
                if(mode=="lost-reply") accepted.TryAdd(command.CommandId,MessagePackSerializer.Serialize(command));
                if(mode is "throw" or "lost-reply") throw new IOException("Injected transport fault.");
                return ValueTask.FromResult<ServiceResult<Guid>>(mode=="wrong-id"?new ServiceOk<Guid>(Guid.NewGuid()):new ServiceFailed<Guid>(1,"Rejected"));
            };
            await first.Start(); Assert.False(await first.Deliver(source));
            Assert.Single(first.Journal.Pending());
        }
        await using var recovered=new FuturesOptionRoutingDeliveryFixture(path);
        recovered.Send=command=>
        {
            var bytes=MessagePackSerializer.Serialize(command);
            Assert.Equal(MessagePackSerializer.Serialize(original!),bytes);
            Assert.Equal(MessageOrigins.Get(original!),MessageOrigins.Get(command));
            if(accepted.TryGetValue(command.CommandId,out var existing)) Assert.Equal(existing,bytes);
            else accepted.Add(command.CommandId,bytes);
            return ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(command.CommandId));
        };
        await recovered.Start(); Assert.Single(recovered.Attempts); Assert.Equal(2,accepted.Count);
        Assert.True(await recovered.Deliver(source)); Assert.Single(recovered.Attempts);
        Assert.Empty(recovered.Journal.Pending());
    }
    [Fact]
    public async Task Newer_source_cannot_pass_failed_branch_and_recovery_keeps_order()
    {
        await using var fixture=new FuturesOptionRoutingDeliveryFixture(DirectoryPath());
        fixture.Send=command=>ValueTask.FromResult<ServiceResult<Guid>>(command.EntityId.Trade.TradeId==2?new ServiceFailed<Guid>(1,"Rejected"):new ServiceOk<Guid>(command.CommandId));
        await fixture.Start(); Assert.False(await fixture.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));
        Assert.False(await fixture.Deliver(FuturesOptionRoutingDeliveryFixture.Input(2)));
        Assert.All(fixture.Attempts,command=>Assert.Equal(1,command.SourceSequence));
        fixture.Send=null; Assert.True(await fixture.Deliver(FuturesOptionRoutingDeliveryFixture.Input(2)));
        Assert.Equal(new long[]{1,1,1,1,2,2},fixture.Attempts.Select(command=>command.SourceSequence));
    }
    [Fact]
    public async Task Closed_generation_retires_unfinished_branch_without_rerouting_old_tick()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path))
        {
            first.Send=command=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));
            await first.Start(); Assert.False(await first.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));
        }
        await using var recovered=new FuturesOptionRoutingDeliveryFixture(path,branches:0);
        await recovered.Start(); Assert.Empty(recovered.Attempts);Assert.Empty(recovered.Journal.Pending());
        Assert.All(recovered.Journal.Find(FuturesOptionRoutingDeliveryFixture.Input(1))!.Stages,stage=>Assert.Equal(OptionRouteStage.Superseded,stage));
    }
    [Fact]
    public async Task Authoritative_retirement_clears_failure_health_only_after_pending_branches_settle()
    {
        await using var fixture=new FuturesOptionRoutingDeliveryFixture(DirectoryPath());
        var progress=new TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Model.OptionQuoteRoutingProgress();
        fixture.Context.RoutingProgress.Returns(progress);
        fixture.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));
        await fixture.Start();Assert.False(await fixture.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));
        Assert.False(progress.IsHealthy(DateTime.UtcNow.AddMinutes(1)));
        foreach(var n in new[]{1,2})fixture.Routes.RemovePosition(FuturesOptionRoutingDeliveryFixture.Route(n).StrategyPositionId,2);
        Assert.True(await fixture.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));
        Assert.Single(fixture.Attempts);Assert.True(progress.IsHealthy(DateTime.UtcNow.AddMinutes(1)));Assert.Empty(fixture.Journal.Pending());
    }
    [Fact]
    public async Task Behind_snapshot_cannot_retire_a_newer_captured_generation()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path,branches:1))
        {
            await first.Start();first.Routes.Add(FuturesOptionRoutingDeliveryFixture.Route(1) with{Generation=2},"ESZ6 C5500");
            first.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));
            Assert.False(await first.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));
        }
        await using var recovered=new FuturesOptionRoutingDeliveryFixture(path,branches:1);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await recovered.Start());
        Assert.Empty(recovered.Attempts);Assert.Single(recovered.Journal.Pending());
    }
    [Fact]
    public async Task Quote_and_pricing_completion_keep_distinct_identity_and_exact_risk_across_restart()
    {
        var path=DirectoryPath();var quote=FuturesOptionRoutingDeliveryFixture.Input(1);var priced=FuturesOptionRoutingDeliveryFixture.Input(1,"Pricing");
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path))
        {
            await first.Start();Assert.True(await first.Deliver(quote));Assert.True(await first.Deliver(priced));
            Assert.Equal(4,first.Attempts.Count);Assert.Equal(4,first.Attempts.Select(c=>c.CommandId).Distinct().Count());
            Assert.All(first.Attempts.Take(2),command=>Assert.False(command.OptionTick!.IsPricingUpdate));
            Assert.All(first.Attempts.Skip(2),command=>Assert.True(command.OptionTick!.IsPricingUpdate));
        }
        await using var recovered=new FuturesOptionRoutingDeliveryFixture(path);await recovered.Start();
        Assert.True(await recovered.Deliver(quote));Assert.True(await recovered.Deliver(priced));Assert.Empty(recovered.Attempts);Assert.Empty(recovered.Publications);
        Assert.True(await recovered.Deliver(FuturesOptionRoutingDeliveryFixture.Input(2)));Assert.Equal(2,recovered.Attempts.Count);
        await Assert.ThrowsAsync<InvalidDataException>(async()=>await (quote with { ReceivedOn=quote.ReceivedOn.AddSeconds(1) }).ExecuteAsync(recovered.Context));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task Partial_snapshot_publication_is_recoverable_with_original_ids_payloads_and_times(bool lostReply)
    {
        var path=DirectoryPath();var source=FuturesOptionRoutingDeliveryFixture.Input(1);TradeRealtimeSnapshotChangedEvent? original=null;
        var accepted=new Dictionary<Guid,byte[]>();
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path))
        {
            first.Publish=publication=>
            {
                if(publication.EntityId.TradeId==2)
                {
                    original=publication;if(lostReply)accepted.Add(publication.Id,MessagePackSerializer.Serialize(publication));
                    throw new IOException("Injected partial publication fault.");
                }
                accepted.Add(publication.Id,MessagePackSerializer.Serialize(publication));return ValueTask.CompletedTask;
            };
            await first.Start();Assert.False(await first.Deliver(source));Assert.Empty(first.Attempts);Assert.Single(first.Journal.Pending());
        }
        await using var recovered=new FuturesOptionRoutingDeliveryFixture(path);recovered.Time.Now+=TimeSpan.FromDays(1);
        recovered.Publish=publication=>
        {
            Assert.Equal(original!.Id,publication.Id);Assert.Equal(MessagePackSerializer.Serialize(original),MessagePackSerializer.Serialize(publication));
            Assert.Equal(MessageOrigins.Get(original),MessageOrigins.Get(publication));
            if(accepted.TryGetValue(publication.Id,out var bytes))Assert.Equal(bytes,MessagePackSerializer.Serialize(publication));else accepted.Add(publication.Id,MessagePackSerializer.Serialize(publication));
            return ValueTask.CompletedTask;
        };
        await recovered.Start();Assert.Single(recovered.Publications);Assert.Equal(2,accepted.Count);Assert.Equal(2,recovered.Attempts.Count);
        Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Publications);Assert.Empty(recovered.Journal.Pending());
    }
    [Fact]
    public async Task Different_contract_progress_does_not_wait_for_a_blocked_contract()
    {
        using var journal=new FuturesOptionRoutingReceipts();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked=journal.SerializeAsync("ES",async()=>{entered.SetResult();await release.Task;}).AsTask();
        await entered.Task;var other=false;
        await journal.SerializeAsync("VX",()=>{other=true;return Task.CompletedTask;}).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(other);Assert.False(blocked.IsCompleted);release.SetResult();await blocked;
    }
    [Theory]
    [InlineData("capture")][InlineData("archive")]
    public async Task Durable_write_failure_retains_source_and_retry_resumes_without_losing_branches(string phase)
    {
        var path=DirectoryPath();var source=FuturesOptionRoutingDeliveryFixture.Input(1);var id=FuturesOptionRoutingReceipts.Identity(source);
        await using var fixture=new FuturesOptionRoutingDeliveryFixture(path);await fixture.Start();
        var obstruction=phase switch
        {
            "capture"=>Path.Combine(path,"pending",id.ToString("N")+".ort.tmp"),
            _=>Path.Combine(path,"history",id.ToString("N")[..2],id.ToString("N")+".ort")
        };
        if(phase=="archive")fixture.Send=command=>
        {
            if(command.EntityId.Trade.TradeId==2)System.IO.Directory.CreateDirectory(obstruction);
            return ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(command.CommandId));
        };
        else System.IO.Directory.CreateDirectory(obstruction);
        Assert.False(await fixture.Deliver(source));Assert.Equal(phase=="capture"?0:2,fixture.Attempts.Count);
        System.IO.Directory.Delete(obstruction);
        Assert.True(await fixture.Deliver(source));Assert.Equal(2,fixture.Attempts.Count);Assert.Empty(fixture.Journal.Pending());
    }
    [Fact]
    public async Task Corrupt_pending_capture_blocks_restart()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path))
        {first.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));await first.Start();Assert.False(await first.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));}
        var frame=System.IO.Directory.GetFiles(Path.Combine(path,"pending"),"*.ort").Single();var bytes=File.ReadAllBytes(frame);bytes[^1]^=1;File.WriteAllBytes(frame,bytes);
        Assert.Throws<InvalidDataException>(()=>new FuturesOptionRoutingReceipts(path));
    }
    [Fact]
    public async Task Pending_prepared_before_dispatch_restores_exact_source_and_commands()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesOptionRoutingDeliveryFixture(path))
        {first.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));await first.Start();Assert.False(await first.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));}
        using(var journal=new FuturesOptionRoutingReceipts(path))
        {var receipt=journal.Pending().Single();Assert.Equal("GLBX.MDP3",receipt.Source.SourceDataset);Assert.Equal(FuturesOptionRoutingDeliveryFixture.Input(1).SourceGenerationId,receipt.Source.SourceGenerationId);Assert.Equal(2,receipt.Commands.Length);}
        await using var recovered=new FuturesOptionRoutingDeliveryFixture(path);await recovered.Start();Assert.Equal(2,recovered.Attempts.Count);
        Assert.True(await recovered.Deliver(FuturesOptionRoutingDeliveryFixture.Input(1)));Assert.Equal(2,recovered.Attempts.Count);
    }
}
