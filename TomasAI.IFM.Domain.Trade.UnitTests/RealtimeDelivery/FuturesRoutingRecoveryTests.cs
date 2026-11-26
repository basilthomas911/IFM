using MessagePack;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Futures.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class FuturesRoutingRecoveryTests
{
    static string DirectoryPath()=>Path.Combine(Path.GetTempPath(),"ifm-futures-routing-test",Guid.NewGuid().ToString("N"));
    [Theory]
    [InlineData("failure")][InlineData("throw")][InlineData("wrong-id")][InlineData("lost-reply")]
    public async Task Failed_branch_is_retained_and_successful_sibling_is_not_repeated(string mode)
    {
        var path=DirectoryPath(); var source=FuturesRoutingDeliveryFixture.Input(1);
        var accepted=new Dictionary<Guid,byte[]>(); ChangeTradeLegDataCommand? original=null;
        await using(var first=new FuturesRoutingDeliveryFixture(path))
        {
            first.Send=command=>
            {
                if(command.EntityId.Trade.TradeId==1) {accepted.TryAdd(command.CommandId,MessagePackSerializer.Serialize(command)); return ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(command.CommandId));}
                original=command;
                if(mode=="lost-reply") accepted.TryAdd(command.CommandId,MessagePackSerializer.Serialize(command));
                if(mode is "throw" or "lost-reply") throw new IOException("Injected transport fault.");
                return ValueTask.FromResult<ServiceResult<Guid>>(mode=="wrong-id"?new ServiceOk<Guid>(Guid.NewGuid()):new ServiceFailed<Guid>(1,"Rejected"));
            };
            await first.Start(); Assert.False(await first.Deliver(source)); Assert.False(first.Journal.IsCompletedSequence(source));
            Assert.Single(first.Journal.Pending());
        }
        await using var recovered=new FuturesRoutingDeliveryFixture(path);
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
        Assert.True(recovered.Journal.IsCompletedSequence(source)); Assert.Empty(recovered.Journal.Pending());
    }
    [Fact]
    public async Task Newer_source_cannot_pass_failed_branch_and_recovery_keeps_order()
    {
        await using var fixture=new FuturesRoutingDeliveryFixture(DirectoryPath());
        fixture.Send=command=>ValueTask.FromResult<ServiceResult<Guid>>(command.EntityId.Trade.TradeId==2?new ServiceFailed<Guid>(1,"Rejected"):new ServiceOk<Guid>(command.CommandId));
        await fixture.Start(); Assert.False(await fixture.Deliver(FuturesRoutingDeliveryFixture.Input(1)));
        Assert.False(await fixture.Deliver(FuturesRoutingDeliveryFixture.Input(2)));
        Assert.All(fixture.Attempts,command=>Assert.Equal(1,command.SourceSequence));
        fixture.Send=null; Assert.True(await fixture.Deliver(FuturesRoutingDeliveryFixture.Input(2)));
        Assert.Equal(new long[]{1,1,1,1,2,2},fixture.Attempts.Select(command=>command.SourceSequence));
    }
    [Fact]
    public async Task Closed_generation_retires_unfinished_branch_without_rerouting_old_tick()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesRoutingDeliveryFixture(path))
        {
            first.Send=command=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));
            await first.Start(); Assert.False(await first.Deliver(FuturesRoutingDeliveryFixture.Input(1)));
        }
        await using var recovered=new FuturesRoutingDeliveryFixture(path,branches:0);
        await recovered.Start(); Assert.Empty(recovered.Attempts);Assert.Empty(recovered.Journal.Pending());
        Assert.All(recovered.Journal.Find(FuturesRoutingDeliveryFixture.Input(1))!.Stages,stage=>Assert.Equal(FuturesRouteStage.Superseded,stage));
    }
    [Fact]
    public async Task Behind_snapshot_cannot_retire_a_newer_captured_generation()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesRoutingDeliveryFixture(path,branches:1))
        {
            await first.Start();first.Routes.Add(FuturesRoutingDeliveryFixture.Route(1) with{Generation=2},"ESZ6");
            first.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));
            Assert.False(await first.Deliver(FuturesRoutingDeliveryFixture.Input(1)));
        }
        await using var recovered=new FuturesRoutingDeliveryFixture(path,branches:1);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await recovered.Start());
        Assert.Empty(recovered.Attempts);Assert.Single(recovered.Journal.Pending());
    }
    [Fact]
    public async Task Cursor_survives_restart_and_is_scoped_to_source_epoch()
    {
        var path=DirectoryPath();var source=FuturesRoutingDeliveryFixture.Input(2);
        await using(var first=new FuturesRoutingDeliveryFixture(path)){await first.Start();Assert.True(await first.Deliver(source));}
        await using var recovered=new FuturesRoutingDeliveryFixture(path);await recovered.Start();
        Assert.True(await recovered.Deliver(source));Assert.True(await recovered.Deliver(FuturesRoutingDeliveryFixture.Input(1)));Assert.Empty(recovered.Attempts);
        var newEpoch=source with{Id=Guid.NewGuid(),SourceGenerationId=Guid.NewGuid()};
        Assert.False(recovered.Journal.IsCompletedSequence(newEpoch)); // Receiver epoch acceptance remains a separate contract.
        await Assert.ThrowsAsync<InvalidDataException>(async()=>await (source with{TradeData=source.TradeData with{Price=1m}}).ExecuteAsync(recovered.Context));
    }
    [Fact]
    public async Task Different_contract_progress_does_not_wait_for_a_blocked_contract()
    {
        using var journal=new FuturesRoutingReceipts();
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked=journal.SerializeAsync("ES",async()=>{entered.SetResult();await release.Task;}).AsTask();
        await entered.Task;var other=false;
        await journal.SerializeAsync("VX",()=>{other=true;return Task.CompletedTask;}).AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(other);Assert.False(blocked.IsCompleted);release.SetResult();await blocked;
    }
    [Theory]
    [InlineData("capture")][InlineData("cursor")][InlineData("archive")]
    public async Task Durable_write_failure_retains_source_and_retry_resumes_without_losing_branches(string phase)
    {
        var path=DirectoryPath();var source=FuturesRoutingDeliveryFixture.Input(1);
        await using var fixture=new FuturesRoutingDeliveryFixture(path);await fixture.Start();
        var obstruction=phase switch
        {
            "capture"=>Path.Combine(path,"pending",source.Id.ToString("N")+".frt.tmp"),
            "cursor"=>Path.Combine(path,"cursors",FuturesRoutingReceipts.Stream(source)+".cur.tmp"),
            _=>Path.Combine(path,"history",source.Id.ToString("N")[..2],source.Id.ToString("N")+".frt")
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
        await using(var first=new FuturesRoutingDeliveryFixture(path))
        {first.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));await first.Start();Assert.False(await first.Deliver(FuturesRoutingDeliveryFixture.Input(1)));}
        var frame=System.IO.Directory.GetFiles(Path.Combine(path,"pending"),"*.frt").Single();var bytes=File.ReadAllBytes(frame);bytes[^1]^=1;File.WriteAllBytes(frame,bytes);
        Assert.Throws<InvalidDataException>(()=>new FuturesRoutingReceipts(path));
    }
    [Fact]
    public async Task Pending_prepared_before_dispatch_restores_exact_source_and_commands()
    {
        var path=DirectoryPath();
        await using(var first=new FuturesRoutingDeliveryFixture(path))
        {first.Send=_=>ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"Rejected"));await first.Start();Assert.False(await first.Deliver(FuturesRoutingDeliveryFixture.Input(1)));}
        using(var journal=new FuturesRoutingReceipts(path))
        {var receipt=journal.Pending().Single();Assert.Equal("GLBX.MDP3",receipt.Source.SourceDataset);Assert.Equal(FuturesRoutingDeliveryFixture.Input(1).SourceGenerationId,receipt.Source.SourceGenerationId);Assert.Equal(2,receipt.Commands.Length);}
        await using var recovered=new FuturesRoutingDeliveryFixture(path);await recovered.Start();Assert.Equal(2,recovered.Attempts.Count);
        Assert.True(await recovered.Deliver(FuturesRoutingDeliveryFixture.Input(1)));Assert.Equal(2,recovered.Attempts.Count);
    }
}
