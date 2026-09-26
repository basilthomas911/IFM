namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime;

/// <summary>Controls admission of new signal-triggered strategy workflows.</summary>
public interface IIntrinsicTimeWorkflowStartPolicy
{
    /// <summary>Gets whether a new signal may start a strategy workflow.</summary>
    bool Enabled { get; }
}
