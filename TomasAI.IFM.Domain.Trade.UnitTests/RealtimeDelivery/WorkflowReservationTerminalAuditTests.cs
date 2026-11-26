using TomasAI.IFM.TestSupport;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowReservationTerminalAuditTests
{
    static string DirectoryForTest()=>Path.Combine(Path.GetTempPath(),"ifm-init-terminal",Guid.NewGuid().ToString("N"));
    static WorkflowDispatchDeliveryFixture Create(WorkflowReservationFailures journal,TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent source,bool metrics=false)
    {
        var state=new WorkflowDispatchDeliveryFixture(metrics,reservationFailures:journal);
        state.ReadCurrent=_=>Task.FromResult<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView?>(source.State);
        return state;
    }
    [Theory][InlineData(false,"send")][InlineData(true,"send")][InlineData(false,"typed")][InlineData(true,"typed")][InlineData(false,"lost")][InlineData(true,"lost")]
    public async Task Failed_reservation_send_retains_original_terminal_across_retry_and_cold_start(bool metrics,string mode)
    {
        var directory=DirectoryForTest();var source=WorkflowDispatchDeliveryFixture.ReservationInput();
        FailTradeSelectionCommand original;
        using(var journal=new WorkflowReservationFailures(directory))
        {
            await using var state=Create(journal,source,metrics);
            state.TypedRejected=mode=="typed";
            state.Send=_=>mode=="typed"?ValueTask.CompletedTask:ValueTask.FromException(new IOException("Original terminal not confirmed."));
            await state.Start();Assert.False(await state.Deliver(source));
            original=Assert.IsType<FailTradeSelectionCommand>(Assert.Single(state.Commands));Assert.Equal(original,Assert.Single(journal.Pending()).Command);
            state.Time.At=state.Time.At.AddHours(1);
            Assert.False(await state.Deliver(source));Assert.Equal(original,state.Commands.Last());Assert.Equal(2,state.Commands.Count);
            Assert.Single(state.Context.PortfolioCommands.ReceivedCalls(),call=>call.GetMethodInfo().Name=="ReserveCompositionAsync");
        }
        using(var journal=new WorkflowReservationFailures(directory))
        {
            await using var recovered=Create(journal,source);recovered.Time.At=recovered.Time.At.AddDays(1);
            await recovered.Start();Assert.Equal(original,Assert.Single(recovered.Commands));Assert.Empty(journal.Pending());
            Assert.True(await recovered.Deliver(source));Assert.Single(recovered.Commands);
            Assert.True(journal.Find(source.Id)!.Sent);
            Assert.DoesNotContain(recovered.Context.PortfolioCommands.ReceivedCalls(),call=>call.GetMethodInfo().Name=="ReserveCompositionAsync");
        }
        using(var journal=new WorkflowReservationFailures(directory))
        {
            await using var replay=Create(journal,source);
            await replay.Start();Assert.True(await replay.Deliver(source));Assert.Empty(replay.Commands);
        }
    }
    [Fact]
    public async Task Changed_source_and_corrupt_frame_fail_closed_and_writer_is_exclusive()
    {
        var directory=DirectoryForTest();var source=WorkflowDispatchDeliveryFixture.ReservationInput();
        using(var journal=new WorkflowReservationFailures(directory))
        {
            Assert.Throws<IOException>(()=>new WorkflowReservationFailures(directory));
            await using var state=Create(journal,source);await state.Start();Assert.True(await state.Deliver(source));
            Assert.False(await state.Deliver(source with{State=source.State with{WorkflowRevision=2}}));
        }
        var file=Directory.GetFiles(Path.Combine(directory,"sent"),"*.wrf",SearchOption.AllDirectories).Single();
        var bytes=File.ReadAllBytes(file);bytes[^1]^=1;File.WriteAllBytes(file,bytes);
        using var restored=new WorkflowReservationFailures(directory);
        Assert.Throws<InvalidDataException>(()=>restored.Find(source.Id));
    }
    [Fact]
    public async Task Receipt_write_failure_prevents_send_and_confirmed_pending_marker_archives_without_resend()
    {
        var directory=DirectoryForTest();var source=WorkflowDispatchDeliveryFixture.ReservationInput();
        using(var journal=new WorkflowReservationFailures(directory))
        {
            await using var state=Create(journal,source);await state.Start();
            var temporary=Path.Combine(directory,"pending",source.Id.ToString("N")+".wrf.tmp");
            using(var locked=new FileStream(temporary,FileMode.Create,FileAccess.ReadWrite,FileShare.None))
            {Assert.False(await state.Deliver(source));Assert.Empty(state.Commands);}
            Assert.True(await state.Deliver(source));Assert.Single(state.Commands);
        }
        var sent=Directory.GetFiles(Path.Combine(directory,"sent"),"*.wrf",SearchOption.AllDirectories).Single();
        File.Move(sent,Path.Combine(directory,"pending",Path.GetFileName(sent)));
        using(var journal=new WorkflowReservationFailures(directory))
        {
            Assert.True(Assert.Single(journal.Pending()).Sent);
            await using var state=Create(journal,source);await state.Start();
            Assert.Empty(state.Commands);Assert.Empty(journal.Pending());Assert.True(await state.Deliver(source));Assert.Empty(state.Commands);
        }
    }
}
