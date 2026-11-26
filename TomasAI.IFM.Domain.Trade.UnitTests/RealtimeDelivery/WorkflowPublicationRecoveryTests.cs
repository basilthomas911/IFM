using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class WorkflowPublicationRecoveryTests
{
    static string Root()=>Path.Combine(Path.GetTempPath(),"ifm-workflow-publication-"+Guid.NewGuid().ToString("N"));
    static FailRegimeDiscoveryCommand Command(IntrinsicTimeStrategyWorkflowView view)=>new()
    {CommandId=Guid.NewGuid(),EntityId=view.EntityId,WorkflowId=view.WorkflowId,InputWorkflowRevision=view.WorkflowRevision,
        FailedAtUtc=view.UpdatedAtUtc,Subject=new(ActorType.Command,FailRegimeDiscoveryCommand.Actor,FailRegimeDiscoveryCommand.Verb,view.EntityId.Format())};

    [Fact]
    public async Task Original_pending_command_reference_is_preserved_and_redispatch_metadata_is_not_intent()
    {
        var source=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);var command=Command(source.State);
        using var journal=new WorkflowPublicationJournal();
        await using var fixture=new WorkflowDispatchDeliveryFixture(publications:journal);
        ICommand? observed=null;fixture.Send=c=>{observed=c;throw new IOException("Reply lost");};
        await Assert.ThrowsAsync<IOException>(()=>WorkflowPublicationDelivery.PublishAsync(fixture.Context,source,command).AsTask());
        Assert.Same(command,observed);Assert.Same(command,Assert.Single(journal.Pending()).Command);
        fixture.Send=c=>{observed=c;return ValueTask.CompletedTask;};
        var alias=source with{Id=Guid.NewGuid(),CommandId=Guid.NewGuid(),UpdatedAtUtc=source.UpdatedAtUtc.AddSeconds(1)};
        Assert.True(await WorkflowPublicationDelivery.RecoverAsync(fixture.Context,alias.State));
        Assert.Same(command,observed);Assert.Empty(journal.Pending());
        await Assert.ThrowsAsync<InvalidDataException>(()=>WorkflowPublicationDelivery.RecoverAsync(fixture.Context,source.State with{UpdatedAtUtc=source.State.UpdatedAtUtc.AddTicks(1)}).AsTask());
    }

    [Fact]
    public void Restart_restores_exact_output_origin_and_frozen_request_without_invoking_factory()
    {
        var root=Root();var source=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true);var command=Command(source.State);
        var origin=MessageOrigins.ForPublication(command,TimeProvider.System,"workflow-test",Guid.NewGuid(),Guid.NewGuid(),source.Id);
        var key=WorkflowPublicationDelivery.Key(source.State);byte[] expected;
        using(var journal=new WorkflowPublicationJournal(root))
        {
            journal.Prepare(key,WorkflowPublicationDelivery.Digest(source.State),source.State,Guid.NewGuid(),command);
            expected=MessagePackSerializer.Serialize(command);
            var captured=journal.FreezeInput(key,"A".PadLeft(64,'A'),()=>command);
            Assert.Equal(origin,MessageOrigins.Get(captured));
        }
        using(var journal=new WorkflowPublicationJournal(root))
        {
            var restored=Assert.Single(journal.Pending());Assert.NotSame(command,restored.Command);
            Assert.Equal(expected,MessagePackSerializer.Serialize((FailRegimeDiscoveryCommand)restored.Command));Assert.Equal(origin,MessageOrigins.Get(restored.Command));
            var input=journal.FreezeInput<FailRegimeDiscoveryCommand>(key,"A".PadLeft(64,'A'),()=>throw new InvalidOperationException("Must not recalculate"));
            Assert.Equal(expected,MessagePackSerializer.Serialize(input));Assert.Equal(origin,MessageOrigins.Get(input));
        }
        Directory.Delete(root,true);
    }

    [Fact]
    public async Task One_unavailable_owner_does_not_prevent_another_entity_from_recovering()
    {
        var view=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true).State;
        using var journal=new WorkflowPublicationJournal();var first=Command(view);var second=Command(view);
        journal.Prepare(Guid.NewGuid(),WorkflowPublicationDelivery.Digest(view),view,Guid.Empty,first);
        journal.Prepare(Guid.NewGuid(),WorkflowPublicationDelivery.Digest(view),view,Guid.Empty,second);
        await using var fixture=new WorkflowDispatchDeliveryFixture(publications:journal);
        fixture.Send=c=>c.CommandId==first.CommandId?ValueTask.FromException(new IOException("First owner unavailable")):ValueTask.CompletedTask;
        await Assert.ThrowsAsync<AggregateException>(()=>WorkflowPublicationDelivery.RecoverPendingAsync(fixture.Context).AsTask());
        Assert.Equal(first.CommandId,Assert.Single(journal.Pending()).Command.CommandId);
        Assert.Equal(2,fixture.Commands.Count);
    }

    [Fact]
    public async Task Lost_reply_is_settled_from_authoritative_exact_receipt_without_resending()
    {
        var view=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true).State;
        using var journal=new WorkflowPublicationJournal();var owner=Substitute.For<IWorkflowPublicationOwner>();
        owner.ReconcileAsync(Arg.Any<WorkflowPublication>(),Arg.Any<IIntrinsicTimeStrategyWorkflowRealtimeContext>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkflowPublicationDisposition?>(null));
        await using var fixture=new WorkflowDispatchDeliveryFixture(publications:journal,publicationOwner:owner);
        fixture.Send=_=>ValueTask.FromException(new IOException("Accepted, reply lost"));
        await Assert.ThrowsAsync<IOException>(()=>WorkflowPublicationDelivery.PublishAsync(fixture.Context,view,Command(view)).AsTask());
        owner.ReconcileAsync(Arg.Any<WorkflowPublication>(),Arg.Any<IIntrinsicTimeStrategyWorkflowRealtimeContext>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkflowPublicationDisposition?>(WorkflowPublicationDisposition.Accepted));
        await WorkflowPublicationDelivery.RecoverPendingAsync(fixture.Context);Assert.Empty(journal.Pending());Assert.Single(fixture.Commands);
    }

    [Fact]
    public void Corrupt_pending_frame_blocks_restart_and_preserves_the_evidence()
    {
        var root=Root();var view=WorkflowDispatchDeliveryFixture.RegimeFunctionInput(representative:true).State;
        using(var journal=new WorkflowPublicationJournal(root))journal.Prepare(Guid.NewGuid(),WorkflowPublicationDelivery.Digest(view),view,Guid.Empty,Command(view));
        var file=Assert.Single(Directory.GetFiles(Path.Combine(root,"pending"),"*.wop"));
        var bytes=File.ReadAllBytes(file);bytes[^1]^=1;File.WriteAllBytes(file,bytes);
        Assert.Throws<InvalidDataException>(()=>new WorkflowPublicationJournal(root));Assert.True(File.Exists(file));Directory.Delete(root,true);
    }
}
