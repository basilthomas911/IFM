using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.EventConsumers;

public sealed class IntrinsicTimeStrategyWorkflowUIEventConsumerTests
{
    [Fact]
    public void Dispatch_ForwardsCompleteSnapshotAndIsolatesSubscribers()
    {
        var consumer = new IntrinsicTimeStrategyWorkflowUIEventConsumer(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger>());
        var recorded = new List<long>();

        consumer.AddSubscriber(Guid.NewGuid(), _ => throw new InvalidOperationException("view failed"))
            .Should().BeTrue();
        consumer.AddSubscriber(Guid.NewGuid(), notification => recorded.Add(notification.WorkflowRevision))
            .Should().BeFalse();

        consumer.Dispatch(new IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent
        {
            WorkflowRevision = 7,
            State = new IntrinsicTimeStrategyWorkflowView { WorkflowRevision = 7 }
        });

        recorded.Should().Equal(7);
    }

    [Fact]
    public void Dispatch_DoesNotApplyFrontendWorkflowValidation()
    {
        var consumer = new IntrinsicTimeStrategyWorkflowUIEventConsumer(
            new NatsEventListenerOptions(),
            Substitute.For<ILogger>());
        IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent? received = null;
        consumer.AddSubscriber(Guid.NewGuid(), notification => received = notification);

        var notification = new IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent
        {
            CommandId = Guid.Empty,
            WorkflowRevision = 0,
            State = new IntrinsicTimeStrategyWorkflowView()
        };
        consumer.Dispatch(notification);

        received.Should().BeSameAs(notification);
    }
}
