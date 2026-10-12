using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
/// <summary>Identifies how independent specialist calculations are scheduled.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing compact execution-mode contract.")]
public enum RegimeDiscoveryExecutionMode : byte
{
    /// <summary>Specialists execute deterministically on the current actor task.</summary>
    Sequential = 0,
    /// <summary>Specialists execute on ordinary .NET thread-pool tasks and are awaited together.</summary>
    ThreadPoolParallel = 1
}
