using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime;

/// <summary>Controls admission of new signal-triggered strategy workflows.</summary>
public interface IIntrinsicTimeWorkflowStartPolicy
{
    /// <summary>Gets whether a new signal may start a strategy workflow.</summary>
    bool Enabled { get; }
    bool RequiresAssignment => false;
    IntrinsicTimeFundAssignment? ResolveAssignment(FuturesItiSignalEntityId signal, DateTime now) => null;
}
