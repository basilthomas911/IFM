using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

namespace TomasAI.IFM.UI.EventConsumer;

public interface IIntrinsicTimeStrategyWorkflowUIEventConsumer
{
    ValueTask StartAsync(
        Guid siteId,
        Action<IntrinsicTimeStrategyWorkflowUpdatedNotifyEvent> eventAction);

    ValueTask StopAsync(Guid siteId);
}
