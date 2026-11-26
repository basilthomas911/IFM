using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowInitializationHandoffAuditTests
{
    static string DirectoryForTest()=>Path.Combine(Path.GetTempPath(),"ifm-init-terminal",Guid.NewGuid().ToString("N"));
    [Theory][InlineData(false,"send")][InlineData(true,"send")][InlineData(false,"typed")][InlineData(true,"typed")][InlineData(false,"lost")][InlineData(true,"lost")]
    public async Task Failed_initialization_send_retains_original_terminal_across_retry_and_cold_start(bool metrics,string mode)
    {
        var directory=DirectoryForTest();var source=WorkflowDispatchDeliveryFixture.Input() with{DispatchRegimeDiscovery=false};
        FailRegimeDiscoveryCommand original;
        using(var journal=new WorkflowInitializationTerminals(directory))
        {
            await using var state=new WorkflowDispatchDeliveryFixture(metrics,terminals:journal);
            state.TypedRejected=mode=="typed";
            state.Send=_=>mode=="typed"?ValueTask.CompletedTask:ValueTask.FromException(new IOException("Original terminal not confirmed."));
            await state.Start();Assert.False(await state.Deliver(source));
            original=Assert.IsType<FailRegimeDiscoveryCommand>(Assert.Single(state.Commands));Assert.Equal(original,Assert.Single(journal.Pending()).Command);
            state.Time.At=state.Time.At.AddHours(1);
            Assert.False(await state.Deliver(source));Assert.Equal(original,state.Commands.Last());Assert.Equal(2,state.Commands.Count);
        }
        using(var journal=new WorkflowInitializationTerminals(directory))
        {
            await using var recovered=new WorkflowDispatchDeliveryFixture(terminals:journal);recovered.Time.At=recovered.Time.At.AddDays(1);
            await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));Assert.Empty(journal.Pending());
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);
            Assert.True(journal.Find(source.Id)!.Sent);
        }
        using(var journal=new WorkflowInitializationTerminals(directory))
        {
            await using var replay=new WorkflowDispatchDeliveryFixture(terminals:journal);
            await replay.Start();Assert.True(await replay.Deliver(source));Assert.Empty(replay.Commands);
        }
    }
    [Fact]
    public async Task Changed_source_and_corrupt_frame_fail_closed_and_writer_is_exclusive()
    {
        var directory=DirectoryForTest();var source=WorkflowDispatchDeliveryFixture.Input() with{DispatchRegimeDiscovery=false};
        using(var journal=new WorkflowInitializationTerminals(directory))
        {
            Assert.Throws<IOException>(()=>new WorkflowInitializationTerminals(directory));
            await using var state=new WorkflowDispatchDeliveryFixture(terminals:journal);await state.Start();Assert.True(await state.Deliver(source));
            Assert.False(await state.Deliver(source with{State=source.State with{WorkflowRevision=2}}));
        }
        var file=Directory.GetFiles(Path.Combine(directory,"sent"),"*.wit",SearchOption.AllDirectories).Single();
        var bytes=File.ReadAllBytes(file);bytes[^1]^=1;File.WriteAllBytes(file,bytes);
        using var restored=new WorkflowInitializationTerminals(directory);
        Assert.Throws<InvalidDataException>(()=>restored.Find(source.Id));
    }
    [Fact]
    public async Task Receipt_write_failure_prevents_send_and_confirmed_pending_marker_archives_without_resend()
    {
        var directory=DirectoryForTest();var source=WorkflowDispatchDeliveryFixture.Input() with{DispatchRegimeDiscovery=false};
        using(var journal=new WorkflowInitializationTerminals(directory))
        {
            await using var state=new WorkflowDispatchDeliveryFixture(terminals:journal);await state.Start();
            var temporary=Path.Combine(directory,"pending",source.Id.ToString("N")+".wit.tmp");
            using(var locked=new FileStream(temporary,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {Assert.False(await state.Deliver(source));Assert.Empty(state.Commands);}
            Assert.True(await state.Deliver(source));Assert.Single(state.Commands);
        }
        var sent=Directory.GetFiles(Path.Combine(directory,"sent"),"*.wit",SearchOption.AllDirectories).Single();
        File.Move(sent,Path.Combine(directory,"pending",Path.GetFileName(sent)));
        using(var journal=new WorkflowInitializationTerminals(directory))
        {
            Assert.True(Assert.Single(journal.Pending()).Sent);
            await using var state=new WorkflowDispatchDeliveryFixture(terminals:journal);await state.Start();
            Assert.Empty(state.Commands);Assert.Empty(journal.Pending());Assert.True(await state.Deliver(source));Assert.Empty(state.Commands);
        }
    }
}
