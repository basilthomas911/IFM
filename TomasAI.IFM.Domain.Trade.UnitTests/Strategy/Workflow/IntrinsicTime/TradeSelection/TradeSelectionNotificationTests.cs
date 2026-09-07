using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

public sealed class TradeSelectionNotificationTests
{
    [Theory]
    [InlineData("current", true)]
    [InlineData("missing", false)]
    [InlineData("cancelled", false)]
    [InlineData("expired", false)]
    [InlineData("new-workflow", false)]
    [InlineData("new-revision", false)]
    [InlineData("new-stage", false)]
    public async Task Delayed_notification_requires_the_current_authoritative_workflow(string scenario, bool allowed)
    {
        var c = await TradeSelectionFixture.Command();
        var notification = c.WorkflowView;
        IntrinsicTimeStrategyWorkflowView? current = scenario switch
        {
            "missing" => null,
            "cancelled" => notification with { Status = WorkflowStrategyMachineStatus.Cancelled },
            "expired" => notification with { Status = WorkflowStrategyMachineStatus.TimedOut },
            "new-workflow" => notification with { WorkflowId = new StrategyWorkflowId(Guid.NewGuid()) },
            "new-revision" => notification with { WorkflowRevision = notification.WorkflowRevision + 1 },
            "new-stage" => notification with { CurrentStage = StrategyWorkflowStage.OrderComposition },
            _ => notification
        };
        IntrinsicTimeStrategyWorkflowRealtimeActor.IsCurrentSelectionNotification(notification, current).Should().Be(allowed);
    }
}
