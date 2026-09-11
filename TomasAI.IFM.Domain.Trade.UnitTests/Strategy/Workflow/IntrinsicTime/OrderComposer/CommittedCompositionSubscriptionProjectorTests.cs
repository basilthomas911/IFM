using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Realtime;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

public sealed class CommittedCompositionSubscriptionProjectorTests
{
    [Fact]
    public async Task Delivered_event_uses_its_committed_payload_without_reloading_event_log()
    {
        var workflow=new WorkflowStrategyStateUpdatedEvent
        {
            Id=Guid.NewGuid(),EventId=202,WorkflowId=new(Guid.NewGuid()),WorkflowRevision=3,
            State=new(){Status=WorkflowStrategyMachineStatus.Started}
        };
        var journal=Substitute.For<ICommittedBusinessEventJournal>();
        var source=Substitute.For<ICommittedBusinessSubscriptionSource>();
        source.ReadCommittedAsync(workflow,BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow,3,7,202,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DurableAuthorityMutation?>(new InvalidDataException("invalid committed source")));
        var projector=new CommittedCompositionSubscriptionProjector(journal,source,
            Substitute.For<IDurableSubscriptionIntentStore>(),NullLogger<CommittedCompositionSubscriptionProjector>.Instance);
        var context=new ProjectionExecutionContext(nameof(CommittedCompositionSubscriptionProjector),202,7,
            new(nameof(CommittedCompositionSubscriptionProjector),202,EventProjectorEffectKind.TargetProjection),
            Guid.NewGuid(),EventProjectionIdempotencyStrategy.NaturalKeyMutation,CancellationToken.None,3);

        await projector.ProjectCommittedAsync(workflow,context);

        await source.DidNotReceive().ReadAsync(Arg.Any<BusinessSubscriptionSourceReference>(),Arg.Any<CancellationToken>());
        await journal.Received(1).RejectAsync(202,"InvalidCommittedSource","invalid committed source",Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_committed_source_is_quarantined_and_does_not_remain_pending()
    {
        var workflow=new WorkflowStrategyStateUpdatedEvent
        {
            Id=Guid.NewGuid(),WorkflowId=new(Guid.NewGuid()),WorkflowRevision=7,
            State=new(){Status=WorkflowStrategyMachineStatus.Started}
        };
        var row=new EventLogReadModel(1,workflow.EventName,workflow.GetType().AssemblyQualifiedName!,101,
            EventLogMessagePackCodec.Shared.Serialize(workflow),Guid.NewGuid(),DateTime.UtcNow.ToString("O"),7);
        var journal=Substitute.For<ICommittedBusinessEventJournal>();
        journal.ReadPendingAsync(Arg.Any<IReadOnlyList<string>>(),Arg.Any<CancellationToken>())
            .Returns(new[] { row }, Array.Empty<EventLogReadModel>());
        var source=Substitute.For<ICommittedBusinessSubscriptionSource>();
        source.ReadCommittedAsync(Arg.Any<IEvent>(),Arg.Any<BusinessSubscriptionSourceKind>(),Arg.Any<long>(),
                Arg.Any<long>(),Arg.Any<long>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DurableAuthorityMutation?>(new InvalidDataException("Committed workflow snapshot identity is inconsistent.")));
        var store=Substitute.For<IDurableSubscriptionIntentStore>();
        var projector=new CommittedCompositionSubscriptionProjector(journal,source,store,
            NullLogger<CommittedCompositionSubscriptionProjector>.Instance);

        Assert.Equal(1,await projector.ProjectPendingAsync(default));
        Assert.Equal(0,await projector.ProjectPendingAsync(default));
        await journal.Received(1).RejectAsync(101,"InvalidCommittedSource",
            "Committed workflow snapshot identity is inconsistent.",Arg.Any<CancellationToken>());
        await journal.DidNotReceive().AcknowledgeAsync(101,Arg.Any<CancellationToken>());
    }
}
