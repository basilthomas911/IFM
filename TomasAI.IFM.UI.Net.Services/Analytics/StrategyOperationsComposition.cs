using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.UI.EventConsumer;
namespace TomasAI.IFM.UI.Net.Services.Analytics;
/// <summary>Shared framework-neutral composition for both Strategy UI hosts.</summary>
public static class StrategyOperationsComposition
{
 public static StrategyOperationsService Create(IMarketDataAnalyticsQueryApi signals, IFuturesItiSignalUIEventConsumer signalEvents, IIntrinsicTimeStrategyWorkflowQueryApi workflows, IIntrinsicTimeStrategyWorkflowUIEventConsumer workflowEvents) => new(signals,signalEvents,workflows,workflowEvents);
}
